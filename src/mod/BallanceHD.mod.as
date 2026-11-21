[bml.mod id="compiware.ballancehd"
         name="Ballance HD"
         version="1.3.0"
         author="CompiWare / BockwurstWilly"
         description="Widescreen without bars; controller: stepless rolling, third-person camera, button prompts, settings in Options - Controls"
         bml="0.3.14"]
class BallanceHD {
    // ---- files in ModLoader\BallanceHD (the game's working directory is Bin) ----
    //   lang_de   German texts      hints_off   no button prompts      wide_off   no widescreen fix
    string root = "..\\ModLoader\\BallanceHD\\";

    // ---- settings (shown in Options - Controls; the launcher edits the same file) ----
    array<string> buttonNames = { "A / Cross", "B / Circle", "X / Square", "Y / Triangle", "LB / L1", "RB / R1", "LT / L2", "RT / R2", "Start / Options", "Back / Share", "L3", "R3", "-" };
    array<string> buttonGlyphs = { "a", "b", "x", "y", "lb", "rb", "lt", "rt", "start", "back", "l3", "r3", "" };
    array<int> buttonMasks = { 0x1000, 0x2000, 0x4000, 0x8000, 0x0100, 0x0200, 0x10000, 0x20000, 0x0010, 0x0020, 0x0040, 0x0080, 0 };
    array<string> actionKeys = { "Confirm", "Back", "Pause", "CameraLift", "CameraLeft", "CameraRight", "SkipTutorial", "Respawn", "CameraDistance" };
    array<string> actionDefaults = { "A / Cross", "B / Circle", "Start / Options", "Y / Triangle", "LB / L1", "RB / R1", "X / Square", "-", "Back / Share" };
    array<string> actionDe = { "Bestätigen", "Zurück", "Pause-Menü", "Kamera anheben (halten)", "Kamera 90° links", "Kamera 90° rechts", "Einführung überspringen", "Kugel neu setzen", "Kameraabstand wechseln" };
    array<string> actionEn = { "Confirm", "Back", "Pause menu", "Raise camera (hold)", "Camera 90° left", "Camera 90° right", "Skip tutorial", "Respawn the ball", "Change camera distance" };
    array<string> actionButton(9);
    bool analogRoll = true;
    bool freeCamera = true;
    bool invertY = false;
    int cameraSpeed = 100;
    int cameraDistance = 1;          // index into distances
    array<float> distances = { 30.0f, 41.3f, 55.0f, 70.0f };
    array<string> distanceDe = { "Nah", "Normal", "Weit", "Sehr weit" };
    array<string> distanceEn = { "Near", "Normal", "Far", "Very far" };

    // ---- state ----
    bool wide = true;
    bool hints = true;
    bool german = false;
    int frame = 0;
    bool levelKnown = true;
    bool tutorialLevel = false;
    float dt = 0.016f;

    // ---- shared memory with the launcher (see SharedPad in the launcher) ----
    NativePointer shm;
    bool shmOpen = false;
    int shmRetry = 0;
    int padSeq = -1, padQuiet = 999;
    float lx = 0, ly = 0, rx = 0, ry = 0;
    int padButtons = 0, lastPadButtons = 0;
    string pad = "";

    // ---- prompts ----
    int promptState = -1;
    int promptCam = 0;
    float promptTime = 0.0f;
    string lastPad = "";
    dictionary textures;            // glyph id -> texture CK_ID
    dictionary failed;

    // ---- stepless rolling ----
    float sx = 0, sy = 0;           // smoothed stick
    float baseForce = 0.43f;
    bool forceActive = false;
    float lastAngle = 0, lastMag = 0;
    int framesSinceSet = 0, idleFrames = 0;

    // ---- camera ----
    float camElev = 1.0097f;        // the game's default: 35 up, 22 back
    float camDist = 41.3f;
    float liftAmount = 0.0f;
    float yawRate = 0.0f;
    bool camInit = false;

    // ---- settings panel in Options - Controls ----
    bool onControls = false;
    bool panelFocus = false;
    int panelRow = 0;
    int repeatDir = 0;
    float repeatAt = 0.0f;
    float clock = 0.0f;

    // ---- widescreen ----
    int lastCam = 0, lastW = 0, lastH = 0;

