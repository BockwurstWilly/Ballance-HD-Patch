// Ballance HD 2.0 - native Virtools plugin (Managers\BallanceHD.dll)
//
// The player loads it like the game's own managers and calls it every frame:
//   PreProcess          controller -> keys for the menus, stepless rolling (the game's own force block)
//   PostProcess         third-person camera (free turning / tilting, fixed distance, distance steps, raising)
//   OnPreRender         widescreen: screen aspect + Hor+ field of view for the camera being rendered
//   OnPostRender        the camera's own values back (the game's scripts never see them changed)
//   OnPostSpriteRender  button prompts, controls legend, controller settings next to Options - Controls, FPS
// Settings: <game>\BallanceHD\Settings.ini (the HD launcher edits the same file).
// Built with clang for the Microsoft C++ ABI (the engine is MSVC-compiled), without the Visual C++ runtime.

#include <math.h>
#include "CKAll.h"
#include "win.h"
#include "Draw.h"

#define BALLANCEHD_GUID CKGUID(0x6b1d2c40, 0x3a7e41f1)

// ---- small helpers -----------------------------------------------------------------------------
static bool Same(const char *a, const char *b)
{
    if (!a || !b)
        return false;
    while (*a && *a == *b) { a++; b++; }
    return *a == *b;
}

static float Absf(float v) { return v < 0 ? -v : v; }
static float Minf(float a, float b) { return a < b ? a : b; }
static float Maxf(float a, float b) { return a > b ? a : b; }

static void IntText(int v, char *out, const char *suffix)
{
    char tmp[12];
    int t = 0, n = 0;
    if (v < 0) { out[n++] = '-'; v = -v; }
    do { tmp[t++] = (char)('0' + v % 10); v /= 10; } while (v && t < 11);
    while (t) out[n++] = tmp[--t];
    while (suffix && *suffix) out[n++] = *suffix++;
    out[n] = 0;
}

// trace line for tests: position, camera frame direction, smoothed stick (values x100)
static void wsprintf_trace(char *buf, const VxVector &p, const VxVector &d, float sx, float sy)
{
    float v[7] = { p.x, p.y, p.z, d.x, d.z, sx, sy };
    const char *names[7] = { "pos ", " ", " ", "  dir ", " ", "  stick ", " " };
    int n = 0;
    for (int i = 0; i < 7; i++)
    {
        for (const char *c = names[i]; *c; c++) buf[n++] = *c;
        IntText((int)(v[i] * 100.0f), buf + n, NULL);
        while (buf[n]) n++;
    }
    buf[n] = 0;
}

// ---- buttons and actions -----------------------------------------------------------------------
static const char *const kButtons[13] = { "A / Cross", "B / Circle", "X / Square", "Y / Triangle", "LB / L1", "RB / R1",
                                          "LT / L2", "RT / R2", "Start / Options", "Back / Share", "L3", "R3", "-" };
static const char *const kGlyphs[13] = { "a", "b", "x", "y", "lb", "rb", "lt", "rt", "start", "back", "l3", "r3", "" };
static const int kMasks[13] = { HD_A, HD_B, HD_X, HD_Y, HD_LB, HD_RB, HD_LT, HD_RT, HD_START, HD_BACK, HD_LTHUMB, HD_RTHUMB, 0 };

enum Action { A_CONFIRM, A_BACK, A_PAUSE, A_LIFT, A_LEFT, A_RIGHT, A_SKIP, A_DISTANCE, A_COUNT };
static const char *const kActionKeys[A_COUNT] = { "Confirm", "Back", "Pause", "CameraLift", "CameraLeft", "CameraRight", "SkipTutorial", "CameraDistance" };
static const int kActionDefaults[A_COUNT] = { 0, 1, 8, 3, 4, 5, 2, 9 };
static const char *const kActionDe[A_COUNT] = { "Bestätigen", "Zurück", "Pause-Menü", "Kamera anheben", "Kamera 90° links",
                                                "Kamera 90° rechts", "Einführung überspringen", "Abstand wechseln" };
static const char *const kActionEn[A_COUNT] = { "Confirm", "Back", "Pause menu", "Raise camera", "Camera 90° left",
                                                "Camera 90° right", "Skip tutorial", "Cycle distance" };
static const float kDistances[4] = { 30.0f, 41.3f, 55.0f, 70.0f };
static const char *const kDistDe[4] = { "Nah", "Normal", "Weit", "Sehr weit" };
static const char *const kDistEn[4] = { "Near", "Normal", "Far", "Very far" };

// scan codes as DirectInput reads them (0x1xx = extended)
enum { K_UP = 0x148, K_DOWN = 0x150, K_LEFT = 0x14B, K_RIGHT = 0x14D, K_SHIFT = 0x2A, K_SPACE = 0x39, K_ENTER = 0x1C, K_ESC = 0x01, K_Q = 0x10 };

// ---- settings ----------------------------------------------------------------------------------
struct Settings
{
    bool german = false, prompts = true, wide = true, showFps = false, trace = false;
    int frameLimit = 0;            // 0 = vsync, -1 = free, n = limit
    bool pad = true, analogRoll = true, freeCam = true, invertY = false;
    int deadzone = 35, camSpeed = 100, camDistance = 1;
    int button[A_COUNT];

    void Load()
    {
        char buf[64];
        hd_ini_str("General", "Language", "en", buf, sizeof(buf));
        german = Same(buf, "de");
        prompts = hd_ini_int("General", "Prompts", 1) != 0;
        wide = hd_ini_int("General", "Widescreen", 1) != 0;
        showFps = hd_ini_int("General", "ShowFps", 0) != 0;
        trace = hd_ini_int("Debug", "Trace", 0) != 0;
        frameLimit = hd_ini_int("General", "FrameLimit", 0);
        pad = hd_ini_int("Controller", "Enabled", 1) != 0;
        deadzone = hd_ini_int("Controller", "DeadZone", 35);
        analogRoll = hd_ini_int("Controller", "AnalogRoll", 1) != 0;
        freeCam = hd_ini_int("Controller", "FreeCamera", 1) != 0;
        invertY = hd_ini_int("Controller", "InvertY", 0) != 0;
        camSpeed = hd_ini_int("Controller", "CameraSpeed", 100);
        camDistance = hd_ini_int("Controller", "CameraDistance", 1);
        if (deadzone < 10) deadzone = 10;
        if (deadzone > 80) deadzone = 80;
        if (camSpeed < 25) camSpeed = 25;
        if (camSpeed > 300) camSpeed = 300;
        if (camDistance < 0 || camDistance > 3) camDistance = 1;
        for (int a = 0; a < A_COUNT; a++)
        {
            hd_ini_str("Buttons", kActionKeys[a], kButtons[kActionDefaults[a]], buf, sizeof(buf));
            button[a] = kActionDefaults[a];
            for (int b = 0; b < 13; b++)
                if (Same(buf, kButtons[b]))
                    button[a] = b;
        }
    }

