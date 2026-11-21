[bml.mod id="compiware.ballancehd"
         name="Ballance HD"
         version="1.1.0"
         author="CompiWare / BockwurstWilly"
         description="Full-screen widescreen picture (no black bars) and controller button prompts"
         bml="0.3.14"]
class BallanceHD {
    // ---- files the launcher maintains in ModLoader\BallanceHD (working directory of the game: Bin) ----
    //   pad_xbox / pad_ps   which controller is connected right now (written live by the launcher)
    //   lang_de             German texts (else English)
    //   hints_off           no button prompts
    //   wide_off            no widescreen camera fix
    string root = "..\\ModLoader\\BallanceHD\\";

    // widescreen
    int lastCam = 0;
    int lastW = 0;
    int lastH = 0;
    bool wide = true;

    // button prompts
    string pad = "";            // "xbox", "ps" or "" (none)
    bool german = false;
    bool hints = true;
    int pollFrames = 0;
    float levelTime = 0.0f;     // seconds since the level (re)started; the legend shows for the first seconds
    bool tutorialLevel = false;
    bool levelKnown = true;
    dictionary textures;        // "xbox_a" -> texture name (loaded once)
    dictionary failed;

    void OnLoad(const BML::ModContext &in ctx) {
        ReadState();
        ctx.LogInfo("Ballance HD loaded (pad=" + pad + ", german=" + german + ", hints=" + hints + ", wide=" + wide + ")");
    }

    void ReadState() {
        if (BML::Path::Exists(root + "pad_xbox"))
            pad = "xbox";
        else if (BML::Path::Exists(root + "pad_ps"))
            pad = "ps";
        else
            pad = "";
        german = BML::Path::Exists(root + "lang_de");
        hints = !BML::Path::Exists(root + "hints_off");
        wide = !BML::Path::Exists(root + "wide_off");
    }

    void OnGameEvent(const BML::ModContext &in ctx, BML::GameEvent event) {
        if (event == BML::GAME_EVENT_START_LEVEL) {
            levelTime = 0.0f;
            tutorialLevel = false;
            levelKnown = false;     // the level state is not readable yet at this event: see ReadLevelId
        }
    }

    void OnProcess(const BML::ModContext &in ctx) {
        if (++pollFrames >= 30) {
            pollFrames = 0;
            ReadState();
        }
        if (!levelKnown && pollFrames % 10 == 0)
            ReadLevelId(ctx);
        if (wide)
            FixAspect(ctx);
        if (hints && pad != "")
            DrawPrompts(ctx);
    }

    // the level number from the game's "CurrentLevel" array (written while the level loads)
    void ReadLevelId(const BML::ModContext &in ctx) {
        CKDataArray@ arr = ctx.BorrowDataArrayByName("CurrentLevel");
        if (arr is null)
            return;
        int col = BML::CK::FindColumn(arr, "Level ID");
        if (col < 0)
            return;
        levelKnown = true;
        int id = BML::CK::GetInt(arr, 0, col, 0);
        tutorialLevel = id == 1;
        ctx.LogInfo("Level " + id);
    }

    // ---- widescreen -----------------------------------------------------------------------------
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

    // ---- button prompts -------------------------------------------------------------------------

    string T(const string &in de, const string &in en) { return german ? de : en; }

    CKTexture@ Glyph(const BML::ModContext &in ctx, const string &in key) {
        string id = pad + "_" + key;
        string name = "BallanceHD_Glyph_" + id;
        if (failed.exists(id))
            return null;
        CKTexture@ tex = ctx.BorrowTextureByName(name);
        if (tex !is null)
            return tex;
        CKContext@ ck = ctx.BorrowCKContext();
        if (ck is null)
            return null;
        CKObject@ obj = ck.CreateObject(CKCID_TEXTURE, name);
        @tex = cast<CKTexture>(obj);
        if (tex is null || !tex.LoadImage(root + "glyphs\\" + id + ".tga")) {
            ctx.LogWarn("Glyph " + id + " could not be loaded");
            failed.set(id, true);
            return null;
        }
        return tex;
    }

    // one row of prompts: glyph(s), label, glyph(s), label ... ; keys separated by '+' share one label
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
                CKTexture@ tex = Glyph(ctx, parts[k]);
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
        ImDrawList@ dl = ImGui::GetForegroundDrawList();
        float g = screen.y * 0.052f;            // glyph size: 5.2 % of the screen height (readable on a TV)
        float gap = g * 0.5f;
        float margin = screen.y * 0.03f;
        bool inLevel = ctx.IsInLevel;
        bool paused = ctx.IsPaused;

        if (!inLevel || paused) {
            // menus: navigation bar top right (bottom right holds the lives in a level)
            array<string> keys = { "dpad", "a", "b" };
            array<string> labels = { T("Auswählen", "Select"), T("Bestätigen", "Confirm"), T("Zurück", "Back") };
            float w = RowWidth(keys, labels, g, gap);
            float x = screen.x - margin - w;
            float y = margin;
            Panel(dl, x - gap * 0.6f, y - gap * 0.4f, screen.x - margin + gap * 0.6f, y + g + gap * 0.4f, 1.0f);
            DrawRow(ctx, dl, x, y, keys, labels, g, gap, 1.0f);
        }

        if (inLevel) {
            levelTime += io.DeltaTime;
            // controls: while paused, and for the first seconds of every level (always in the tutorial level)
            float alpha = 0.0f;
            if (paused)
                alpha = 1.0f;
            else if (tutorialLevel)
                alpha = 0.85f;
            else if (levelTime < 10.0f)
                alpha = levelTime < 8.0f ? 1.0f : (10.0f - levelTime) / 2.0f;
            if (alpha <= 0.0f)
                return;
            array<string> keys = { "lstick", "rstick_lr+lb+rb", "rstick_up+y", "start" };
            array<string> labels = { T("Rollen", "Roll"), T("Kamera drehen", "Rotate camera"), T("Kamera anheben", "Raise camera"), T("Pause", "Pause") };
            if (tutorialLevel && !paused) {
                keys.insertLast("x");
                labels.insertLast(T("Einführung überspringen", "Skip tutorial"));
            }
            // a column top left, out of the way of the HUD (score bottom left, lives bottom right)
            float lg = g * 0.9f;
            float x = margin;
            float y = margin;
            float colW = 0.0f;
            for (uint i = 0; i < keys.length(); i++) {
                array<string> k = { keys[i] };
                array<string> l = { labels[i] };
                colW = Max(colW, RowWidth(k, l, lg, gap));
            }
            Panel(dl, x - gap * 0.4f, y - gap * 0.4f, x + colW + gap * 0.4f, y + keys.length() * (lg + gap * 0.35f) + gap * 0.1f, alpha);
            for (uint i = 0; i < keys.length(); i++) {
                array<string> k = { keys[i] };
                array<string> l = { labels[i] };
                DrawRow(ctx, dl, x, y, k, l, lg, gap, alpha);
                y += lg + gap * 0.35f;
            }
        }
    }

    float Max(float a, float b) { return a > b ? a : b; }
}