    string T(const string &in de, const string &in en) { return german ? de : en; }
    int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }
    float Max(float a, float b) { return a > b ? a : b; }
    float Min(float a, float b) { return a < b ? a : b; }
    float Abs(float a) { return a < 0 ? -a : a; }

    // ---- lifecycle -------------------------------------------------------------------------------

    void OnLoad(const BML::ModContext &in ctx) {
        ReadMarkers();
        BML::Config@ config = ctx.BorrowConfig();
        if (config !is null) {
            config.SetCategoryComment("Controller", "Controller (HD Launcher)");
            config.SetCategoryComment("Buttons", T("Tastenbelegung des Controllers", "Controller button layout"));
            Prop(config, "Controller", "AnalogRoll").SetDefaultBoolean(true);
            Prop(config, "Controller", "FreeCamera").SetDefaultBoolean(true);
            Prop(config, "Controller", "InvertY").SetDefaultBoolean(false);
            Prop(config, "Controller", "CameraSpeed").SetDefaultInteger(100);
            Prop(config, "Controller", "CameraDistance").SetDefaultInteger(1);
            for (uint i = 0; i < actionKeys.length(); i++) {
                BML::ConfigProperty@ p = Prop(config, "Buttons", actionKeys[i]);
                p.SetDefaultString(actionDefaults[i]);
                p.SetComment(german ? actionDe[i] : actionEn[i]);
                p.SetChoices(buttonNames);
                p.SetEditor(BML::CONFIG_EDITOR_CHOICE);
            }
        }
        ReadConfig(ctx);
        ctx.LogInfo("Ballance HD loaded (german=" + german + ", analog=" + analogRoll + ", freecam=" + freeCamera + ")");
    }

    BML::ConfigProperty@ Prop(BML::Config@ config, const string &in cat, const string &in key) {
        return config.GetProperty(cat, key);
    }

    void ReadConfig(const BML::ModContext &in ctx) {
        BML::Config@ config = ctx.BorrowConfig();
        if (config is null)
            return;
        analogRoll = Prop(config, "Controller", "AnalogRoll").GetBoolean(true);
        freeCamera = Prop(config, "Controller", "FreeCamera").GetBoolean(true);
        invertY = Prop(config, "Controller", "InvertY").GetBoolean(false);
        cameraSpeed = Clamp(Prop(config, "Controller", "CameraSpeed").GetInteger(100), 25, 300);
        cameraDistance = Clamp(Prop(config, "Controller", "CameraDistance").GetInteger(1), 0, 3);
        for (uint i = 0; i < actionKeys.length(); i++) {
            string v = Prop(config, "Buttons", actionKeys[i]).GetString(actionDefaults[i]);
            actionButton[i] = buttonNames.find(v) >= 0 ? v : actionDefaults[i];
        }
        // 1.2.0 had "respawn" on View / Share, which is the camera distance now
        if (actionButton[7] == actionButton[8] && actionButton[7] != "-")
            actionButton[7] = "-";
    }

    void SaveConfig(const BML::ModContext &in ctx) {
        BML::Config@ config = ctx.BorrowConfig();
        if (config is null)
            return;
        Prop(config, "Controller", "AnalogRoll").SetBoolean(analogRoll);
        Prop(config, "Controller", "FreeCamera").SetBoolean(freeCamera);
        Prop(config, "Controller", "InvertY").SetBoolean(invertY);
        Prop(config, "Controller", "CameraSpeed").SetInteger(cameraSpeed);
        Prop(config, "Controller", "CameraDistance").SetInteger(cameraDistance);
        for (uint i = 0; i < actionKeys.length(); i++)
            Prop(config, "Buttons", actionKeys[i]).SetString(actionButton[i]);
    }

    void OnModifyConfig(const BML::ModContext &in ctx, const BML::ConfigEvent &in event) {
        ReadConfig(ctx);
    }

    void ReadMarkers() {
        german = BML::Path::Exists(root + "lang_de");
        hints = !BML::Path::Exists(root + "hints_off");
        wide = !BML::Path::Exists(root + "wide_off");
    }

    void OnGameEvent(const BML::ModContext &in ctx, BML::GameEvent event) {
        if (event == BML::GAME_EVENT_START_LEVEL) {
            levelKnown = false;
            tutorialLevel = false;
            forceActive = false;
            camInit = false;
            promptState = -1;
        }
    }

    void OnProcess(const BML::ModContext &in ctx) {
        frame++;
        dt = ImGui::GetIO().DeltaTime;
        if (dt <= 0 || dt > 0.1f)
            dt = 0.016f;
        clock += dt;
        if (frame % 60 == 0)
            ReadMarkers();
        ReadPad();
        if (!levelKnown && frame % 10 == 0)
            ReadLevel(ctx);
        if (wide)
            FixAspect(ctx);
        bool inLevel = ctx.IsInLevel;
        bool playing = inLevel && !ctx.IsPaused;
        ControlsPanel(ctx, !inLevel || ctx.IsPaused);
        Roll(ctx, playing && !panelFocus);
        Camera(ctx, playing && !panelFocus);
        WriteState(inLevel, ctx.IsPaused);
        if (hints && pad != "" && !onControls)
            DrawPrompts(ctx);
        lastPadButtons = padButtons;
    }

    // ---- shared memory ---------------------------------------------------------------------------

    void OpenShared() {
        if (shmOpen || shmRetry-- > 0)
            return;
        shmRetry = 120;      // the launcher may start later: try again every ~2 seconds
        DynLibrary@ k32 = DynLibrary();
        if (!k32.Load("kernel32.dll"))
            return;
        NativePointer openMap = k32.FindSymbol("OpenFileMappingA");
        NativePointer mapView = k32.FindSymbol("MapViewOfFile");
        if (openMap.ToUInt() == 0 || mapView.ToUInt() == 0)
            return;
        DynCall@ dc = DynCall();
        dc.Reset();
        dc.Mode(DC_CALL_C_X86_WIN32_STD);
        dc.ArgInt(0x0006);                          // FILE_MAP_READ | FILE_MAP_WRITE
        dc.ArgBool(false);
        string name = "Local\\BallanceHD_Pad";
        dc.ArgString(name);
        NativePointer h = dc.CallPointer(openMap);
        if (h.ToUInt() == 0)
            return;
        dc.Reset();
        dc.Mode(DC_CALL_C_X86_WIN32_STD);
        dc.ArgPointer(h);
        dc.ArgInt(0x0006);
        dc.ArgInt(0);
        dc.ArgInt(0);
        dc.ArgInt(128);
        NativePointer v = dc.CallPointer(mapView);
        if (v.ToUInt() == 0)
            return;
        shm = v;
        shmOpen = true;
    }

    int ReadInt(int at) { int v = 0; (shm + at).ReadInt(v); return v; }
    float ReadFloat(int at) { float v = 0; (shm + at).ReadFloat(v); return v; }

    void ReadPad() {
        OpenShared();
        int kind = 0;
        if (shmOpen) {
            int seq = ReadInt(0);
            float a = ReadFloat(4), b = ReadFloat(8), c = ReadFloat(12), d = ReadFloat(16);
            int buttons = ReadInt(20);
            kind = ReadInt(24);
            if (seq == ReadInt(28)) {
                padQuiet = seq == padSeq ? padQuiet + 1 : 0;
                padSeq = seq;
                lx = a; ly = b; rx = c; ry = d;
                padButtons = buttons;
            }
        }
        if (!shmOpen || padQuiet > 30) {
            lx = ly = rx = ry = 0;
            padButtons = 0;
            kind = 0;
        }
        pad = kind == 1 ? "xbox" : kind == 2 ? "ps" : "";
    }

    void WriteState(bool inLevel, bool paused) {
        if (!shmOpen)
            return;
        (shm + 64).WriteInt(frame);
        int state = (inLevel ? 1 : 0) | (paused ? 2 : 0) | (panelFocus ? 4 : 0) | (freeCamera ? 8 : 0) | (analogRoll ? 16 : 0);
        (shm + 68).WriteInt(state);
        for (uint i = 0; i < actionKeys.length(); i++)
            (shm + 72 + 4 * i).WriteInt(Mask(actionButton[i]));
    }

    bool PadOk() { return shmOpen && padQuiet <= 30 && padSeq >= 0; }

    int Mask(const string &in button) {
        int i = buttonNames.find(button);
        return i >= 0 ? buttonMasks[i] : 0;
    }

    bool Pressed(int mask) { return mask != 0 && (padButtons & mask) != 0 && (lastPadButtons & mask) == 0; }
    bool ActionPressed(int action) { return Pressed(Mask(actionButton[action])); }
    bool ActionHeld(int action) { int m = Mask(actionButton[action]); return m != 0 && (padButtons & m) != 0; }

    // ---- level -----------------------------------------------------------------------------------

    void ReadLevel(const BML::ModContext &in ctx) {
        CKDataArray@ arr = ctx.BorrowDataArrayByName("CurrentLevel");
        if (arr is null)
            return;
        int col = BML::CK::FindColumn(arr, "Level ID");
        if (col < 0)
            return;
        levelKnown = true;
        tutorialLevel = BML::CK::GetInt(arr, 0, col, 0) == 1;
        ReadBaseForce(ctx);
    }

    CK3dEntity@ Ball(const BML::ModContext &in ctx) {
        CK3dEntity@ f = ctx.Borrow3dEntityByName("Ball_Pos_Frame");
        return f is null ? null : BML::CK::BorrowParent(f);
    }

    // ---- stepless rolling ------------------------------------------------------------------------
    // In a level the launcher leaves the left stick to the mod (no arrow keys): the mod pushes the ball with
    // the same kind of physics force the game uses for the arrow keys, in the stick's direction (camera space)
    // and with up to the game's own strength. The stick is smoothed and the force is renewed only when the
    // direction or strength really changes - every renewal rebuilds a physics block in the engine.

    void ReadBaseForce(const BML::ModContext &in ctx) {
        CKBehavior@ s = ctx.BorrowScriptByName("Gameplay_Ingame");
        if (s is null)
            return;
        for (int i = 0; i < s.GetSubBehaviorCount(); i++) {
            CKBehavior@ nav = s.GetSubBehavior(i);
            if (nav is null || nav.GetName() != "Ball Navigation")
                continue;
            for (int k = 0; k < nav.GetSubBehaviorCount(); k++) {
                CKBehavior@ b = nav.GetSubBehavior(k);
                if (b is null || b.GetName() != "SetPhysicsForce" || b.GetInputParameterCount() < 5)
                    continue;
                CKParameterIn@ pin = b.GetInputParameter(4);
                CKParameter@ src = pin is null ? null : pin.GetRealSource();
                if (src !is null && src.GetName() == "Force") {
                    string v;
                    src.GetStringValue(v);
                    float f = parseFloat(v);
                    if (f > 0.01f)
                        baseForce = f;     // per ball type: wood, stone, paper
                    return;
                }
            }
        }
    }

    void StopForce(const BML::ModContext &in ctx) {
        if (!forceActive)
            return;
        CK3dEntity@ ball = Ball(ctx);
        if (ball !is null)
            BML::Physics::ClearForce(ball);
        forceActive = false;
    }

    void Roll(const BML::ModContext &in ctx, bool playing) {
        if (!analogRoll || !playing || !PadOk()) {
            sx = sy = 0;
            StopForce(ctx);
            return;
        }
        float k = Min(1.0f, dt * 14.0f);
        sx += (lx - sx) * k;
        sy += (ly - sy) * k;
        float mag = Min(1.0f, sqrt(sx * sx + sy * sy));
        const float dead = 0.14f;
        if (mag < dead) {
            if (++idleFrames > 3)
                StopForce(ctx);
            return;
        }
        idleFrames = 0;
        if (frame % 30 == 0)
            ReadBaseForce(ctx);       // the ball type may have changed
        float strength = (mag - dead) / (1.0f - dead);
        strength = strength * (0.4f + 0.6f * strength);     // finer near the centre, full force at the edge
        float f = baseForce * strength;
        float angle = atan2(sx, sy);
        float da = Abs(angle - lastAngle);
        if (da > 3.14159f)
            da = 6.28318f - da;
        framesSinceSet++;
        bool change = !forceActive || da > 0.12f || Abs(f - lastMag) > baseForce * 0.12f;
        if (!change || (forceActive && framesSinceSet < 5))
            return;
        CK3dEntity@ ball = Ball(ctx);
        CK3dEntity@ oref = ctx.Borrow3dEntityByName("Cam_OrientRef");
        if (ball is null || oref is null)
            return;
        // camera space: +Z away from the camera, +X to the right
        if (BML::Physics::SetForce(ball, VxVector(0, 0, 0), null, VxVector(sx / mag, 0, sy / mag), oref, f)) {
            forceActive = true;
            lastAngle = angle;
            lastMag = f;
            framesSinceSet = 0;
        }
    }

    // ---- third-person camera ---------------------------------------------------------------------
    // The game turns the camera by rotating Cam_OrientRef (the rolling directions follow it) and lets
    // Cam_Orient follow; the camera position Cam_Pos hangs behind and above the ball, and InGameCam follows
    // Cam_Pos with a delay. The mod owns Cam_Pos: a fixed distance (steps with the View button), an elevation
    // the right stick tilts, and its own raise. Turning rotates both frames and InGameCam around the ball, so
    // the delayed camera stays on its circle instead of cutting across it (that looked like zooming).

    void Camera(const BML::ModContext &in ctx, bool playing) {
        if (!freeCamera || !playing || !PadOk()) {
            yawRate = 0;
            return;
        }
        CK3dEntity@ oref = ctx.Borrow3dEntityByName("Cam_OrientRef");
        CK3dEntity@ orient = ctx.Borrow3dEntityByName("Cam_Orient");
        CK3dEntity@ pos = ctx.Borrow3dEntityByName("Cam_Pos");
        CK3dEntity@ target = ctx.Borrow3dEntityByName("Cam_Target");
        CK3dEntity@ cam = ctx.BorrowTargetCameraByName("InGameCam");
        if (oref is null || orient is null || pos is null || target is null)
            return;
        if (!camInit) {
            camInit = true;
            camElev = 1.0097f;
            camDist = distances[cameraDistance];
            liftAmount = 0;
        }
        float speed = cameraSpeed / 100.0f;
        const float dead = 0.16f;

        // turning: the stick sets a target speed, the camera eases into it (no jerks)
        float want = 0;
        if (Abs(rx) > dead) {
            float v = (Abs(rx) - dead) / (1.0f - dead);
            want = v * v * (rx < 0 ? -1.0f : 1.0f) * 2.4f * speed;      // up to ~140°/s
        }
        yawRate += (want - yawRate) * Min(1.0f, dt * 10.0f);
        float angle = yawRate * dt;
        if (Abs(angle) > 0.00001f) {
            VxVector o, f0, f1;
            oref.GetPosition(o, null);
            oref.Transform(f0, VxVector(0, 0, 1), null);
            VxVector up(0, 1, 0);
            oref.Rotate(up, angle, null, false);
            orient.Rotate(up, angle, null, false);
            oref.Transform(f1, VxVector(0, 0, 1), null);
            if (cam !is null) {
                // the same turn for the delayed camera, around the ball (2D rotation in the ground plane)
                float ax = f0.x - o.x, az = f0.z - o.z, bx = f1.x - o.x, bz = f1.z - o.z;
                float n = ax * ax + az * az;
                if (n > 0.000001f) {
                    float c = (bx * ax + bz * az) / n, s = (bz * ax - bx * az) / n;
                    VxVector t, p;
                    target.GetPosition(t, null);
                    cam.GetPosition(p, null);
                    float vx = p.x - t.x, vz = p.z - t.z;
                    p.x = t.x + vx * c - vz * s;
                    p.z = t.z + vx * s + vz * c;
                    cam.SetPosition(p, null, false);
                }
            }
        }

        // tilting: stick up looks up (the camera goes down and sees further ahead); InvertY swaps it
        if (Abs(ry) > dead) {
            float v = (Abs(ry) - dead) / (1.0f - dead);
            v = v * v * (ry < 0 ? 1.0f : -1.0f) * (invertY ? -1.0f : 1.0f);
            camElev = Max(0.25f, Min(1.40f, camElev + v * 1.1f * speed * dt));
        }

        // distance steps and raising
        if (ActionPressed(8)) {
            cameraDistance = (cameraDistance + 1) % 4;
            SaveConfig(ctx);
        }
        bool lift = ActionHeld(3);
        liftAmount += ((lift ? 1.0f : 0.0f) - liftAmount) * Min(1.0f, dt * 6.0f);
        camDist += (distances[cameraDistance] - camDist) * Min(1.0f, dt * 5.0f);

        float e = camElev + (1.38f - camElev) * liftAmount;
        float d = camDist * (1.0f + 0.3f * liftAmount);
        VxVector lp(0, d * sin(e), -d * cos(e));
        pos.SetPosition(lp, orient, false);

        // a fixed distance like a third-person camera: the game lets InGameCam trail behind (it falls back
        // when the ball speeds up and cuts corners when tilting); keep its direction, fix its distance
        if (cam !is null) {
            VxVector t, p;
            target.GetPosition(t, null);
            cam.GetPosition(p, null);
            float vx = p.x - t.x, vy = p.y - t.y, vz = p.z - t.z;
            float len = sqrt(vx * vx + vy * vy + vz * vz);
            if (len > 0.001f && Abs(len - d) > 0.01f) {
                float k = d / len;
                cam.SetPosition(VxVector(t.x + vx * k, t.y + vy * k, t.z + vz * k), null, false);
            }
        }
    }

    // ---- settings panel in Options - Controls ----------------------------------------------------
    // Next to the game's key list; Y gives it the controller (the launcher then sends the game nothing),
    // up / down picks a row, left / right / A changes it, B gives the controller back to the game menu.

    int PanelRows() { return 5 + int(actionKeys.length()); }

    string RowLabel(int r) {
        if (r == 0) return T("Kugel stufenlos rollen", "Stepless rolling");
        if (r == 1) return T("Freie Kamera (rechter Stick)", "Free camera (right stick)");
        if (r == 2) return T("Kamera-Neigung umkehren", "Invert camera tilt");
        if (r == 3) return T("Kamera-Tempo", "Camera speed");
        if (r == 4) return T("Kameraabstand", "Camera distance");
        int a = r - 5;
        return german ? actionDe[a] : actionEn[a];
    }

    string RowValue(int r) {
        if (r == 0) return analogRoll ? T("An", "On") : T("Aus", "Off");
        if (r == 1) return freeCamera ? T("An", "On") : T("Aus", "Off");
        if (r == 2) return invertY ? T("An", "On") : T("Aus", "Off");
        if (r == 3) return "" + cameraSpeed + " %";
        if (r == 4) return german ? distanceDe[cameraDistance] : distanceEn[cameraDistance];
        string b = actionButton[r - 5];
        return b == "-" ? T("nicht belegt", "unassigned") : b;
    }

    void ChangeRow(const BML::ModContext &in ctx, int r, int step) {
        if (r == 0) analogRoll = !analogRoll;
        else if (r == 1) freeCamera = !freeCamera;
        else if (r == 2) invertY = !invertY;
        else if (r == 3) cameraSpeed = Clamp(cameraSpeed + step * 25, 25, 300);
        else if (r == 4) cameraDistance = (cameraDistance + step + 4) % 4;
        else {
            int a = r - 5;
            int i = buttonNames.find(actionButton[a]);
            i = (i + step + int(buttonNames.length())) % int(buttonNames.length());
            actionButton[a] = buttonNames[i];
        }
        SaveConfig(ctx);
    }

    void ControlsPanel(const BML::ModContext &in ctx, bool inMenu) {
        CK2dEntity@ title = ctx.Borrow2dEntityByName("M_Opt_Keys_Title");
        onControls = inMenu && title !is null && BML::CK::IsVisible(title);
        if (!onControls) {
            panelFocus = false;
            return;
        }
        if (pad != "") {
            if (!panelFocus && Pressed(0x8000)) {             // Y
                panelFocus = true;
                panelRow = 0;
            } else if (panelFocus && Pressed(0x2000)) {        // B
                panelFocus = false;
            } else if (panelFocus) {
                int dir = 0;
                if ((padButtons & 0x0001) != 0 || ly > 0.5f) dir = 1;
                else if ((padButtons & 0x0002) != 0 || ly < -0.5f) dir = 2;
                else if ((padButtons & 0x0004) != 0 || lx < -0.5f) dir = 3;
                else if ((padButtons & 0x0008) != 0 || lx > 0.5f) dir = 4;
                if (dir == 0) {
                    repeatDir = 0;
                } else if (dir != repeatDir || clock >= repeatAt) {
                    repeatAt = clock + (dir == repeatDir ? 0.12f : 0.4f);
                    repeatDir = dir;
                    if (dir == 1) panelRow = (panelRow + PanelRows() - 1) % PanelRows();
                    else if (dir == 2) panelRow = (panelRow + 1) % PanelRows();
                    else ChangeRow(ctx, panelRow, dir == 4 ? 1 : -1);
                }
                if (Pressed(0x1000))                            // A
                    ChangeRow(ctx, panelRow, 1);
            }
        }
        DrawPanel(ctx);
    }

    void DrawPanel(const BML::ModContext &in ctx) {
        ImVec2 screen = ImGui::GetIO().DisplaySize;
        if (screen.x <= 0 || screen.y <= 0)
            return;
        ImDrawList@ dl = ImGui::GetForegroundDrawList();
        ImFont@ font = ImGui::GetFont();
        float h = screen.y;
        float x0 = screen.x * 0.5f + h * 0.375f;
        float x1 = screen.x - h * 0.03f;
        if (x1 - x0 < h * 0.45f)
            x0 = x1 - h * 0.45f;          // narrow (4:3) screens: overlap the game's column a little
        float y0 = h * 0.12f;
        float rowH = h * 0.045f;
        float fs = h * 0.024f;
        int rows = PanelRows();
        float y1 = y0 + h * 0.085f + rows * rowH + h * 0.07f;
        float a = panelFocus ? 0.82f : 0.6f;
        dl.AddRectFilled(ImVec2(x0, y0), ImVec2(x1, y1), ImGui::ColorConvertFloat4ToU32(ImVec4(0.05f, 0.06f, 0.10f, a)), 12.0f);
        uint white = ImGui::ColorConvertFloat4ToU32(ImVec4(0.96f, 0.93f, 0.88f, 1.0f));
        uint dim = ImGui::ColorConvertFloat4ToU32(ImVec4(0.75f, 0.72f, 0.68f, 1.0f));
        uint warm = ImGui::ColorConvertFloat4ToU32(ImVec4(0.97f, 0.68f, 0.38f, 1.0f));
        uint sel = ImGui::ColorConvertFloat4ToU32(ImVec4(0.55f, 0.28f, 0.12f, 0.85f));
        float pad_ = h * 0.02f;
        ImGui::PushFont(font, fs * 1.35f);
        dl.AddText(ImVec2(x0 + pad_, y0 + pad_), warm, "Controller");
        ImGui::PopFont();
        ImGui::PushFont(font, fs);
        float y = y0 + h * 0.085f;
        for (int r = 0; r < rows; r++) {
            if (panelFocus && r == panelRow)
                dl.AddRectFilled(ImVec2(x0 + pad_ * 0.5f, y - rowH * 0.1f), ImVec2(x1 - pad_ * 0.5f, y + rowH * 0.9f), sel, 6.0f);
            float g = rowH * 0.8f;
            float tx = x0 + pad_;
            if (r >= 5 && pad != "") {
                CKTexture@ tex = Texture(ctx, buttonGlyphs[buttonNames.find(actionButton[r - 5])]);
                if (tex !is null)
                    dl.AddImage(tex, ImVec2(tx, y), ImVec2(tx + g, y + g), ImVec2(0, 0), ImVec2(1, 1), 0xffffffff);
                tx += g + pad_ * 0.5f;
            }
            dl.AddText(ImVec2(tx, y + (g - ImGui::GetFontSize()) * 0.5f), white, RowLabel(r));
            string v = RowValue(r);
            if (panelFocus && r == panelRow)
                v = "<  " + v + "  >";
            float vw = ImGui::CalcTextSize(v).x;
            dl.AddText(ImVec2(x1 - pad_ - vw, y + (g - ImGui::GetFontSize()) * 0.5f), r == panelRow && panelFocus ? warm : dim, v);
            y += rowH;
        }
        // hint line with glyphs
        float g = rowH * 0.8f;
        float hx = x0 + pad_;
        float hy = y1 - h * 0.055f;
        string key = panelFocus ? "b" : "y";
        if (pad != "") {
            CKTexture@ tex = Texture(ctx, key);
            if (tex !is null)
                dl.AddImage(tex, ImVec2(hx, hy), ImVec2(hx + g, hy + g), ImVec2(0, 0), ImVec2(1, 1), 0xffffffff);
            hx += g + pad_ * 0.5f;
        }
        dl.AddText(ImVec2(hx, hy + (g - ImGui::GetFontSize()) * 0.5f), dim,
                   panelFocus ? T("Fertig  ·  Steuerkreuz: wählen / ändern", "Done  ·  d-pad: select / change")
                              : T("Controller-Einstellungen bearbeiten", "Edit controller settings"));
        ImGui::PopFont();
    }

    // ---- widescreen ------------------------------------------------------------------------------
    // Ballance's cameras are 4:3; the engine then draws a 4:3 picture with black bars on wider screens.
    // Giving the active camera the screen's aspect ratio fills the screen; BML+'s WidescreenFix keeps the
    // vertical field of view of the original (Hor+), so nothing is cut off at the top and bottom.

    void FixAspect(const BML::ModContext &in ctx) {
        CKRenderContext@ rc = ctx.BorrowRenderContext();
        if (rc is null)
            return;
        int w = rc.GetWidth();
        int h = rc.GetHeight();
        if (w <= 0 || h <= 0)
            return;
        CKCamera@ cam = rc.GetAttachedCamera();
        if (cam is null)
            return;
        int aw = 0, ah = 0;
        cam.GetAspectRatio(aw, ah);
        if (aw * h == ah * w)
            return;
        cam.SetAspectRatio(w, h);
        int id = cam.GetID();
        if (id != lastCam || w != lastW || h != lastH) {
            ctx.LogInfo("Camera " + BML::CK::GetName(cam) + " " + aw + ":" + ah + " -> " + w + ":" + h);
            lastCam = id;
            lastW = w;
            lastH = h;
        }
    }

    // ---- button prompts --------------------------------------------------------------------------
    // Shown for 10 seconds, then faded out over 2: in the menus (again after each confirm / back), in the
    // pause menu and at the start of a level - and again whenever the controller changes.

    string Glyph(int action) {
        int i = buttonNames.find(actionButton[action]);
        return i >= 0 ? buttonGlyphs[i] : "";
    }

    CKTexture@ Texture(const BML::ModContext &in ctx, const string &in key) {
        if (key == "")
            return null;
        string id = pad + "_" + key;
        if (failed.exists(id))
            return null;
        CKContext@ ck = ctx.BorrowCKContext();
        if (ck is null)
            return null;
        CK_ID tid = 0;
        if (textures.get(id, tid)) {
            CKTexture@ cached = cast<CKTexture>(ck.GetObject(tid));
            if (cached !is null)
                return cached;
        }
        CKTexture@ tex = cast<CKTexture>(ck.CreateObject(CKCID_TEXTURE, "BallanceHD_Glyph_" + id));
        if (tex is null || !tex.LoadImage(root + "glyphs\\" + id + ".tga")) {
            ctx.LogWarn("Glyph " + id + " could not be loaded");
            failed.set(id, true);
            return null;
        }
        textures.set(id, tex.GetID());
        return tex;
    }

    float RowWidth(const array<string> &in keys, const array<string> &in labels, float g, float gap) {
        float w = 0.0f;
        for (uint i = 0; i < keys.length(); i++) {
            array<string>@ parts = keys[i].split("+");
            w += parts.length() * g + (parts.length() - 1) * gap * 0.25f + gap * 0.4f + ImGui::CalcTextSize(labels[i]).x;
            if (i + 1 < keys.length())
                w += gap * 1.6f;
        }
        return w;
    }

    void DrawRow(const BML::ModContext &in ctx, ImDrawList@ dl, float x, float y, const array<string> &in keys,
                 const array<string> &in labels, float g, float gap, float alpha) {
        uint white = ImGui::ColorConvertFloat4ToU32(ImVec4(1.0f, 1.0f, 1.0f, alpha));
        uint text = ImGui::ColorConvertFloat4ToU32(ImVec4(0.96f, 0.93f, 0.88f, alpha));
        float th = ImGui::GetFontSize();
        for (uint i = 0; i < keys.length(); i++) {
            array<string>@ parts = keys[i].split("+");
            for (uint k = 0; k < parts.length(); k++) {
                CKTexture@ tex = Texture(ctx, parts[k]);
                if (tex !is null)
                    dl.AddImage(tex, ImVec2(x, y), ImVec2(x + g, y + g), ImVec2(0, 0), ImVec2(1, 1), white);
                x += g + (k + 1 < parts.length() ? gap * 0.25f : 0.0f);
            }
            x += gap * 0.4f;
            dl.AddText(ImVec2(x, y + (g - th) * 0.5f), text, labels[i]);
            x += ImGui::CalcTextSize(labels[i]).x + gap * 1.6f;
        }
    }

    void Panel(ImDrawList@ dl, float x0, float y0, float x1, float y1, float alpha) {
        dl.AddRectFilled(ImVec2(x0, y0), ImVec2(x1, y1), ImGui::ColorConvertFloat4ToU32(ImVec4(0.05f, 0.06f, 0.10f, 0.62f * alpha)), 10.0f);
    }

    void DrawPrompts(const BML::ModContext &in ctx) {
        ImGuiIO@ io = ImGui::GetIO();
        ImVec2 screen = io.DisplaySize;
        if (screen.x <= 0 || screen.y <= 0)
            return;
        bool inLevel = ctx.IsInLevel;
        bool paused = ctx.IsPaused;
        int state = !inLevel ? 0 : paused ? 1 : 2;
        // the timer starts when a menu or level camera takes over (the game loads for a while before that)
        int camId = 0;
        CKRenderContext@ rc = ctx.BorrowRenderContext();
        if (rc !is null && rc.GetAttachedCamera() !is null)
            camId = rc.GetAttachedCamera().GetID();
        if (camId != promptCam) {
            promptCam = camId;
            promptState = -1;
        }
        if (state != promptState || pad != lastPad) {
            promptState = state;
            lastPad = pad;
            promptTime = 0.0f;
        }
        if (state != 2 && (ActionPressed(0) || ActionPressed(1)))
            promptTime = 0.0f;
        promptTime += dt;
        float alpha = promptTime < 10.0f ? 1.0f : 1.0f - (promptTime - 10.0f) / 2.0f;
        if (alpha <= 0.0f)
            return;

        ImDrawList@ dl = ImGui::GetForegroundDrawList();
        ImFont@ font = ImGui::GetFont();
        float g = screen.y * 0.05f;
        float margin = screen.y * 0.028f;

        if (state != 2) {
            // navigation: bottom right, between the menu column and the screen edge, scaled to fit there
            array<string> keys = { "dpad", Glyph(0), Glyph(1) };
            array<string> labels = { T("Auswählen", "Select"), T("Bestätigen", "Confirm"), T("Zurück", "Back") };
            float menuRight = screen.x * 0.5f + screen.y * 0.362f;
            float avail = screen.x - margin - menuRight - margin;
            ImGui::PushFont(font, g * 0.52f);
            float w = RowWidth(keys, labels, g, g * 0.5f) + g * 0.6f;
            ImGui::PopFont();
            float s = w > avail ? avail / w : 1.0f;
            float gs = g * s;
            float gap = gs * 0.5f;
            ImGui::PushFont(font, gs * 0.52f);
            float rw = RowWidth(keys, labels, gs, gap);
            float x = screen.x - margin - gap * 0.6f - rw;
            float y = (state == 1 ? screen.y * 0.80f : screen.y - margin) - gs;
            Panel(dl, x - gap * 0.6f, y - gap * 0.4f, x + rw + gap * 0.6f, y + gs + gap * 0.4f, alpha);
            DrawRow(ctx, dl, x, y, keys, labels, gs, gap, alpha);
            ImGui::PopFont();
        }

        if (state != 0) {
            array<string> keys;
            array<string> labels;
            keys.insertLast("lstick");
            labels.insertLast(analogRoll ? T("Rollen (stufenlos)", "Roll (stepless)") : T("Rollen", "Roll"));
            if (freeCamera) {
                keys.insertLast("rstick");
                labels.insertLast(T("Kamera drehen / neigen", "Turn / tilt the camera"));
            }
            string turn = Glyph(4) + (Glyph(5) != "" ? (Glyph(4) != "" ? "+" : "") + Glyph(5) : "");
            if (!freeCamera)
                turn = "rstick_lr" + (turn != "" ? "+" + turn : "");
            if (turn != "") {
                keys.insertLast(turn);
                labels.insertLast(T("Kamera 90° drehen", "Turn camera 90°"));
            }
            string lift = Glyph(3);
            if (!freeCamera)
                lift = "rstick_up" + (lift != "" ? "+" + lift : "");
            if (lift != "") {
                keys.insertLast(lift);
                labels.insertLast(T("Kamera anheben", "Raise camera"));
            }
            if (freeCamera && Glyph(8) != "") {
                keys.insertLast(Glyph(8));
                labels.insertLast(T("Kameraabstand", "Camera distance"));
            }
            if (Glyph(2) != "") {
                keys.insertLast(Glyph(2));
                labels.insertLast(T("Pause", "Pause"));
            }
            if (tutorialLevel && state == 2 && Glyph(6) != "") {
                keys.insertLast(Glyph(6));
                labels.insertLast(T("Einführung überspringen", "Skip tutorial"));
            }
            float lg = g * 0.9f;
            float gap = lg * 0.5f;
            ImGui::PushFont(font, lg * 0.52f);
            float colW = 0.0f;
            for (uint i = 0; i < keys.length(); i++) {
                array<string> k = { keys[i] };
                array<string> l = { labels[i] };
                colW = Max(colW, RowWidth(k, l, lg, gap));
            }
            float x = margin;
            float y = margin;
            Panel(dl, x - gap * 0.4f, y - gap * 0.4f, x + colW + gap * 0.4f, y + keys.length() * (lg + gap * 0.35f) + gap * 0.1f, alpha);
            for (uint i = 0; i < keys.length(); i++) {
                array<string> k = { keys[i] };
                array<string> l = { labels[i] };
                DrawRow(ctx, dl, x, y, k, l, lg, gap, alpha);
                y += lg + gap * 0.35f;
            }
            ImGui::PopFont();
        }
    }
}