    void Save()
    {
        hd_ini_set_int("Controller", "AnalogRoll", analogRoll);
        hd_ini_set_int("Controller", "FreeCamera", freeCam);
        hd_ini_set_int("Controller", "InvertY", invertY);
        hd_ini_set_int("Controller", "CameraSpeed", camSpeed);
        hd_ini_set_int("Controller", "CameraDistance", camDistance);
        for (int a = 0; a < A_COUNT; a++)
            hd_ini_set_str("Buttons", kActionKeys[a], kButtons[button[a]]);
    }

    int Mask(int a) const { return kMasks[button[a]]; }
    const char *Glyph(int a) const { return kGlyphs[button[a]]; }
    const char *T(const char *de, const char *en) const { return german ? de : en; }
};

// one row of prompts: up to 4 glyphs sharing one label
struct Row { const char *keys[4]; int nkeys; const char *label; };

static Row MakeRow(const char *key, const char *label)
{
    Row r;
    r.keys[0] = key; r.keys[1] = r.keys[2] = r.keys[3] = NULL;
    r.nkeys = 1;
    r.label = label;
    return r;
}

// ---- the manager -------------------------------------------------------------------------------
class BallanceHD : public CKBaseManager
{
public:
    explicit BallanceHD(CKContext *context) : CKBaseManager(context, BALLANCEHD_GUID, (CKSTRING) "Ballance HD")
    {
        context->RegisterNewManager(this);
        s.Load();
        draw::Init(context);
        hd_logi("settings: frame limit / analog roll", s.frameLimit, s.analogRoll);
    }

    CKDWORD GetValidFunctionsMask() override
    {
        return CKMANAGER_FUNC_PreProcess | CKMANAGER_FUNC_PostProcess | CKMANAGER_FUNC_OnPreRender |
               CKMANAGER_FUNC_OnPostRender | CKMANAGER_FUNC_OnPostSpriteRender;
    }

    CKERROR PreProcess() override;
    CKERROR PostProcess() override;
    CKERROR OnPreRender(CKRenderContext *dev) override;
    CKERROR OnPostRender(CKRenderContext *dev) override;
    CKERROR OnPostSpriteRender(CKRenderContext *dev) override;

private:
    Settings s;
    double now = 0, last = 0;
    float dt = 0.016f;
    int frame = 0;

    // ---- objects, looked up by name once (a name lookup walks through all objects) ----
    struct Cached { const char *name; CK_CLASSID cid; CK_ID id; int retry; };
    enum { O_OREF, O_ORIENT, O_POS, O_TARGET, O_CAM, O_MENU, O_KEYSTITLE, O_LEVEL, O_INGAME, O_COUNT };
    Cached objs[O_COUNT] = {
        { "Cam_OrientRef", CKCID_3DENTITY, 0, 0 }, { "Cam_Orient", CKCID_3DENTITY, 0, 0 }, { "Cam_Pos", CKCID_3DENTITY, 0, 0 },
        { "Cam_Target", CKCID_3DENTITY, 0, 0 }, { "InGameCam", CKCID_TARGETCAMERA, 0, 0 },
        { "M_BlackScreen", CKCID_2DENTITY, 0, 0 }, { "M_Opt_Keys_Title", CKCID_2DENTITY, 0, 0 }, { "CurrentLevel", CKCID_DATAARRAY, 0, 0 },
        { "Gameplay_Ingame", CKCID_BEHAVIOR, 0, 0 } };

    CKObject *Obj(int i)
    {
        Cached &c = objs[i];
        if (c.id)
        {
            CKObject *o = m_Context->GetObject(c.id);
            if (o && Same(o->GetName(), c.name))
                return o;
            c.id = 0;
        }
        if (c.retry > frame)
            return NULL;
        CKObject *o = m_Context->GetObjectByNameAndClass((CKSTRING)c.name, c.cid, NULL);
        if (!o)
        {
            c.retry = frame + 60;      // look again in a second
            return NULL;
        }
        c.id = o->GetID();
        return o;
    }
    CK3dEntity *E3(int i) { return (CK3dEntity *)Obj(i); }

    // ---- game state ----
    bool inLevel = false, menu = false, paused = false, onControls = false, playing = false;
    CK_ID attachedCam = 0;
    bool levelStart = false, tutorial = false, levelKnown = true;

    // ---- controller ----
    hd_pad pad = {}, lastPad = {};
    int camStep = 0, camKey = 0, camDir = 0;
    double camNext = 0;
    bool Pressed(int mask) const { return mask && (pad.buttons & mask) && !(lastPad.buttons & mask); }
    bool Held(int mask) const { return mask && (pad.buttons & mask); }
    void Keys();
    void StepCamera();

    // ---- rolling ----
    CK_ID moveBB = 0, forceParam = 0, ballParam = 0, ownBB = 0, forcedBall = 0, wakeBB = 0, ownWake = 0;
    float sx = 0, sy = 0, lastAngle = 0, lastForce = 0;
    bool forceOn = false;
    int framesSinceSet = 0, idle = 0, ownRetry = 0;
    bool FindMoveBlock();
    bool OwnBlocks();
    CK_ID MirrorBlock(CK_ID gameId, const char *name);
    void Wake(CK3dEntity *ball);
    CK3dEntity *ActiveBall();
    void Push(float dx, float dz, float force);
    void StopPush();
    void KeepPush();
    void Roll();

    // ---- camera ----
    float camElev = 1.0097f, camDist = 41.3f, lift = 0;
    bool camInit = false;
    void Camera();

    // ---- widescreen ----
    CKCamera *wideCam = NULL;
    int aspectW = 4, aspectH = 3;
    float fov = 0;

    // ---- frame rate ----
    int appliedLimit = -999;
    void FrameLimit();

    // ---- prompts / panel ----
    float menuTime = 99, legendTime = 99;
    int lastKind = 0;
    bool lastMenuVisible = false;
    bool panelFocus = false;
    int panelRow = 0, repeatDir = 0;
    double repeatAt = 0;
    int fpsFrames = 0;
    double fpsStart = 0;
    float fps = 0;
    void Panel();
    void DrawPanel(float w, float h);
    void PanelRow(int r, const char *&label, const char *&value, char *num);
    void DrawPrompts(float w, float h);
    const char *Kind() const { return pad.kind == HD_PAD_PS ? "ps" : "xbox"; }
    CKTexture *Glyph(const char *key);
};

