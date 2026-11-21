[bml.mod id="compiware.ballancehd"
         name="Ballance HD"
         version="1.2.0"
         author="CompiWare / BockwurstWilly"
         description="Widescreen without bars, controller: stepless rolling, free camera, button prompts, button layout"
         bml="0.3.14"]
class BallanceHD {
    // ---- files in ModLoader\BallanceHD (the game's working directory is Bin) ----
    //   pad.bin             live controller state from the HD launcher (see PadLink in the launcher)
    //   pad_xbox / pad_ps   connected controller (fallback when pad.bin is missing)
    //   lang_de             German texts      hints_off   no button prompts      wide_off   no widescreen fix
    string root = "..\\ModLoader\\BallanceHD\\";

    // settings (Options - Mods - Ballance HD; the launcher edits the same file)
    array<string> buttonNames = { "A / Cross", "B / Circle", "X / Square", "Y / Triangle", "LB / L1", "RB / R1", "LT / L2", "RT / R2", "Start / Options", "Back / Share", "L3", "R3", "-" };
    array<string> buttonGlyphs = { "a", "b", "x", "y", "lb", "rb", "lt", "rt", "start", "back", "l3", "r3", "" };
    array<string> actionKeys = { "Confirm", "Back", "Pause", "CameraLift", "CameraLeft", "CameraRight", "SkipTutorial", "Respawn" };
    array<string> actionDefaults = { "A / Cross", "B / Circle", "Start / Options", "Y / Triangle", "LB / L1", "RB / R1", "X / Square", "Back / Share" };
    array<string> actionButton(8);
    bool analogRoll = true;
    bool freeCamera = true;
    bool invertY = false;
    int cameraSpeed = 100;

    // state
    bool wide = true;
    bool hints = true;
    bool german = false;
    int pollFrames = 0;
    bool levelKnown = true;
    bool tutorialLevel = false;

    // pad link
    NativeBuffer@ padBuf = NativeBuffer(32);
    int padSeq = -1;
    int padStale = 999;
    float lx = 0, ly = 0, rx = 0, ry = 0;
    int padButtons = 0, lastPadButtons = 0;
    string pad = "";              // "xbox", "ps" or ""

    // prompts
    int promptState = -1;         // 0 main menu, 1 paused, 2 playing
    float promptTime = 0.0f;
    string lastPad = "";
    int promptCam = 0;
    dictionary failed;

    // analog rolling
    CK_ID forceParamId = 0;
    float baseForce = 0.43f;
    bool forceMuted = false;
    bool forceActive = false;
    VxVector lastDir;
    float lastMag = 0.0f;
    int framesSinceSet = 0;
    int idleFrames = 0;

    // widescreen
    int lastCam = 0, lastW = 0, lastH = 0;

    string T(const string &in de, const string &in en) { return german ? de : en; }

    // ---- lifecycle -------------------------------------------------------------------------------

