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
        CheckBox chkPad;
        RetroSlider slDead;
        Label lblDead, lblPadStatus;
        // game
        ComboBox cbGameLang, cbUiLang;
        CheckBox chkSkip, chkFps, chkTitle, chkLifeBall;

        public LauncherForm(string dir)
        {
            this.dir = dir;
            s = HdSettings.Load(dir);
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
            string[] names = { T.Get("nav_gfx"), T.Get("nav_pad"), T.Get("nav_game"), T.Get("nav_info") };
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
                cbFps.Items.Add(v < 0 ? T.Get("gfx_fps_auto", CurrentDisplay().Hz) : v == 0 ? T.Get("gfx_fps_off") : T.Get("gfx_fps_n", v));
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

            slDead = new RetroSlider { Minimum = 10, Maximum = 80, Width = 280 };
            lblDead = Theme.Label("", Theme.Bold, Theme.AccentLight);
            var l = Theme.Label(T.Get("pad_dead"));
            l.Location = new Point(28, y + 4);
            slDead.Location = new Point(210, y);
            lblDead.Location = new Point(500, y + 4);
            slDead.ValueChanged += (o, e) => lblDead.Text = slDead.Value + " %";
            page.Controls.AddRange(new Control[] { l, slDead, lblDead });
            y += 40;

            var lay = Theme.Label(T.Get("pad_layout"), Theme.Display(12f), Theme.Brass);
            lay.Location = new Point(28, y);
            page.Controls.Add(lay);
            var layHint = Theme.Label(T.Get("pad_layout_hint"), Theme.Small, Theme.TextDim);
            layHint.Location = new Point(330, y + 4);
            page.Controls.Add(layHint);
            y += 30;

            var rows = new[]
            {
                new[] { T.Get("pad_l_stick"), T.Get("act_roll") },
                new[] { T.Get("pad_r_stick_x") + "  ·  " + T.Get("pad_shoulders"), T.Get("act_cam") },
                new[] { T.Get("pad_r_stick_y") + "  ·  LT / RT  ·  Y", T.Get("act_lift") },
                new[] { T.Get("pad_a"), T.Get("act_confirm") },
                new[] { T.Get("pad_b"), T.Get("act_back") },
                new[] { T.Get("pad_start"), T.Get("act_pause") },
                new[] { T.Get("pad_x"), T.Get("act_skip") },
                new[] { T.Get("pad_back"), T.Get("act_reset") },
            };
            var table = new Panel { Bounds = new Rectangle(24, y, 640, rows.Length * 24 + 2), BackColor = Theme.Border };
            for (int i = 0; i < rows.Length; i++)
            {
                var r = new Panel { Bounds = new Rectangle(1, 1 + i * 24, 638, 23), BackColor = i % 2 == 0 ? Theme.PanelLight : Theme.Panel };
                var a = Theme.Label(rows[i][0]);
                a.Location = new Point(8, 3);
                var b = Theme.Label(rows[i][1], Theme.Body, Theme.AccentLight);
                b.Location = new Point(330, 3);
                r.Controls.Add(a);
                r.Controls.Add(b);
                table.Controls.Add(r);
            }
            page.Controls.Add(table);
            y += table.Height + 10;
            Note(page, ref y, T.Get("pad_menu_hint"));
            return page;
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
            lblDead.Text = s.Deadzone + " %";

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

        void PollPad()
        {
            if (starting)
                return;
            var p = pads.Poll();
            var k = pads.Kind;
            lblPadStatus.Text = k == PadKind.Xbox ? T.Get("pad_xbox") : k == PadKind.PlayStation ? T.Get("pad_sony") : T.Get("pad_none");
            lblPadStatus.ForeColor = k == PadKind.None ? Theme.TextDim : Theme.Good;
            bool aNow = p.Has(Pads.A) || p.Has(Pads.Start);
            bool aBefore = lastPad.Has(Pads.A) || lastPad.Has(Pads.Start);
            lastPad = p;
            if (aNow && !aBefore && ContainsFocus && Form.ActiveForm == this)
                Play();
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
                Close();
                return;
            }
            // the launcher stays in the background and turns controller input into keys until the game ends
            Hide();
            ShowInTaskbar = false;
            var bridge = new Bridge(game, s.Deadzone);
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
            string[] names = { "graphics", "controller", "game", "about" };
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