// ---- per frame: state, controller, rolling -----------------------------------------------------
CKERROR BallanceHD::PreProcess()
{
    frame++;
    now = hd_time();
    dt = last > 0 ? (float)(now - last) : 0.016f;
    if (dt <= 0 || dt > 0.1f)
        dt = 0.016f;
    last = now;
    if (s.trace)
    {
        // frame pacing for tests: every 5 s the average, the longest frame and frames over 20 ms
        static double sum = 0, worst = 0, start = 0;
        static int n = 0, slow = 0;
        sum += dt; n++;
        if (dt > worst) worst = dt;
        if (dt > 0.020f) slow++;
        if (now - start > 5.0)
        {
            if (start > 0) { hd_logi("frames: avg us / worst us", (int)(sum / n * 1e6), (int)(worst * 1e6)); hd_logi("frames: count / over 20 ms", n, slow); }
            sum = worst = 0; n = slow = 0; start = now;
        }
    }
    // game state: the level camera means a level; the menu column (also in the pause menu) means a menu
    CKRenderContext *dev = m_Context->GetPlayerRenderContext();
    CKCamera *cam = dev ? dev->GetAttachedCamera() : NULL;
    CK_ID camId = cam ? cam->GetID() : 0;
    bool levelCam = cam && Same(cam->GetName(), "InGameCam");
    if (camId != attachedCam)
    {
        attachedCam = camId;
        hd_log(cam ? cam->GetName() : "(no camera)");
        if (levelCam)
        {
            levelStart = true;           // a level begins: legend, camera, tutorial check
            levelKnown = false;
            camInit = false;
            forceOn = false;
            moveBB = 0;
        }
    }
    inLevel = levelCam;
    CK2dEntity *column = (CK2dEntity *)Obj(O_MENU);
    menu = column && column->IsVisible();
    paused = inLevel && menu;
    playing = inLevel && !menu;
    CK2dEntity *keysTitle = (CK2dEntity *)Obj(O_KEYSTITLE);
    onControls = menu && keysTitle && keysTitle->IsVisible();
    if (!onControls)
        panelFocus = false;

    if (!levelKnown && frame % 10 == 0)
    {
        CKDataArray *arr = (CKDataArray *)Obj(O_LEVEL);
        if (arr)
        {
            for (int c = 0; c < arr->GetColumnCount(); c++)
            {
                if (Same(arr->GetColumnName(c), "Level ID"))
                {
                    int id = 0;
                    arr->GetElementValue(0, c, &id);
                    tutorial = id == 1;
                    levelKnown = true;
                    hd_logi("level", id, 0);
                }
            }
        }
    }

    FrameLimit();

    // the legend comes once per level, when the game hands over the ball (its "Ball Navigation" starts)
    if (levelStart && playing && FindMoveBlock())
    {
        CKBehavior *b = (CKBehavior *)m_Context->GetObject(moveBB);
        CKBehavior *nav = b ? b->GetParent() : NULL;
        if (nav && nav->IsActive())
        {
            levelStart = false;
            legendTime = 0;
            hd_log("level start: ball control on");
        }
    }

    lastPad = pad;
    if (s.pad)
        hd_pad_poll(&pad);
    else
        pad = hd_pad();
    if (pad.kind != lastKind)
    {
        lastKind = pad.kind;
        menuTime = 0;
        hd_logi("controller kind", pad.kind, 0);
    }
    Panel();
    if (pad.kind != HD_PAD_NONE && hd_game_in_front() && !panelFocus)
        Keys();
    else
        hd_release_keys();
    if (playing && frame % 15 == 0 && s.trace)
    {
        CK3dEntity *t = E3(O_TARGET), *o = E3(O_OREF);
        if (t && o)
        {
            VxVector p, dir, up, right;
            t->GetPosition(&p, NULL);
            o->GetOrientation(&dir, &up, &right, NULL);
            char buf[160];
            wsprintf_trace(buf, p, dir, sx, sy);
            hd_log(buf);
        }
    }
    return CK_OK;
}

CKERROR BallanceHD::PostProcess()
{
    Roll();
    Camera();
    return CK_OK;
}

void BallanceHD::FrameLimit()
{
    if (appliedLimit == s.frameLimit && frame % 120 != 0)
        return;
    CKTimeManager *tm = m_Context->GetTimeManager();
    if (!tm)
        return;
    // VSync: an even 16.67 ms at 60 Hz (a 60 fps limiter gives 17.3 ms frames = a repeated frame every 0.4 s)
    CKDWORD opt = tm->GetLimitOptions();
    if (s.frameLimit == 0)
    {
        if (!(opt & CK_FRAMERATE_SYNC))
            tm->ChangeLimitOptions(CK_FRAMERATE_SYNC);
    }
    else if (s.frameLimit < 0)
    {
        if (!(opt & CK_FRAMERATE_FREE))
            tm->ChangeLimitOptions(CK_FRAMERATE_FREE);
    }
    else if (!(opt & CK_FRAMERATE_LIMIT) || appliedLimit != s.frameLimit)
    {
        tm->ChangeLimitOptions(CK_FRAMERATE_LIMIT);
        tm->SetFrameRateLimit((float)s.frameLimit);
    }
    if (appliedLimit != s.frameLimit)
        hd_logi("frame rate mode (0 vsync, -1 free, n limit)", s.frameLimit, 0);
    appliedLimit = s.frameLimit;
}

// ---- controller -> keys (menus, digital rolling, 90 degree turns, buttons) ---------------------
void BallanceHD::StepCamera()
{
    if (now < camNext)
        return;
    switch (camStep)
    {
    case 4:
        // free the arrows first, so the tap below is a fresh key press
        hd_key(K_UP, 0); hd_key(K_DOWN, 0); hd_key(K_LEFT, 0); hd_key(K_RIGHT, 0);
        hd_key(K_SHIFT, 1);
        break;
    case 3: hd_key((unsigned short)camKey, 1); break;
    case 2: hd_key((unsigned short)camKey, 0); break;
    case 1: hd_key(K_SHIFT, 0); break;
    }
    camStep--;
    camNext = now + 0.035;
}