    void OnLoad(const BML::ModContext &in ctx) {
        ReadMarkers();
        BML::Config@ config = ctx.BorrowConfig();
        if (config !is null) {
            config.SetCategoryComment("Controller", "Controller (HD Launcher)");
            config.SetCategoryComment("Buttons", T("Tastenbelegung des Controllers", "Controller button layout"));
            BoolProp(config, "Controller", "AnalogRoll", true, T("Kugel stufenlos mit dem linken Stick rollen", "Roll the ball steplessly with the left stick"));
            BoolProp(config, "Controller", "FreeCamera", true, T("Kamera frei mit dem rechten Stick drehen und neigen", "Turn and tilt the camera freely with the right stick"));
            BoolProp(config, "Controller", "InvertY", false, T("Kamera-Neigung umkehren", "Invert camera tilt"));
            BML::ConfigProperty@ sp = config.GetProperty("Controller", "CameraSpeed");
            if (sp !is null) {
                sp.SetDefaultInteger(100);
                sp.SetComment(T("Kamera-Tempo in Prozent (25-300)", "Camera speed in percent (25-300)"));
            }
            array<string> de = { "Bestätigen", "Zurück", "Pause-Menü", "Kamera anheben (halten)", "Kamera 90° links", "Kamera 90° rechts", "Einführung überspringen", "Kugel neu setzen" };
            array<string> en = { "Confirm", "Back", "Pause menu", "Raise camera (hold)", "Camera 90° left", "Camera 90° right", "Skip tutorial", "Respawn the ball" };
            for (uint i = 0; i < actionKeys.length(); i++) {
                BML::ConfigProperty@ p = config.GetProperty("Buttons", actionKeys[i]);
                if (p is null)
                    continue;
                p.SetDefaultString(actionDefaults[i]);
                p.SetComment(german ? de[i] : en[i]);
                p.SetChoices(buttonNames);
                p.SetEditor(BML::CONFIG_EDITOR_CHOICE);
            }
        }
        ReadConfig(ctx);
        ctx.LogInfo("Ballance HD loaded (german=" + german + ", hints=" + hints + ", wide=" + wide + ", analog=" + analogRoll + ", freecam=" + freeCamera + ")");
    }

    void BoolProp(BML::Config@ config, const string &in cat, const string &in key, bool def, const string &in comment) {
        BML::ConfigProperty@ p = config.GetProperty(cat, key);
        if (p is null)
            return;
        p.SetDefaultBoolean(def);
        p.SetComment(comment);
    }

    void ReadConfig(const BML::ModContext &in ctx) {
        BML::Config@ config = ctx.BorrowConfig();
        if (config is null)
            return;
        BML::ConfigProperty@ p;
        @p = config.GetProperty("Controller", "AnalogRoll");   if (p !is null) analogRoll = p.GetBoolean(true);
        @p = config.GetProperty("Controller", "FreeCamera");   if (p !is null) freeCamera = p.GetBoolean(true);
        @p = config.GetProperty("Controller", "InvertY");      if (p !is null) invertY = p.GetBoolean(false);
        @p = config.GetProperty("Controller", "CameraSpeed");  if (p !is null) cameraSpeed = Clamp(p.GetInteger(100), 25, 300);
        for (uint i = 0; i < actionKeys.length(); i++) {
            @p = config.GetProperty("Buttons", actionKeys[i]);
            string v = p is null ? actionDefaults[i] : p.GetString(actionDefaults[i]);
            actionButton[i] = buttonNames.find(v) >= 0 ? v : actionDefaults[i];
        }
    }

    void OnModifyConfig(const BML::ModContext &in ctx, const BML::ConfigEvent &in event) {
        ReadConfig(ctx);
        if (!analogRoll)
            StopForce(ctx);
    }

    void ReadMarkers() {
        german = BML::Path::Exists(root + "lang_de");
        hints = !BML::Path::Exists(root + "hints_off");
        wide = !BML::Path::Exists(root + "wide_off");
    }

    void OnGameEvent(const BML::ModContext &in ctx, BML::GameEvent event) {
        if (event == BML::GAME_EVENT_START_LEVEL) {
            levelKnown = false;          // the level's data is not readable yet at this event: see ReadLevel
            tutorialLevel = false;
            forceParamId = 0;            // Gameplay_Ingame is loaded with every level
            forceMuted = false;
            forceActive = false;
            promptState = -1;
        }
    }

    void OnProcess(const BML::ModContext &in ctx) {
        if (++pollFrames >= 30) {
            pollFrames = 0;
            ReadMarkers();
        }
        ReadPad();
        if (!levelKnown && pollFrames % 10 == 0)
            ReadLevel(ctx);
        if (wide)
            FixAspect(ctx);
        bool playing = ctx.IsInLevel && !ctx.IsPaused;
        AnalogRoll(ctx, playing);
        FreeCamera(ctx, playing);
        if (hints && pad != "")
            DrawPrompts(ctx);
        lastPadButtons = padButtons;
    }

