using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace BallanceHD
{
    class LauncherForm : Form
    {
        readonly string dir;
        readonly HdSettings s;
        readonly List<DisplayInfo> displays = Displays.All();
        Bitmap banner;

        readonly Panel content = new Panel();
        readonly List<RetroButton> navButtons = new List<RetroButton>();
        readonly List<Control> pages = new List<Control>();
        readonly Label status = Theme.Label("", Theme.Small, Theme.TextDim);
        readonly System.Windows.Forms.Timer statusTimer = new System.Windows.Forms.Timer { Interval = 4000 };
        readonly System.Windows.Forms.Timer padTimer = new System.Windows.Forms.Timer { Interval = 50 };
        readonly Pads pads = new Pads();
        PadState lastPad;
        bool starting;

        // graphics
        ComboBox cbDisplay, cbMode, cbRes, cbAa, cbFps;
        CheckBox chkWide;
        List<Size2> modes = new List<Size2>();
        // controller
        CheckBox chkPad, chkPrompts, chkAnalog, chkFreeCam, chkInvert;
        RetroSlider slDead, slCam;
        Label lblDead, lblCam, lblPadStatus;
        // button layout (shared with the game mod: ModLoader\Configs\compiware.ballancehd.cfg)
        readonly PadConfig padCfg;
        readonly List<ComboBox> cbMap = new List<ComboBox>();
        readonly List<GlyphIcon> glyphIcons = new List<GlyphIcon>();
        int page;
        // game
        ComboBox cbGameLang, cbUiLang;
        CheckBox chkSkip, chkFps, chkTitle, chkLifeBall;

        public LauncherForm(string dir)
        {
            this.dir = dir;
            s = HdSettings.Load(dir);
            padCfg = PadConfig.Load(dir);
            banner = Banner.Load(dir);

            Text = "Ballance - HD Launcher";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(880, 680);
            DoubleBuffered = true;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            BuildLayout();
            LoadValues();
            ShowPage(0);
            Theme.ScaleToDpi(this);
            padTimer.Tick += (o, e) => PollPad();
            padTimer.Start();
        }

        // ---- layout ------------------------------------------------------------------------------

        const int BannerH = 150, NavW = 180, BottomH = 58;

        void BuildLayout()
        {
            var head = new Panel { Bounds = new Rectangle(0, 0, ClientSize.Width, BannerH) };
            head.Paint += (o, e) => Banner.Paint(e.Graphics, head.ClientRectangle, banner, "HD LAUNCHER  ·  v" + Version.Text);
            Controls.Add(head);

            var nav = new Panel { Bounds = new Rectangle(0, BannerH, NavW, ClientSize.Height - BannerH - BottomH), BackColor = Theme.Back };
            nav.Paint += (o, e) =>
            {
                using (var p = new Pen(Theme.Border))
                    e.Graphics.DrawLine(p, nav.Width - 1, 0, nav.Width - 1, nav.Height);
            };
            Controls.Add(nav);
            string[] names = { T.Get("nav_gfx"), T.Get("nav_pad"), T.Get("nav_map"), T.Get("nav_game"), T.Get("nav_info") };
            for (int i = 0; i < names.Length; i++)
            {
                int page = i;
                var b = Theme.Button(names[i]);
                b.Font = Theme.Display(11f);
                b.Bounds = new Rectangle(14, 18 + i * 46, NavW - 28, 38);
                b.Click += (o, e) => ShowPage(page);
                nav.Controls.Add(b);
                navButtons.Add(b);
            }
            var motto = Theme.Label(T.Get("motto"), Theme.Display(9f, FontStyle.Italic), Theme.TextDim);
            motto.MaximumSize = new Size(NavW - 28, 0);
            motto.Location = new Point(14, nav.Height - 50);
            nav.Controls.Add(motto);

            content.Bounds = new Rectangle(NavW, BannerH, ClientSize.Width - NavW, ClientSize.Height - BannerH - BottomH);
            content.BackColor = Theme.Panel;
            Controls.Add(content);

            pages.Add(BuildGraphicsPage());
            pages.Add(BuildPadPage());
            pages.Add(BuildMapPage());
            pages.Add(BuildGamePage());
            pages.Add(BuildInfoPage());
            foreach (var p in pages)
            {
                p.Bounds = content.ClientRectangle;
                p.Visible = false;
                content.Controls.Add(p);
            }

            var bottom = new Panel { Bounds = new Rectangle(0, ClientSize.Height - BottomH, ClientSize.Width, BottomH), BackColor = Theme.Panel };
            bottom.Paint += (o, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0); };
            Controls.Add(bottom);
            status.AutoSize = false;
            status.AutoEllipsis = true;
            status.Bounds = new Rectangle(64, 9, 380, 18);
            bottom.Controls.Add(status);
            statusTimer.Tick += (o, e) => { statusTimer.Stop(); status.Text = ""; };
            var logo = Theme.Logo(new Rectangle(12, 7, 44, 44), Theme.Panel);
            if (logo != null)
                bottom.Controls.Add(logo);
            bottom.Controls.Add(Theme.CreditsLabel(new Point(64, 31)));

            var play = Theme.Button(T.Get("btn_play"), true);
            play.Bounds = new Rectangle(ClientSize.Width - 168, 11, 154, 36);
            play.Click += (o, e) => Play();
            var apply = Theme.Button(T.Get("btn_apply"));
            apply.Bounds = new Rectangle(ClientSize.Width - 296, 12, 118, 34);
            apply.Click += (o, e) => { if (SaveAll()) SetStatus(T.Get("st_saved"), Theme.Good); };
            var close = Theme.Button(T.Get("btn_close"));
            close.Bounds = new Rectangle(ClientSize.Width - 424, 12, 118, 34);
            close.Click += (o, e) => Close();
            bottom.Controls.AddRange(new Control[] { play, apply, close });
            AcceptButton = null;
        }

        void ShowPage(int index)
        {
            page = index;
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].Visible = i == index;
                navButtons[i].Primary = i == index;
                navButtons[i].Invalidate();
            }
        }

        Panel NewPage(string title, out int y)
        {
            var page = new Panel { BackColor = Theme.Panel };
            var head = new Label
            {
                Text = title,
                Font = Theme.Display(17f),
                ForeColor = Theme.Brass,
                BackColor = Color.Transparent,
                AutoSize = true,
                UseMnemonic = false,
                Location = new Point(24, 12),
            };
            page.Controls.Add(head);
            page.Paint += (o, e) => Theme.Rule(e.Graphics, Theme.S(24), page.Width - Theme.S(24), Theme.S(50));
            y = 66;
            return page;
        }

        void Row(Panel page, ref int y, string label, Control control, string hint = null)
        {
            var l = Theme.Label(label);
            l.Location = new Point(28, y + 4);
            page.Controls.Add(l);
            control.Location = new Point(210, y);
            page.Controls.Add(control);
            y += control.Height + 4;
            if (hint != null)
            {
                var h = Theme.Label(hint, Theme.Small, Theme.TextDim);
                h.Location = new Point(210, y);
                h.MaximumSize = new Size(460, 0);
                page.Controls.Add(h);
                y += Theme.Unscaled(h.PreferredHeight) + 2;
            }
            y += 8;
        }

        void CheckRow(Panel page, ref int y, CheckBox box, string hint = null)
        {
            box.Location = new Point(28, y);
            page.Controls.Add(box);
            y += 26;
            if (hint != null)
            {
                var h = Theme.Label(hint, Theme.Small, Theme.TextDim);
                h.Location = new Point(50, y - 4);
                h.MaximumSize = new Size(620, 0);
                page.Controls.Add(h);
                y += Theme.Unscaled(h.PreferredHeight) + 2;
            }
            y += 6;
        }

        Label Note(Panel page, ref int y, string text)
        {
            var info = Theme.Label(text, Theme.Small, Theme.TextDim);
            info.MaximumSize = new Size(640, 0);
            info.Location = new Point(28, y);
            page.Controls.Add(info);
            y += Theme.Unscaled(info.PreferredHeight) + 8;
            return info;
        }

        // ---- pages -------------------------------------------------------------------------------

        Control BuildGraphicsPage()
        {
            int y;
            var page = NewPage(T.Get("nav_gfx"), out y);
            cbDisplay = Theme.Combo(440);
            foreach (var d in displays)
                cbDisplay.Items.Add(T.Get("gfx_display_item", d.Number, d.Name, d.Width, d.Height, d.Hz, d.Primary ? T.Get("gfx_primary") : ""));
            cbDisplay.SelectedIndexChanged += (o, e) => { FillResolutions(); FillFps(); };
            Row(page, ref y, T.Get("gfx_display"), cbDisplay);

            cbMode = Theme.Combo(300);
            cbMode.Items.AddRange(new object[] { T.Get("gfx_mode_bl"), T.Get("gfx_mode_fs"), T.Get("gfx_mode_win") });
            cbMode.SelectedIndexChanged += (o, e) => FillResolutions();
            Row(page, ref y, T.Get("gfx_mode"), cbMode, T.Get("gfx_mode_hint"));

            cbRes = Theme.Combo(300);
            Row(page, ref y, T.Get("gfx_res"), cbRes);

            cbAa = Theme.Combo(160);
            cbAa.Items.AddRange(new object[] { T.Get("gfx_off"), "2× MSAA", "4× MSAA", "8× MSAA" });
            Row(page, ref y, T.Get("gfx_aa"), cbAa);

            cbFps = Theme.Combo(240);
            Row(page, ref y, T.Get("gfx_fps"), cbFps);

            y += 4;
            chkWide = Theme.Check(T.Get("gfx_wide"));
            CheckRow(page, ref y, chkWide, T.Get("gfx_wide_hint"));
            y += 4;
            Note(page, ref y, T.Get("gfx_note"));
            return page;
        }

        DisplayInfo CurrentDisplay()
        {
            int i = cbDisplay.SelectedIndex;
            return i >= 0 && i < displays.Count ? displays[i] : Displays.Pick(displays, "");
        }

        static readonly string[] ModeKeys = { "borderless", "fullscreen", "window" };

        void FillResolutions()
        {
            var d = CurrentDisplay();
            int keepW = s.Width, keepH = s.Height;
            if (cbRes.SelectedIndex > 0 && cbRes.SelectedIndex - 1 < modes.Count)
            {
                keepW = modes[cbRes.SelectedIndex - 1].W;
                keepH = modes[cbRes.SelectedIndex - 1].H;
            }
            cbRes.Items.Clear();
            modes = Displays.Modes(d.Device);
            if (cbMode.SelectedIndex == 2)
                modes = modes.Where(m => m.W < d.Width && m.H < d.Height).ToList();
            cbRes.Items.Add(T.Get("gfx_res_native", d.Width, d.Height));
            foreach (var m in modes)
                cbRes.Items.Add(m.ToString());
            int sel = modes.FindIndex(m => m.W == keepW && m.H == keepH);
            cbRes.SelectedIndex = sel >= 0 ? sel + 1 : 0;
            // borderless always runs at the native resolution
            cbRes.Enabled = cbMode.SelectedIndex != 0;
            if (cbMode.SelectedIndex == 0)
                cbRes.SelectedIndex = 0;
            if (cbMode.SelectedIndex == 2)
                cbRes.Items[0] = T.Get("gfx_res_native", s.Resolution(d).W, s.Resolution(d).H).Replace(T.German ? "Nativ" : "Native", "Auto");
        }

        static readonly int[] FpsValues = { -1, 30, 60, 75, 120, 144, 165, 240, 0 };

        void FillFps()
        {
            int keep = cbFps.SelectedIndex >= 0 ? FpsValues[cbFps.SelectedIndex] : s.Fps;
            cbFps.Items.Clear();
            foreach (var v in FpsValues)
                cbFps.Items.Add(v < 0 ? T.Get("gfx_fps_auto", Math.Max(CurrentDisplay().Hz, 60)) : v == 0 ? T.Get("gfx_fps_off") : T.Get("gfx_fps_n", v));
            int i = Array.IndexOf(FpsValues, keep);
            cbFps.SelectedIndex = i >= 0 ? i : 0;
        }

        Control BuildPadPage()
        {
            int y;
            var page = NewPage(T.Get("nav_pad"), out y);
            chkPad = Theme.Check(T.Get("pad_use"));
            chkPad.Location = new Point(28, y);
            page.Controls.Add(chkPad);
            lblPadStatus = Theme.Label("", Theme.Bold, Theme.TextDim);
            lblPadStatus.Location = new Point(330, y + 2);
            page.Controls.Add(lblPadStatus);
            y += 26;
            var h = Theme.Label(T.Get("pad_use_hint"), Theme.Small, Theme.TextDim);
            h.Location = new Point(50, y - 4);
            page.Controls.Add(h);
            y += 30;

            slDead = Slider(page, ref y, T.Get("pad_dead"), 10, 80, out lblDead);
            slDead.ValueChanged += (o, e) => lblDead.Text = slDead.Value + " %";
            y += 4;

            chkAnalog = Theme.Check(T.Get("pad_analog"));
            CheckRow(page, ref y, chkAnalog);
            chkFreeCam = Theme.Check(T.Get("pad_freecam"));
            CheckRow(page, ref y, chkFreeCam);
            chkInvert = Theme.Check(T.Get("pad_invert"));
            CheckRow(page, ref y, chkInvert);
            slCam = Slider(page, ref y, T.Get("pad_camspeed"), 25, 300, out lblCam);
            slCam.ValueChanged += (o, e) => lblCam.Text = slCam.Value + " %";
            y += 4;
            chkPrompts = Theme.Check(T.Get("pad_prompts"));
            CheckRow(page, ref y, chkPrompts);
            y += 4;
            Note(page, ref y, T.Get("pad_ingame_hint"));
            Note(page, ref y, T.Get("pad_launcher_hint"));
            return page;
        }

        RetroSlider Slider(Panel page, ref int y, string label, int min, int max, out Label value)
        {
            var sl = new RetroSlider { Minimum = min, Maximum = max, Width = 280 };
            value = Theme.Label("", Theme.Bold, Theme.AccentLight);
            var l = Theme.Label(label);
            l.Location = new Point(28, y + 4);
            sl.Location = new Point(210, y);
            value.Location = new Point(500, y + 4);
            page.Controls.AddRange(new Control[] { l, sl, value });
            y += 32;
            return sl;
        }

        Control BuildMapPage()
        {
            int y;
            var page = NewPage(T.Get("nav_map"), out y);
            // fixed: the sticks and the d-pad
            FixedRow(page, ref y, new[] { "lstick", "dpad" }, T.Get("act_roll"));
            FixedRow(page, ref y, new[] { "rstick" }, T.Get("act_freecam"));
            y += 6;
            for (int i = 0; i < PadConfig.Actions.Length; i++)
            {
                var a = PadConfig.Actions[i];
                var icon = new GlyphIcon { Bounds = new Rectangle(28, y, 30, 30) };
                glyphIcons.Add(icon);
                var l = Theme.Label(a.Label);
                l.Location = new Point(70, y + 5);
                var cb = Theme.Combo(230);
                foreach (var b in PadConfig.Buttons)
                    cb.Items.Add(b);
                cb.Location = new Point(400, y + 1);
                var ic = icon;
                cb.SelectedIndexChanged += (o, e) => ic.Keys = new[] { PadConfig.Glyph((string)((ComboBox)o).SelectedItem) };
                cbMap.Add(cb);
                page.Controls.AddRange(new Control[] { icon, l, cb });
                y += 34;
            }
            y += 4;
            var reset = Theme.Button(T.Get("map_reset"));
            reset.Bounds = new Rectangle(400, y, 230, 30);
            reset.Click += (o, e) =>
            {
                for (int i = 0; i < PadConfig.Actions.Length; i++)
                    cbMap[i].SelectedItem = PadConfig.Actions[i].Default;
            };
            page.Controls.Add(reset);
            var hint = Theme.Label(T.Get("map_hint"), Theme.Small, Theme.TextDim);
            hint.MaximumSize = new Size(360, 0);
            hint.Location = new Point(28, y);
            page.Controls.Add(hint);
            return page;
        }

        void FixedRow(Panel page, ref int y, string[] keys, string text)
        {
            var icon = new GlyphIcon { Bounds = new Rectangle(28, y, 30 * keys.Length + 4 * (keys.Length - 1), 30), Keys = keys };
            glyphIcons.Add(icon);
            var l = Theme.Label(text, Theme.Body, Theme.AccentLight);
            l.Location = new Point(28 + icon.Width + 12, y + 5);
            page.Controls.AddRange(new Control[] { icon, l });
            y += 34;
        }
        static readonly string[] GameLanguages = { "Deutsch", "English", "Español", "Italiano", "Français" };

        Control BuildGamePage()
        {
            int y;
            var page = NewPage(T.Get("nav_game"), out y);
            cbGameLang = Theme.Combo(200);
            cbGameLang.Items.AddRange(GameLanguages);
            Row(page, ref y, T.Get("game_lang"), cbGameLang);
            y += 6;
            chkSkip = Theme.Check(T.Get("game_skip"));
            CheckRow(page, ref y, chkSkip);
            chkLifeBall = Theme.Check(T.Get("game_lifeball"));
            CheckRow(page, ref y, chkLifeBall);
            chkFps = Theme.Check(T.Get("game_fps"));
            CheckRow(page, ref y, chkFps);
            chkTitle = Theme.Check(T.Get("game_title"));
            CheckRow(page, ref y, chkTitle);
            y += 10;
            cbUiLang = Theme.Combo(200);
            cbUiLang.Items.AddRange(new object[] { T.Get("lang_auto"), "Deutsch", "English" });
            Row(page, ref y, T.Get("game_ui_lang"), cbUiLang);
            y += 6;
            Note(page, ref y, T.Get("game_save_hint"));
            return page;
        }

        Control BuildInfoPage()
        {
            int y;
            var page = NewPage(T.Get("nav_info"), out y);
            var text = Theme.Label(T.Get("info_text", Version.BallancePlayer, Version.BmlPlus));
            text.MaximumSize = new Size(640, 0);
            text.Location = new Point(28, y);
            page.Controls.Add(text);
            y += Theme.Unscaled(text.PreferredHeight) + 20;
            var folder = Theme.Button(T.Get("info_folder"));
            folder.Bounds = new Rectangle(28, y, 200, 32);
            folder.Click += (o, e) => Process.Start("explorer.exe", "\"" + dir + "\"");
            var remove = Theme.Button(T.Get("info_uninstall"));
            remove.Bounds = new Rectangle(240, y, 200, 32);
            remove.Click += (o, e) => Uninstall();
            page.Controls.Add(folder);
            page.Controls.Add(remove);
            y += 48;
            var ver = Theme.Label("Ballance HD " + Version.Text + "  ·  BallancePlayer " + Version.BallancePlayer + "  ·  BML+ " + Version.BmlPlus + "\n" + dir, Theme.Small, Theme.TextDim);
            ver.Location = new Point(28, y);
            page.Controls.Add(ver);
            return page;
        }

        // ---- values ------------------------------------------------------------------------------

        void LoadValues()
        {
            var d = Displays.Pick(displays, s.Display);
            cbDisplay.SelectedIndex = displays.IndexOf(d);
            cbMode.SelectedIndex = Array.IndexOf(ModeKeys, s.Mode) < 0 ? 0 : Array.IndexOf(ModeKeys, s.Mode);
            FillResolutions();
            cbAa.SelectedIndex = s.Msaa >= 8 ? 3 : s.Msaa >= 4 ? 2 : s.Msaa >= 2 ? 1 : 0;
            cbFps.SelectedIndex = -1;
            FillFps();
            int fi = Array.IndexOf(FpsValues, s.Fps);
            cbFps.SelectedIndex = fi >= 0 ? fi : 0;
            chkWide.Checked = s.Widescreen;

            chkPad.Checked = s.Pad;
            slDead.Value = s.Deadzone;
            chkPrompts.Checked = s.Prompts;
            lblDead.Text = s.Deadzone + " %";
            chkAnalog.Checked = padCfg.AnalogRoll;
            chkFreeCam.Checked = padCfg.FreeCamera;
            chkInvert.Checked = padCfg.InvertY;
            slCam.Value = padCfg.CameraSpeed;
            lblCam.Text = padCfg.CameraSpeed + " %";
            for (int i = 0; i < PadConfig.Actions.Length; i++)
                cbMap[i].SelectedItem = padCfg.Map[PadConfig.Actions[i].Key];

            cbGameLang.SelectedIndex = s.GameLanguage;
            chkSkip.Checked = s.SkipIntro;
            chkLifeBall.Checked = s.LifeBallFix;
            chkFps.Checked = s.ShowFps;
            chkTitle.Checked = s.ShowTitle;
            cbUiLang.SelectedIndex = s.UiLanguage == "de" ? 1 : s.UiLanguage == "en" ? 2 : 0;
        }

        void ReadValues()
        {
            var d = CurrentDisplay();
            s.Display = d.Primary ? "" : d.Device;
            s.Mode = ModeKeys[Math.Max(0, cbMode.SelectedIndex)];
            if (cbRes.SelectedIndex > 0 && cbRes.SelectedIndex - 1 < modes.Count)
            {
                s.Width = modes[cbRes.SelectedIndex - 1].W;
                s.Height = modes[cbRes.SelectedIndex - 1].H;
            }
            else
            {
                s.Width = s.Height = 0;
            }
            s.Msaa = new[] { 0, 2, 4, 8 }[Math.Max(0, cbAa.SelectedIndex)];
            s.Fps = FpsValues[Math.Max(0, cbFps.SelectedIndex)];
            s.Widescreen = chkWide.Checked;
            s.Pad = chkPad.Checked;
            s.Deadzone = slDead.Value;
            s.Prompts = chkPrompts.Checked;
            padCfg.AnalogRoll = chkAnalog.Checked;
            padCfg.FreeCamera = chkFreeCam.Checked;
            padCfg.InvertY = chkInvert.Checked;
            padCfg.CameraSpeed = slCam.Value;
            for (int i = 0; i < PadConfig.Actions.Length; i++)
                padCfg.Map[PadConfig.Actions[i].Key] = (string)cbMap[i].SelectedItem ?? PadConfig.Actions[i].Default;
            s.GameLanguage = Math.Max(0, cbGameLang.SelectedIndex);
            s.SkipIntro = chkSkip.Checked;
            s.LifeBallFix = chkLifeBall.Checked;
            s.ShowFps = chkFps.Checked;
            s.ShowTitle = chkTitle.Checked;
            s.UiLanguage = new[] { "auto", "de", "en" }[Math.Max(0, cbUiLang.SelectedIndex)];
        }

        bool SaveAll()
        {
            try
            {
                if (Game.IsRunning(dir))
                {
                    SetStatus(T.Get("st_running"), Theme.Warn);
                    return false;
                }
                ReadValues();
                s.Save(dir);
                s.Apply(dir);
                padCfg.Save(dir);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        void SetStatus(string text, Color color)
        {
            status.ForeColor = color;
            status.Text = text;
            statusTimer.Stop();
            statusTimer.Start();
        }

        // ---- controller --------------------------------------------------------------------------

        // The launcher with a controller: d-pad / left stick up-down moves through the page (and the buttons at
        // the bottom), left-right changes a list or slider, A presses / ticks, LB / RB switch pages, Y applies,
        // Start plays.
        int repeatDir;
        DateTime repeatAt;

        void PollPad()
        {
            if (starting)
                return;
            var p = pads.Poll();
            var k = pads.Kind;
            lblPadStatus.Text = k == PadKind.Xbox ? T.Get("pad_xbox") : k == PadKind.PlayStation ? T.Get("pad_sony") : T.Get("pad_none");
            lblPadStatus.ForeColor = k == PadKind.None ? Theme.TextDim : Theme.Good;
            if (k != PadKind.None)
            {
                string kind = k == PadKind.PlayStation ? "ps" : "xbox";
                foreach (var g in glyphIcons)
                    g.Kind = kind;
            }
            var last = lastPad;
            lastPad = p;
            if (!ContainsFocus || Form.ActiveForm != this || k == PadKind.None)
                return;
            if (status.Text == "")
                SetStatus(T.Get("st_padnav"), Theme.TextDim);
            Func<ushort, bool> pressed = b => p.Has(b) && !last.Has(b);
            if (pressed(Pads.Start)) { Play(); return; }
            if (pressed(Pads.LB)) { ShowPage((page + pages.Count - 1) % pages.Count); FocusFirst(); return; }
            if (pressed(Pads.RB)) { ShowPage((page + 1) % pages.Count); FocusFirst(); return; }
            if (pressed(Pads.Y)) { if (SaveAll()) SetStatus(T.Get("st_saved"), Theme.Good); return; }
            if (pressed(Pads.A)) { Activate(ActiveControl); return; }

            // directions with key repeat (first after 400 ms, then every 110 ms)
            int dir = 0;
            const int dz = 16000;
            if (p.Has(Pads.DPadUp) || p.LY > dz) dir = 1;
            else if (p.Has(Pads.DPadDown) || p.LY < -dz) dir = 2;
            else if (p.Has(Pads.DPadLeft) || p.LX < -dz) dir = 3;
            else if (p.Has(Pads.DPadRight) || p.LX > dz) dir = 4;
            if (dir == 0)
            {
                repeatDir = 0;
                return;
            }
            var now = DateTime.UtcNow;
            if (dir == repeatDir && now < repeatAt)
                return;
            repeatAt = now.AddMilliseconds(dir == repeatDir ? 110 : 400);
            repeatDir = dir;
            if (dir <= 2)
                MoveFocus(dir == 2);
            else
                Adjust(ActiveControl, dir == 4 ? 1 : -1);
        }

        /// <summary>Selectable controls of the visible page, top to bottom, then the buttons at the bottom.</summary>
        List<Control> FocusRing()
        {
            var list = new List<Control>();
            Collect(pages[page], list);
            list.Sort((a, b) => a.Top != b.Top ? a.Top.CompareTo(b.Top) : a.Left.CompareTo(b.Left));
            var bottom = new List<Control>();
            Collect(Controls.OfType<Panel>().Last(), bottom);
            bottom.Sort((a, b) => a.Left.CompareTo(b.Left));
            list.AddRange(bottom);
            return list;
        }

        static void Collect(Control parent, List<Control> list)
        {
            foreach (Control c in parent.Controls)
            {
                if (!c.Visible || !c.Enabled)
                    continue;
                if (c is RetroButton || c is CheckBox || c is ComboBox || c is RetroSlider)
                    list.Add(c);
                else if (c.HasChildren)
                    Collect(c, list);
            }
        }

        void FocusFirst()
        {
            var ring = FocusRing();
            if (ring.Count > 0)
                ring[0].Focus();
        }

        void MoveFocus(bool down)
        {
            var ring = FocusRing();
            if (ring.Count == 0)
                return;
            int i = ring.IndexOf(ActiveControl);
            if (i < 0)
                i = down ? -1 : 0;
            i = (i + (down ? 1 : -1) + ring.Count) % ring.Count;
            ring[i].Focus();
        }

        void Activate(Control c)
        {
            var box = c as CheckBox;
            if (box != null) { box.Checked = !box.Checked; return; }
            var button = c as RetroButton;
            if (button != null) { button.PerformClick(); return; }
            var combo = c as ComboBox;
            if (combo != null && combo.Items.Count > 0) { combo.SelectedIndex = (combo.SelectedIndex + 1) % combo.Items.Count; return; }
            if (c == null)
                FocusFirst();
        }

        void Adjust(Control c, int step)
        {
            var combo = c as ComboBox;
            if (combo != null && combo.Enabled && combo.Items.Count > 0)
            {
                combo.SelectedIndex = Math.Max(0, Math.Min(combo.Items.Count - 1, combo.SelectedIndex + step));
                return;
            }
            var slider = c as RetroSlider;
            if (slider != null)
            {
                slider.Value += step * Math.Max(1, (slider.Maximum - slider.Minimum) / 30);
                return;
            }
            // in the bottom row: left / right moves between the buttons
            if (c is RetroButton && c.Parent == Controls.OfType<Panel>().Last())
                MoveFocus(step > 0);
        }
        // ---- play --------------------------------------------------------------------------------

        public void PlayNow()
        {
            Play();
            // nothing started (game already running, error): don't leave an invisible window behind
            if (!starting)
                Close();
        }

        void Play()
        {
            if (starting)
                return;
            if (!SaveAll())
                return;
            Process game;
            try
            {
                game = Process.Start(new ProcessStartInfo(Path.Combine(dir, Game.PlayerExe))
                {
                    WorkingDirectory = Path.Combine(dir, Game.BinDir),
                    UseShellExecute = false,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T.Get("err_start", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            starting = true;
            padTimer.Stop();
            if (!s.Pad)
            {
                PadMarker.SetPad(dir, PadKind.None);
                Close();
                return;
            }
            // the launcher stays in the background and turns controller input into keys until the game ends
            Hide();
            ShowInTaskbar = false;
            var bridge = new Bridge(game, dir, s.Deadzone);
            var t = new Thread(() =>
            {
                try { bridge.Run(pads); }
                catch (Exception) { }
                try { BeginInvoke(new Action(Close)); } catch (Exception) { }
            }) { IsBackground = true, Name = "pad-bridge" };
            t.Start();
        }

        void Uninstall()
        {
            var setup = Path.Combine(dir, Game.PatchDir, "BallanceHD_Setup.exe");
            if (!File.Exists(setup))
                return;
            try
            {
                Process.Start(new ProcessStartInfo(setup, "/uninstall /dir=\"" + dir + "\"") { UseShellExecute = false });
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Renders every page to PNG (documentation, tests).</summary>
        public void Snapshot(string folder)
        {
            Directory.CreateDirectory(folder);
            string[] names = { "graphics", "controller", "layout", "game", "about" };
            for (int i = 0; i < pages.Count; i++)
            {
                ShowPage(i);
                Refresh();
                using (var bmp = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                    bmp.Save(Path.Combine(folder, "launcher_" + names[i] + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            padTimer.Stop();
            pads.Dispose();
            if (banner != null)
                banner.Dispose();
            base.OnFormClosed(e);
        }
    }
}