void BallanceHD::Keys()
{
    float dz = s.deadzone / 100.0f;
    bool analog = playing && s.analogRoll;        // the left stick rolls the ball itself (Roll)
    bool freeCam = s.freeCam;

    // 90 degree turns: Shift down, arrow tapped, Shift up; without the free camera also by the right stick
    int dir = 0;
    if (!freeCam)
    {
        if (pad.rx < -dz * 2 && Absf(pad.rx) > Absf(pad.ry)) dir = -1;
        else if (pad.rx > dz * 2 && Absf(pad.rx) > Absf(pad.ry)) dir = 1;
        else if (Absf(pad.rx) >= dz) dir = camDir;
    }
    bool left = (dir == -1 && camDir != -1) || (playing && Pressed(s.Mask(A_LEFT)));
    bool right = (dir == 1 && camDir != 1) || (playing && Pressed(s.Mask(A_RIGHT)));
    camDir = dir;
    if (camStep == 0 && (left || right))
    {
        camKey = left ? K_LEFT : K_RIGHT;
        camStep = 4;
        camNext = now;
    }
    if (camStep > 0)
    {
        StepCamera();
        return;
    }

    bool stick = !analog;
    bool up = (pad.buttons & HD_DPAD_UP) || (stick && pad.ly > dz);
    bool dn = (pad.buttons & HD_DPAD_DOWN) || (stick && pad.ly < -dz);
    bool lf = (pad.buttons & HD_DPAD_LEFT) || (stick && pad.lx < -dz);
    bool rt = (pad.buttons & HD_DPAD_RIGHT) || (stick && pad.lx > dz);
    hd_key(K_UP, up && !dn);
    hd_key(K_DOWN, dn && !up);
    hd_key(K_LEFT, lf && !rt);
    hd_key(K_RIGHT, rt && !lf);

    // with the free camera the plugin raises the camera itself in a level (the game's animation would fight it)
    hd_key(K_SPACE, (!freeCam || !playing) && (Held(s.Mask(A_LIFT)) || (!freeCam && pad.ry > dz * 2)));
    hd_key(K_ENTER, Held(s.Mask(A_CONFIRM)));
    hd_key(K_ESC, Held(s.Mask(A_BACK)) || Held(s.Mask(A_PAUSE)));
    hd_key(K_Q, Held(s.Mask(A_SKIP)));
}

// ---- stepless rolling ---------------------------------------------------------------------------
// The game rolls the ball in "Ball Navigation": one "SetPhysicsForce" block per arrow key (direction relative to
// "Cam_OrientRef Frame", strength = parameter "Force" for the current ball type) plus "Physics WakeUp" - the
// physics engine lets a slow ball fall asleep, and forces on a sleeping ball do nothing. The plugin keeps its
// own instances of both blocks with its own parameters: the same kind of force through the ball's centre, in
// any direction and at any strength. The force is renewed only on a real change (the stick is smoothed).

bool BallanceHD::FindMoveBlock()
{
    if (moveBB && m_Context->GetObject(moveBB) && wakeBB && m_Context->GetObject(wakeBB))
        return true;
    moveBB = wakeBB = 0;
    if (frame % 30 != 0)
        return false;
    CKBehavior *script = (CKBehavior *)Obj(O_INGAME);
    if (!script)
        return false;
    for (int i = 0; i < script->GetSubBehaviorCount(); i++)
    {
        CKBehavior *nav = script->GetSubBehavior(i);
        if (!nav || !Same(nav->GetName(), "Ball Navigation"))
            continue;
        for (int k = 0; k < nav->GetSubBehaviorCount(); k++)
        {
            CKBehavior *b = nav->GetSubBehavior(k);
            if (!b)
                continue;
            if (Same(b->GetName(), "Physics WakeUp"))
                wakeBB = b->GetID();
            if (moveBB || !Same(b->GetName(), "SetPhysicsForce") || b->GetInputParameterCount() < 5)
                continue;
            CKParameter *f = b->GetInputParameter(4)->GetRealSource();
            CKParameter *ball = b->GetInputParameter(1)->GetRealSource();
            if (!f || !ball || !Same(f->GetName(), "Force"))
                continue;
            moveBB = b->GetID();
            forceParam = f->GetID();
            ballParam = ball->GetID();
        }
        if (moveBB && wakeBB)
        {
            hd_log("rolling: game blocks found");
            return true;
        }
    }
    moveBB = wakeBB = 0;
    return false;
}

// An own instance of one of the game's building blocks: the prototype gives the code, the layout (ins, outs,
// parameters) is mirrored from the game's block; every input parameter gets its own value.
CK_ID BallanceHD::MirrorBlock(CK_ID gameId, const char *name)
{
    CKBehavior *game = (CKBehavior *)m_Context->GetObject(gameId);
    if (!game)
        return 0;
    CKBehavior *b = (CKBehavior *)m_Context->CreateObject(CKCID_BEHAVIOR, (CKSTRING)name, CK_OBJECTCREATION_DYNAMIC);
    if (!b)
        return 0;
    b->InitFromGuid(game->GetPrototypeGuid());
    if (!b->GetFunction())
        b->SetFunction(game->GetFunction());
    if (!b->GetFunction())
    {
        hd_log("rolling: could not create a building block");
        m_Context->DestroyObject(b);
        return 0;
    }
    while (b->GetInputCount() < game->GetInputCount())
        b->CreateInput((CKSTRING) "In");
    while (b->GetOutputCount() < game->GetOutputCount())
        b->CreateOutput((CKSTRING) "Out");
    for (int i = b->GetInputParameterCount(); i < game->GetInputParameterCount(); i++)
        b->CreateInputParameter((CKSTRING) "p", game->GetInputParameter(i)->GetGUID());
    for (int i = b->GetLocalParameterCount(); i < game->GetLocalParameterCount(); i++)
        b->CreateLocalParameter((CKSTRING) "l", game->GetLocalParameter(i)->GetGUID());   // the physics handle: starts empty
    if (!b->IsUsingTarget())
        b->UseTarget(TRUE);
    CKParameterIn *t = b->GetTargetParameter();
    if (t)
        t->SetDirectSource(m_Context->CreateCKParameterLocal((CKSTRING) "", game->GetTargetParameter() ? game->GetTargetParameter()->GetGUID() : CKPGUID_3DENTITY, TRUE));
    for (int i = 0; i < b->GetInputParameterCount(); i++)
    {
        CKParameterIn *in = b->GetInputParameter(i);
        in->SetDirectSource(m_Context->CreateCKParameterLocal((CKSTRING) "", in->GetGUID(), TRUE));
    }
    return b->GetID();
}

bool BallanceHD::OwnBlocks()
{
    if (ownBB && m_Context->GetObject(ownBB) && ownWake && m_Context->GetObject(ownWake))
        return true;
    if (frame < ownRetry)
        return false;
    ownRetry = frame + 300;
    if (!(ownBB && m_Context->GetObject(ownBB)))
        ownBB = MirrorBlock(moveBB, "BallanceHD Roll");
    if (!(ownWake && m_Context->GetObject(ownWake)))
        ownWake = MirrorBlock(wakeBB, "BallanceHD WakeUp");
    if (ownBB && ownWake)
        hd_log("rolling: own blocks ready");
    return ownBB && ownWake;
}