    int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }
    float Max(float a, float b) { return a > b ? a : b; }
    float Min(float a, float b) { return a < b ? a : b; }
    float Abs(float a) { return a < 0 ? -a : a; }

    // ---- pad link --------------------------------------------------------------------------------

    void ReadPad() {
        padBuf.Reset();            // Load fills the buffer from its cursor
        uint got = padBuf.Load(root + "pad.bin", 32);
        bool ok = got == 32;
        int seq = 0, seq2 = -1, kind = 0;
        if (ok) {
            padBuf.Reset();
            padBuf.ReadInt(seq);
            float a = 0, b = 0, c = 0, d = 0;
            int buttons = 0;
            padBuf.ReadFloat(a);
            padBuf.ReadFloat(b);
            padBuf.ReadFloat(c);
            padBuf.ReadFloat(d);
            padBuf.ReadInt(buttons);
            padBuf.ReadInt(kind);
            padBuf.ReadInt(seq2);
            if (seq == seq2) {
                padStale = seq == padSeq ? padStale + 1 : 0;
                padSeq = seq;
                lx = a; ly = b; rx = c; ry = d;
                padButtons = buttons;
            }
        }
        if (!ok || padStale > 20) {
            // no launcher (game started without it, or gone): no analog input
            lx = ly = rx = ry = 0;
            padButtons = 0;
            kind = 0;
        }
        if (kind == 1)
            pad = "xbox";
        else if (kind == 2)
            pad = "ps";
        else if (BML::Path::Exists(root + "pad_xbox"))
            pad = "xbox";
        else if (BML::Path::Exists(root + "pad_ps"))
            pad = "ps";
        else
            pad = "";
    }

    bool PadFresh() { return padStale <= 20 && padSeq >= 0; }

    int Mask(const string &in button) {
        int i = buttonNames.find(button);
        array<int> masks = { 0x1000, 0x2000, 0x4000, 0x8000, 0x0100, 0x0200, 0x10000, 0x20000, 0x0010, 0x0020, 0x0040, 0x0080, 0 };
        return i >= 0 ? masks[i] : 0;
    }

    bool ButtonPressed(int action) {
        int m = Mask(actionButton[action]);
        return m != 0 && (padButtons & m) != 0 && (lastPadButtons & m) == 0;
    }

    // ---- level -----------------------------------------------------------------------------------

    // the level number from the game's "CurrentLevel" array (written while the level loads)
    void ReadLevel(const BML::ModContext &in ctx) {
        CKDataArray@ arr = ctx.BorrowDataArrayByName("CurrentLevel");
        if (arr is null)
            return;
        int col = BML::CK::FindColumn(arr, "Level ID");
        if (col < 0)
            return;
        levelKnown = true;
        tutorialLevel = BML::CK::GetInt(arr, 0, col, 0) == 1;
    }

    CK3dEntity@ Ball(const BML::ModContext &in ctx) {
        CK3dEntity@ f = ctx.Borrow3dEntityByName("Ball_Pos_Frame");
        return f is null ? null : BML::CK::BorrowParent(f);
    }

    // ---- analog rolling --------------------------------------------------------------------------
    // The game pushes the ball with a fixed force while an arrow key is held ("Ball Navigation": four
    // SetPhysicsForce blocks sharing the parameter "Force", read when the key goes down). While the left
    // stick is out of its dead zone the mod sets that parameter to 0 - the launcher presses the arrow a
    // moment later, for the menus - and pushes the ball itself, in the stick's direction and strength.

    CKParameter@ ForceParam(const BML::ModContext &in ctx) {
        CKContext@ ck = ctx.BorrowCKContext();
        if (forceParamId != 0) {
            CKParameter@ p = cast<CKParameter>(ck.GetObject(forceParamId));
            if (p !is null)
                return p;
            forceParamId = 0;
        }
        CKBehavior@ s = ctx.BorrowScriptByName("Gameplay_Ingame");
        if (s is null)
            return null;
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
                    forceParamId = src.GetID();
                    return src;
                }
            }
        }
        return null;
    }

    float GetFloat(CKParameter@ p) {
        string v;
        p.GetStringValue(v);
        return parseFloat(v);
    }

    void SetFloat(CKParameter@ p, float v) {
        p.SetStringValue("" + v);
    }

    void MuteGameForce(const BML::ModContext &in ctx, bool mute) {
        CKParameter@ p = ForceParam(ctx);
        if (p is null)
            return;
        float v = GetFloat(p);
        if (mute) {
            if (v > 0.0001f)
                baseForce = v;            // the game sets it per ball type (wood, stone, paper)
            if (v != 0.0f)
                SetFloat(p, 0.0f);
            forceMuted = true;
        } else if (forceMuted) {
            if (v == 0.0f)
                SetFloat(p, baseForce);
            forceMuted = false;
        }
    }

    void StopForce(const BML::ModContext &in ctx) {
        if (forceActive) {
            CK3dEntity@ ball = Ball(ctx);
            if (ball !is null)
                BML::Physics::ClearForce(ball);
            forceActive = false;
        }
        if (forceMuted)
            MuteGameForce(ctx, false);
    }

    void AnalogRoll(const BML::ModContext &in ctx, bool playing) {
        if (!analogRoll || !playing || !PadFresh()) {
            StopForce(ctx);
            return;
        }
        float mag = Min(1.0f, sqrt(lx * lx + ly * ly));
        const float dead = 0.15f;
        if (mag < dead) {
            if (++idleFrames > 2)
                StopForce(ctx);
            return;
        }
        idleFrames = 0;
        MuteGameForce(ctx, true);
        CK3dEntity@ ball = Ball(ctx);
        CK3dEntity@ oref = ctx.Borrow3dEntityByName("Cam_OrientRef");
        if (ball is null || oref is null)
            return;
        float strength = (mag - dead) / (1.0f - dead);
        strength = strength * (0.35f + 0.65f * strength);       // finer control near the centre
        // camera space: +Z away from the camera, +X to the right
        VxVector dir(lx / mag, 0.0f, ly / mag);
        float f = baseForce * strength;
        framesSinceSet++;
        float dot = dir.x * lastDir.x + dir.z * lastDir.z;
        if (!forceActive || dot < 0.9986f || Abs(f - lastMag) > baseForce * 0.06f || framesSinceSet > 30) {
            if (BML::Physics::SetForce(ball, VxVector(0, 0, 0), null, dir, oref, f)) {
                forceActive = true;
                lastDir = dir;
                lastMag = f;
                framesSinceSet = 0;
            }
        }
    }

    // ---- free camera -----------------------------------------------------------------------------
    // The game turns the camera in 90° steps by rotating Cam_OrientRef (the rolling directions follow it) and
    // lets Cam_Orient follow; the camera itself (Cam_Pos) hangs behind and above. Rotating both frames turns
    // camera and controls together; moving Cam_Pos on its arc tilts the view.

    void FreeCamera(const BML::ModContext &in ctx, bool playing) {
        if (!freeCamera || !playing || !PadFresh())
            return;
        float dt = ImGui::GetIO().DeltaTime;
        if (dt <= 0 || dt > 0.1f)
            return;
        const float dead = 0.18f;
        float speed = cameraSpeed / 100.0f;
        if (Abs(rx) > dead) {
            float v = (Abs(rx) - dead) / (1.0f - dead);
            v = v * v * (rx < 0 ? -1.0f : 1.0f);
            float angle = v * 2.6f * speed * dt;                 // up to ~150°/s
            CK3dEntity@ oref = ctx.Borrow3dEntityByName("Cam_OrientRef");
            CK3dEntity@ orient = ctx.Borrow3dEntityByName("Cam_Orient");
            if (oref !is null && orient !is null) {
                VxVector up(0, 1, 0);
                oref.Rotate(up, angle, null, false);
                orient.Rotate(up, angle, null, false);
            }
        }
        if (Abs(ry) > dead) {
            CK3dEntity@ pos = ctx.Borrow3dEntityByName("Cam_Pos");
            CK3dEntity@ orient = ctx.Borrow3dEntityByName("Cam_Orient");
            if (pos !is null && orient !is null) {
                float v = (Abs(ry) - dead) / (1.0f - dead);
                // stick up looks up: the camera goes down (and sees further ahead); InvertY swaps it
                v = v * v * (ry < 0 ? 1.0f : -1.0f) * (invertY ? -1.0f : 1.0f);
                VxVector p;
                pos.GetPosition(p, orient);
                float r = sqrt(p.y * p.y + p.z * p.z);
                float elev = atan2(p.y, -p.z) + v * 1.2f * speed * dt;
                elev = Max(0.35f, Min(1.50f, elev));                // ~20° .. ~86°
                p.y = r * sin(elev);
                p.z = -r * cos(elev);
                pos.SetPosition(p, orient, false);
            }
        }
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
        string id = pad + "_" + key;
        string name = "BallanceHD_Glyph_" + id;
        if (key == "" || failed.exists(id))
            return null;
        CKTexture@ tex = ctx.BorrowTextureByName(name);
        if (tex !is null)
            return tex;
        CKContext@ ck = ctx.BorrowCKContext();
        if (ck is null)
            return null;
        @tex = cast<CKTexture>(ck.CreateObject(CKCID_TEXTURE, name));
        if (tex is null || !tex.LoadImage(root + "glyphs\\" + id + ".tga")) {
            ctx.LogWarn("Glyph " + id + " could not be loaded");
            failed.set(id, true);
            return null;
        }
        return tex;
    }

    // keys of one prompt joined by '+' share a label
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
        // in the menus every confirm / back opens another page: show the prompts again
        if (state != 2 && (ButtonPressed(0) || ButtonPressed(1)))
            promptTime = 0.0f;
        promptTime += io.DeltaTime;
        float alpha = promptTime < 10.0f ? 1.0f : 1.0f - (promptTime - 10.0f) / 2.0f;
        if (alpha <= 0.0f)
            return;

        ImDrawList@ dl = ImGui::GetForegroundDrawList();
        ImFont@ font = ImGui::GetFont();
        float g = screen.y * 0.05f;              // glyph size: 5 % of the screen height (readable on a TV)
        float margin = screen.y * 0.028f;

        if (state != 2) {
            // navigation: bottom right, between the menu column and the screen edge, scaled to fit there
            array<string> keys = { "dpad", Glyph(0), Glyph(1) };
            array<string> labels = { T("Auswählen", "Select"), T("Bestätigen", "Confirm"), T("Zurück", "Back") };
            float menuRight = screen.x * 0.5f + screen.y * 0.362f;     // the game's menu column is 0.72 x height wide
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
            // in a level the lives sit bottom right: stay above them
            float y = (state == 1 ? screen.y * 0.80f : screen.y - margin) - gs;
            Panel(dl, x - gap * 0.6f, y - gap * 0.4f, x + rw + gap * 0.6f, y + gs + gap * 0.4f, alpha);
            DrawRow(ctx, dl, x, y, keys, labels, gs, gap, alpha);
            ImGui::PopFont();
        }

        if (state != 0) {
            // controls: top left (score bottom left, lives bottom right)
            array<string> keys;
            array<string> labels;
            keys.insertLast("lstick");
            labels.insertLast(analogRoll ? T("Rollen (stufenlos)", "Roll (stepless)") : T("Rollen", "Roll"));
            if (freeCamera) {
                keys.insertLast("rstick");
                labels.insertLast(T("Kamera frei drehen / neigen", "Turn / tilt the camera"));
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