CK3dEntity *BallanceHD::ActiveBall()
{
    CKParameter *p = (CKParameter *)m_Context->GetObject(ballParam);
    CKObject *o = p ? p->GetValueObject(TRUE) : NULL;
    return o && CKIsChildClassOf(o, CKCID_3DENTITY) ? (CK3dEntity *)o : NULL;
}

static void SetObjectParam(CKParameterIn *in, CKObject *o)
{
    CKParameter *p = in ? in->GetDirectSource() : NULL;
    if (p)
    {
        CK_ID id = o ? o->GetID() : 0;
        p->SetValue(&id, sizeof(id));
    }
}

static void Pulse(CKBehavior *b, int input, float dt)
{
    b->Activate(TRUE, FALSE);
    b->ActivateInput(input, TRUE);
    b->Execute(dt);
    b->ActivateInput(input, FALSE);
    b->Activate(FALSE, FALSE);
}

void BallanceHD::Wake(CK3dEntity *ball)
{
    CKBehavior *w = (CKBehavior *)m_Context->GetObject(ownWake);
    if (!w || !ball)
        return;
    SetObjectParam(w->GetTargetParameter(), ball);
    Pulse(w, 0, dt);
}

void BallanceHD::Push(float dx, float dz, float force)
{
    CK3dEntity *ball = ActiveBall();
    CK3dEntity *oref = E3(O_OREF);
    if (!ball || !oref || !OwnBlocks())
        return;
    if (forceOn && forcedBall != ball->GetID())
        StopPush();                      // the ball was swapped (transformer): the old one lets go first
    CKBehavior *b = (CKBehavior *)m_Context->GetObject(ownBB);
    if (forceOn)
        Pulse(b, 1, dt);                 // the old force off, the new one on
    VxVector zero(0, 0, 0), dir(dx, 0.0f, dz);
    SetObjectParam(b->GetTargetParameter(), ball);
    b->GetInputParameter(0)->GetDirectSource()->SetValue(&zero, sizeof(zero));
    SetObjectParam(b->GetInputParameter(1), ball);
    b->GetInputParameter(2)->GetDirectSource()->SetValue(&dir, sizeof(dir));
    SetObjectParam(b->GetInputParameter(3), oref);
    b->GetInputParameter(4)->GetDirectSource()->SetValue(&force, sizeof(force));
    Pulse(b, 0, dt);
    Wake(ball);
    forceOn = true;
    forcedBall = ball->GetID();
}

void BallanceHD::StopPush()
{
    if (!forceOn)
        return;
    forceOn = false;
    CKBehavior *b = (CKBehavior *)m_Context->GetObject(ownBB);
    if (b)
        Pulse(b, 1, dt);
}

// while the force is on the ball must not fall asleep
void BallanceHD::KeepPush()
{
    if (forceOn)
        Wake((CK3dEntity *)m_Context->GetObject(forcedBall));
}

void BallanceHD::Roll()
{
    if (!s.analogRoll || !playing || pad.kind == HD_PAD_NONE || panelFocus || !FindMoveBlock())
    {
        sx = sy = 0;
        StopPush();
        return;
    }
    float k = Minf(1.0f, dt * 14.0f);
    sx += (pad.lx - sx) * k;
    sy += (pad.ly - sy) * k;
    float mag = Minf(1.0f, sqrtf(sx * sx + sy * sy));
    const float dead = 0.12f;
    if (mag < dead)
    {
        if (++idle > 3)
            StopPush();
        KeepPush();
        return;
    }
    idle = 0;
    CKParameter *fp = (CKParameter *)m_Context->GetObject(forceParam);
    float base = 0.43f;
    if (fp)
        fp->GetValue(&base);           // per ball type: wood, stone, paper
    float strength = (mag - dead) / (1.0f - dead);
    strength = strength * (0.5f + 0.5f * strength);    // finer near the centre, the full game force at the edge
    float force = base * strength;
    float angle = atan2f(sx, sy);
    float da = Absf(angle - lastAngle);
    if (da > 3.14159f)
        da = 6.28318f - da;
    framesSinceSet++;
    bool change = !forceOn || da > 0.12f || Absf(force - lastForce) > base * 0.12f;
    if (!change || (forceOn && framesSinceSet < 5))
    {
        KeepPush();
        return;
    }
    // direction relative to Cam_OrientRef: +Z away from the camera, +X to the right
    Push(sx / mag, sy / mag, force);
    lastAngle = angle;
    lastForce = force;
    framesSinceSet = 0;
}

// ---- third-person camera -------------------------------------------------------------------------
// The game turns the camera by rotating Cam_OrientRef (the rolling directions follow it) and lets Cam_Orient
// follow; the camera point Cam_Pos hangs behind and above the ball, InGameCam follows it. With the free camera
// the plugin turns both frames directly with the right stick (stops the moment the stick is let go), sets
// Cam_Pos on a fixed distance and elevation and puts InGameCam exactly there (no trailing, no springing).

void BallanceHD::Camera()
{
    if (!s.freeCam || !playing || pad.kind == HD_PAD_NONE || panelFocus)
        return;
    CK3dEntity *oref = E3(O_OREF), *orient = E3(O_ORIENT), *pos = E3(O_POS), *target = E3(O_TARGET);
    CK3dEntity *cam = (CK3dEntity *)Obj(O_CAM);
    if (!oref || !orient || !pos || !target)
        return;
    if (!camInit)
    {
        camInit = true;
        camElev = 1.0097f;
        camDist = kDistances[s.camDistance];
        lift = 0;
    }
    float speed = s.camSpeed / 100.0f;
    const float dead = 0.16f;
    if (Absf(pad.rx) > dead)
    {
        float v = (Absf(pad.rx) - dead) / (1.0f - dead);
        float angle = v * v * (pad.rx < 0 ? -1.0f : 1.0f) * 2.4f * speed * dt;     // up to ~140 degrees/s
        VxVector up(0, 1, 0);
        oref->Rotate(&up, angle, NULL, FALSE);
        orient->Rotate(&up, angle, NULL, FALSE);    // both frames: the game's own 90 degree turns keep working
    }
    if (Absf(pad.ry) > dead)
    {
        float v = (Absf(pad.ry) - dead) / (1.0f - dead);
        // stick up looks up: the camera goes down and sees further ahead; InvertY swaps it
        v = v * v * (pad.ry < 0 ? 1.0f : -1.0f) * (s.invertY ? -1.0f : 1.0f);
        camElev = Maxf(0.25f, Minf(1.40f, camElev + v * 1.1f * speed * dt));
    }
    if (Pressed(s.Mask(A_DISTANCE)))
    {
        s.camDistance = (s.camDistance + 1) % 4;
        hd_ini_set_int("Controller", "CameraDistance", s.camDistance);
    }
    bool raise = Held(s.Mask(A_LIFT));
    lift += ((raise ? 1.0f : 0.0f) - lift) * Minf(1.0f, dt * 6.0f);
    camDist += (kDistances[s.camDistance] - camDist) * Minf(1.0f, dt * 5.0f);
    float e = camElev + (1.38f - camElev) * lift;
    float d = camDist * (1.0f + 0.3f * lift);
    VxVector lp(0.0f, d * sinf(e), -d * cosf(e));
    pos->SetPosition(&lp, orient, FALSE);
    if (cam)
    {
        VxVector p;
        pos->GetPosition(&p, NULL);
        cam->SetPosition(&p, NULL, FALSE);
    }
}

// ---- widescreen --------------------------------------------------------------------------------
// Ballance's cameras are 4:3: on a wider screen the engine draws a 4:3 picture with black bars. Just for the
// rendering the camera gets the screen's aspect ratio and a horizontal field of view that keeps the 4:3
// vertical view (Hor+); afterwards its own values go back.

CKERROR BallanceHD::OnPreRender(CKRenderContext *dev)
{
    wideCam = NULL;
    if (!s.wide || !dev)
        return CK_OK;
    CKCamera *cam = dev->GetAttachedCamera();
    int w = dev->GetWidth(), h = dev->GetHeight();
    if (!cam || w <= 0 || h <= 0)
        return CK_OK;
    cam->GetAspectRatio(aspectW, aspectH);
    fov = cam->GetFov();
    if (aspectW <= 0 || aspectH <= 0 || aspectW * h == aspectH * w)
        return CK_OK;
    wideCam = cam;
    float original = (float)aspectW / (float)aspectH, screen = (float)w / (float)h;
    cam->SetAspectRatio(w, h);
    cam->SetFov(2.0f * atanf(tanf(fov * 0.5f) * screen / original));
    // the engine has worked out viewport and projection with the old values already: once more, now
    dev->ForceCameraSettingsUpdate();
    dev->PrepareCameras(CK_RENDER_USECURRENTSETTINGS);
    return CK_OK;
}

CKERROR BallanceHD::OnPostRender(CKRenderContext *dev)
{
    if (wideCam)
    {
        wideCam->SetAspectRatio(aspectW, aspectH);
        wideCam->SetFov(fov);
        wideCam = NULL;
    }
    return CK_OK;
}

// ---- controller settings next to Options - Controls ---------------------------------------------
// Y gives the panel the controller (the game gets no keys meanwhile), up / down picks a row, left / right / A
// changes it, B gives the controller back to the game menu. Changes are saved at once.

static const int kPanelRows = 5 + A_COUNT;

void BallanceHD::Panel()
{
    if (!onControls || pad.kind == HD_PAD_NONE)
        return;
    if (!panelFocus && Pressed(HD_Y))
    {
        panelFocus = true;
        panelRow = 0;
        repeatDir = -1;
        hd_release_keys();
        return;
    }
    if (!panelFocus)
        return;
    if (Pressed(HD_B))
    {
        panelFocus = false;
        return;
    }
    int dir = 0;
    if ((pad.buttons & HD_DPAD_UP) || pad.ly > 0.5f) dir = 1;
    else if ((pad.buttons & HD_DPAD_DOWN) || pad.ly < -0.5f) dir = 2;
    else if ((pad.buttons & HD_DPAD_LEFT) || pad.lx < -0.5f) dir = 3;
    else if ((pad.buttons & HD_DPAD_RIGHT) || pad.lx > 0.5f) dir = 4;
    int step = 0;
    if (dir == 0)
        repeatDir = 0;
    else if (repeatDir != -1 && (dir != repeatDir || now >= repeatAt))
    {
        repeatAt = now + (dir == repeatDir ? 0.12 : 0.4);
        repeatDir = dir;
        if (dir == 1) panelRow = (panelRow + kPanelRows - 1) % kPanelRows;
        else if (dir == 2) panelRow = (panelRow + 1) % kPanelRows;
        else step = dir == 4 ? 1 : -1;
    }
    if (Pressed(HD_A))
        step = 1;
    if (step == 0)
        return;
    int r = panelRow;
    if (r == 0) s.analogRoll = !s.analogRoll;
    else if (r == 1) s.freeCam = !s.freeCam;
    else if (r == 2) s.invertY = !s.invertY;
    else if (r == 3)
    {
        s.camSpeed += step * 25;
        if (s.camSpeed < 25) s.camSpeed = 25;
        if (s.camSpeed > 300) s.camSpeed = 300;
    }
    else if (r == 4) s.camDistance = (s.camDistance + step + 4) % 4;
    else
    {
        int a = r - 5;
        s.button[a] = (s.button[a] + step + 13) % 13;
    }
    s.Save();
}

CKTexture *BallanceHD::Glyph(const char *key)
{
    if (!key || !*key)
        return NULL;
    char path[96] = "BallanceHD\\glyphs\\";
    int n = 18;
    for (const char *k = Kind(); *k && n < 80; k++) path[n++] = *k;
    path[n++] = '_';
    for (const char *k = key; *k && n < 88; k++) path[n++] = *k;
    path[n++] = '.'; path[n++] = 't'; path[n++] = 'g'; path[n++] = 'a'; path[n] = 0;
    return draw::Texture(path);
}

// ---- overlay --------------------------------------------------------------------------------------
CKERROR BallanceHD::OnPostSpriteRender(CKRenderContext *dev)
{
    if (!dev)
        return CK_OK;
    float w = (float)dev->GetWidth(), h = (float)dev->GetHeight();
    if (w <= 0 || h <= 0)
        return CK_OK;
    // FPS for the optional counter
    fpsFrames++;
    if (now - fpsStart >= 0.5)
    {
        fps = (float)(fpsFrames / (now - fpsStart));
        fpsFrames = 0;
        fpsStart = now;
    }
    // timers: the menu bar again with every menu / controller change and each confirm / back in the menus;
    // the legend once at the start of a level
    if (menu != lastMenuVisible)
    {
        lastMenuVisible = menu;
        if (menu)
            menuTime = 0;
    }
    if (menu && (Pressed(s.Mask(A_CONFIRM)) || Pressed(s.Mask(A_BACK))))
        menuTime = 0;
    menuTime += dt;
    legendTime += dt;

    bool showPrompts = s.prompts && pad.kind != HD_PAD_NONE;
    bool any = s.showFps || (showPrompts && ((menu && menuTime < 12) || (playing && legendTime < 12) || onControls));
    if (!any)
        return CK_OK;
    draw::Begin(dev);
    if (showPrompts)
    {
        if (onControls)
            DrawPanel(w, h);
        else
            DrawPrompts(w, h);
    }
    if (s.showFps)
    {
        char buf[24];
        IntText((int)(fps + 0.5f), buf, " FPS");
        float fs = h * 0.022f;
        float tw = draw::TextWidth(buf, fs);
        draw::Rect(w - tw - h * 0.03f, h * 0.008f, w - h * 0.008f, h * 0.008f + fs * 1.4f, draw::Argb(0, 0, 0, 0.5f));
        draw::Text(w - tw - h * 0.019f, h * 0.008f + fs * 0.15f, fs, draw::Argb(1, 1, 1, 1), buf);
    }
    draw::End();
    return CK_OK;
}

static float Fade(float t) { return t < 10.0f ? 1.0f : Maxf(0.0f, 1.0f - (t - 10.0f) / 2.0f); }

static float RowWidth(const Row *r, int n, float g, float gap, float fs)
{
    float w = 0;
    for (int i = 0; i < n; i++)
    {
        w += r[i].nkeys * g + (r[i].nkeys - 1) * gap * 0.25f + gap * 0.4f + draw::TextWidth(r[i].label, fs);
        if (i + 1 < n)
            w += gap * 1.6f;
    }
    return w;
}

static unsigned int WithAlpha(unsigned int argb, float a)
{
    return (argb & 0x00FFFFFF) | ((unsigned int)(a * 255.0f) << 24);
}

void BallanceHD::DrawPrompts(float w, float h)
{
    float g = h * 0.05f, margin = h * 0.028f;
    unsigned int text = draw::Argb(0.96f, 0.93f, 0.88f, 1);
    if (menu && menuTime < 12)
    {
        float a = Fade(menuTime);
        // navigation: bottom right, between the game's menu column and the screen edge, scaled to fit there
        Row r[3] = { MakeRow("dpad", s.T("Auswählen", "Select")),
                     MakeRow(s.Glyph(A_CONFIRM), s.T("Bestätigen", "Confirm")),
                     MakeRow(s.Glyph(A_BACK), s.T("Zurück", "Back")) };
        float menuRight = w * 0.5f + h * 0.362f;
        float avail = w - margin - menuRight - margin;
        float rw = RowWidth(r, 3, g, g * 0.5f, g * 0.52f) + g * 0.6f;
        float sc = rw > avail && avail > 0 ? avail / rw : 1.0f;
        float gs = g * sc, gap = gs * 0.5f, fs = gs * 0.52f;
        rw = RowWidth(r, 3, gs, gap, fs);
        float x = w - margin - gap * 0.6f - rw;
        float y = (paused ? h * 0.80f : h - margin) - gs;      // in a level the lives sit bottom right
        draw::Rect(x - gap * 0.6f, y - gap * 0.4f, x + rw + gap * 0.6f, y + gs + gap * 0.4f, draw::Argb(0.05f, 0.06f, 0.10f, 0.62f * a));
        for (int i = 0; i < 3; i++)
        {
            draw::Image(Glyph(r[i].keys[0]), x, y, x + gs, y + gs, draw::Argb(1, 1, 1, a));
            x += gs + gap * 0.4f;
            draw::Text(x, y + (gs - fs * 1.2f) * 0.5f, fs, WithAlpha(text, a), r[i].label);
            x += draw::TextWidth(r[i].label, fs) + gap * 1.6f;
        }
    }
    if (playing && legendTime < 12)
    {
        float a = Fade(legendTime);
        Row r[8];
        int n = 0;
        r[n++] = MakeRow("lstick", s.analogRoll ? s.T("Rollen (stufenlos)", "Roll (stepless)") : s.T("Rollen", "Roll"));
        if (s.freeCam)
            r[n++] = MakeRow("rstick", s.T("Kamera drehen / neigen", "Turn / tilt the camera"));
        {
            Row t = MakeRow(NULL, s.T("Kamera 90° drehen", "Turn camera 90°"));
            t.nkeys = 0;
            if (!s.freeCam) t.keys[t.nkeys++] = "rstick_lr";
            if (*s.Glyph(A_LEFT)) t.keys[t.nkeys++] = s.Glyph(A_LEFT);
            if (*s.Glyph(A_RIGHT)) t.keys[t.nkeys++] = s.Glyph(A_RIGHT);
            if (t.nkeys) r[n++] = t;
        }
        {
            Row t = MakeRow(NULL, s.T("Kamera anheben", "Raise camera"));
            t.nkeys = 0;
            if (!s.freeCam) t.keys[t.nkeys++] = "rstick_up";
            if (*s.Glyph(A_LIFT)) t.keys[t.nkeys++] = s.Glyph(A_LIFT);
            if (t.nkeys) r[n++] = t;
        }
        if (s.freeCam && *s.Glyph(A_DISTANCE))
            r[n++] = MakeRow(s.Glyph(A_DISTANCE), s.T("Kameraabstand", "Camera distance"));
        if (*s.Glyph(A_PAUSE))
            r[n++] = MakeRow(s.Glyph(A_PAUSE), s.T("Pause", "Pause"));
        if (tutorial && *s.Glyph(A_SKIP))
            r[n++] = MakeRow(s.Glyph(A_SKIP), s.T("Einführung überspringen", "Skip tutorial"));
        float lg = g * 0.9f, gap = lg * 0.5f, fs = lg * 0.52f;
        float colW = 0;
        for (int i = 0; i < n; i++)
            colW = Maxf(colW, RowWidth(&r[i], 1, lg, gap, fs));
        float x0 = margin, y = margin;
        draw::Rect(x0 - gap * 0.4f, y - gap * 0.4f, x0 + colW + gap * 0.4f, y + n * (lg + gap * 0.35f) + gap * 0.1f,
                   draw::Argb(0.05f, 0.06f, 0.10f, 0.62f * a));
        for (int i = 0; i < n; i++)
        {
            float x = x0;
            for (int k = 0; k < r[i].nkeys; k++)
            {
                draw::Image(Glyph(r[i].keys[k]), x, y, x + lg, y + lg, draw::Argb(1, 1, 1, a));
                x += lg + (k + 1 < r[i].nkeys ? gap * 0.25f : 0.0f);
            }
            x += gap * 0.4f;
            draw::Text(x, y + (lg - fs * 1.2f) * 0.5f, fs, WithAlpha(text, a), r[i].label);
            y += lg + gap * 0.35f;
        }
    }
}

void BallanceHD::PanelRow(int r, const char *&label, const char *&value, char *num)
{
    if (r == 0) { label = s.T("Kugel stufenlos rollen", "Stepless rolling"); value = s.analogRoll ? s.T("An", "On") : s.T("Aus", "Off"); }
    else if (r == 1) { label = s.T("Freie Kamera (rechter Stick)", "Free camera (right stick)"); value = s.freeCam ? s.T("An", "On") : s.T("Aus", "Off"); }
    else if (r == 2) { label = s.T("Kamera-Neigung umkehren", "Invert camera tilt"); value = s.invertY ? s.T("An", "On") : s.T("Aus", "Off"); }
    else if (r == 3) { label = s.T("Kamera-Tempo", "Camera speed"); IntText(s.camSpeed, num, " %"); value = num; }
    else if (r == 4) { label = s.T("Kameraabstand", "Camera distance"); value = s.german ? kDistDe[s.camDistance] : kDistEn[s.camDistance]; }
    else
    {
        int a = r - 5;
        label = s.german ? kActionDe[a] : kActionEn[a];
        value = s.button[a] == 12 ? s.T("nicht belegt", "unassigned") : kButtons[s.button[a]];
    }
}

void BallanceHD::DrawPanel(float w, float h)
{
    float x0 = w * 0.5f + h * 0.375f, x1 = w - h * 0.03f;
    if (x1 - x0 < h * 0.5f)
        x0 = x1 - h * 0.5f;            // narrow (4:3) screens: overlap the game's column a little
    float y0 = h * 0.12f, rowH = h * 0.045f, fs = h * 0.024f, pd = h * 0.02f;
    float gsz = rowH * 0.8f;
    const char *footer = panelFocus ? s.T("Fertig  (Steuerkreuz: wählen / ändern)", "Done  (d-pad: select / change)")
                                    : s.T("Controller-Einstellungen bearbeiten", "Edit controller settings");
    // the longest row (and the footer) must fit: the text gets smaller on narrow space
    float need = 0;
    for (int r = 0; r < kPanelRows; r++)
    {
        const char *label, *value;
        char num[16];
        PanelRow(r, label, value, num);
        float rw = draw::TextWidth(label, fs) + draw::TextWidth("  <  ", fs) + draw::TextWidth(value, fs) + draw::TextWidth("  >", fs);
        if (r >= 5)
            rw += gsz + pd * 0.5f;
        need = Maxf(need, rw);
    }
    need = Maxf(need, gsz + pd * 0.5f + draw::TextWidth(footer, fs)) + 2 * pd;
    if (need > x1 - x0)
        fs *= (x1 - x0) / need;
    float y1 = y0 + h * 0.085f + kPanelRows * rowH + h * 0.07f;
    unsigned int white = draw::Argb(0.96f, 0.93f, 0.88f, 1), dim = draw::Argb(0.75f, 0.72f, 0.68f, 1);
    unsigned int warm = draw::Argb(0.97f, 0.68f, 0.38f, 1);
    draw::Rect(x0, y0, x1, y1, draw::Argb(0.05f, 0.06f, 0.10f, panelFocus ? 0.85f : 0.6f));
    draw::Text(x0 + pd, y0 + pd * 0.8f, h * 0.024f * 1.35f, warm, "Controller");
    float y = y0 + h * 0.085f;
    for (int r = 0; r < kPanelRows; r++)
    {
        bool sel = panelFocus && r == panelRow;
        if (sel)
            draw::Rect(x0 + pd * 0.5f, y - rowH * 0.1f, x1 - pd * 0.5f, y + rowH * 0.9f, draw::Argb(0.55f, 0.28f, 0.12f, 0.85f));
        float tx = x0 + pd;
        const char *label, *value;
        char num[16];
        PanelRow(r, label, value, num);
        if (r >= 5)
        {
            draw::Image(Glyph(s.Glyph(r - 5)), tx, y, tx + gsz, y + gsz, draw::Argb(1, 1, 1, 1));
            tx += gsz + pd * 0.5f;
        }
        float ty = y + (gsz - fs * 1.2f) * 0.5f;
        draw::Text(tx, ty, fs, white, label);
        float vx = x1 - pd - draw::TextWidth(value, fs);
        if (sel)
        {
            float arrow = draw::TextWidth(">", fs), space = draw::TextWidth("  ", fs);
            draw::Text(x1 - pd - arrow, ty, fs, warm, ">");
            vx -= arrow + space;
            draw::Text(vx - space - draw::TextWidth("<", fs), ty, fs, warm, "<");
        }
        draw::Text(vx, ty, fs, sel ? warm : dim, value);
        y += rowH;
    }
    float hx = x0 + pd, hy = y1 - h * 0.055f;
    draw::Image(Glyph(panelFocus ? "b" : "y"), hx, hy, hx + gsz, hy + gsz, draw::Argb(1, 1, 1, 1));
    hx += gsz + pd * 0.5f;
    draw::Text(hx, hy + (gsz - fs * 1.2f) * 0.5f, fs, dim, footer);
}

// ---- plugin entry points -------------------------------------------------------------------------
static CKERROR InitInstance(CKContext *context)
{
    hd_init();
    new BallanceHD(context);
    return CK_OK;
}

static CKERROR ExitInstance(CKContext *context)
{
    delete (BallanceHD *)context->GetManagerByGuid(BALLANCEHD_GUID);
    hd_shutdown();
    return CK_OK;
}

static CKPluginInfo *g_info = NULL;

PLUGIN_EXPORT int CKGetPluginInfoCount()
{
    return 1;
}

PLUGIN_EXPORT CKPluginInfo *CKGetPluginInfo(int index)
{
    if (!g_info)
    {
        g_info = new CKPluginInfo();
        g_info->m_GUID = BALLANCEHD_GUID;
        g_info->m_Extension = "";
        g_info->m_Description = "Ballance HD";
        g_info->m_Author = "CompiWare / BockwurstWilly";
        g_info->m_Summary = "Widescreen, controller and HD features for Ballance";
        g_info->m_Version = 0x020000;
        g_info->m_InitInstanceFct = InitInstance;
        g_info->m_ExitInstanceFct = ExitInstance;
        g_info->m_Type = CKPLUGIN_MANAGER_DLL;
    }
    return g_info;
}

// ---- no Visual C++ runtime: memory from the process heap, floats without the CRT --------------------
extern "C" {
__declspec(dllimport) void *__stdcall GetProcessHeap();
__declspec(dllimport) void *__stdcall HeapAlloc(void *, unsigned long, size_t);
__declspec(dllimport) int __stdcall HeapFree(void *, unsigned long, void *);
}
void *operator new(size_t n) { return HeapAlloc(GetProcessHeap(), 8 /* HEAP_ZERO_MEMORY */, n ? n : 1); }
void *operator new[](size_t n) { return HeapAlloc(GetProcessHeap(), 8, n ? n : 1); }
void operator delete(void *p) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }
void operator delete[](void *p) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }
void operator delete(void *p, size_t) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }
void operator delete[](void *p, size_t) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }

extern "C" int __stdcall DllMain(void *, unsigned long, void *) { return 1; }
extern "C" int _fltused = 0;
