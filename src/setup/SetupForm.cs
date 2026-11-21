using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BallanceHD
{
    class SetupForm : Form
    {
        readonly TextBox path = new TextBox();
        readonly Label checkText = Theme.Label("");
        readonly CheckBox chkDesktop = Theme.Check(T.Get("su_desktop"));
        readonly CheckBox chkStart = Theme.Check(T.Get("su_startmenu"));
        readonly TextBox logBox = new TextBox();
        readonly RetroButton btnInstall = Theme.Button(T.Get("su_install"), true);
        readonly RetroButton btnUninstall = Theme.Button(T.Get("su_uninstall"));
        readonly RetroButton btnClose = Theme.Button(T.Get("btn_close"));
        readonly RetroButton btnLaunch = Theme.Button(T.Get("su_launch"), true);
        Bitmap banner;
        string bannerDir;

        public SetupForm(string initialDir)
        {
            Text = T.Get("su_title");
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(760, 600);
            DoubleBuffered = true;

            var head = new Panel { Bounds = new Rectangle(0, 0, ClientSize.Width, 140) };
            head.Paint += (o, e) => Banner.Paint(e.Graphics, head.ClientRectangle, banner, "HD PATCH  ·  SETUP  ·  v" + Version.Text);
            Controls.Add(head);

            int y = 152;
            var l = Theme.Label(T.Get("su_folder"));
            l.Location = new Point(20, y);
            Controls.Add(l);
            path.Bounds = new Rectangle(20, y + 22, 600, 24);
            path.BackColor = Theme.PanelLight;
            path.ForeColor = Theme.Text;
            path.BorderStyle = BorderStyle.FixedSingle;
            path.Text = initialDir ?? "";
            path.TextChanged += (o, e) => Revalidate();
            Controls.Add(path);
            var browse = Theme.Button(T.Get("su_browse"));
            browse.Bounds = new Rectangle(628, y + 20, 112, 28);
            browse.Click += (o, e) => Browse();
            Controls.Add(browse);

            checkText.Location = new Point(20, y + 56);
            checkText.MaximumSize = new Size(720, 0);
            Controls.Add(checkText);


            y += 132;
            chkDesktop.Checked = true;
            chkDesktop.Location = new Point(20, y);
            chkStart.Checked = true;
            chkStart.Location = new Point(20, y + 26);
            Controls.AddRange(new Control[] { chkDesktop, chkStart });
            btnUninstall.Bounds = new Rectangle(620, y, 120, 28);
            btnUninstall.Click += (o, e) => RunUninstall();
            Controls.Add(btnUninstall);

            y += 64;
            var note = Theme.Label(T.Get("su_note"), Theme.Small, Theme.TextDim);
            note.MaximumSize = new Size(720, 0);
            note.Location = new Point(20, y);
            Controls.Add(note);
            y += 26;
            logBox.Bounds = new Rectangle(20, y, 720, ClientSize.Height - y - 70);
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BackColor = Theme.Panel;
            logBox.ForeColor = Theme.TextDim;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.Font = new Font("Consolas", 8.5f);
            Controls.Add(logBox);

            int by = ClientSize.Height - 52;
            btnClose.Bounds = new Rectangle(ClientSize.Width - 140, by, 120, 36);
            btnClose.Click += (o, e) => Close();
            btnInstall.Bounds = new Rectangle(ClientSize.Width - 296, by, 146, 36);
            btnInstall.Click += (o, e) => RunInstall();
            var logo = Theme.Logo(new Rectangle(18, by - 4, 44, 44), Theme.Back);
            if (logo != null)
                Controls.Add(logo);
            Controls.Add(Theme.CreditsLabel(new Point(70, by + 11)));
            btnLaunch.Bounds = new Rectangle(ClientSize.Width - 326, by, 176, 36);
            btnLaunch.Visible = false;
            btnLaunch.Click += (o, e) =>
            {
                Process.Start(new ProcessStartInfo(Path.Combine(Dir, Game.LauncherExe)) { WorkingDirectory = Dir });
                Close();
            };
            Controls.AddRange(new Control[] { btnClose, btnInstall, btnLaunch });
            Revalidate();
            Theme.ScaleToDpi(this);
        }

        string Dir { get { return path.Text.Trim().Trim('"').TrimEnd('\\'); } }

        void Browse()
        {
            using (var dlg = new FolderBrowserDialog { Description = T.Get("su_browse_desc"), SelectedPath = Dir })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    path.Text = dlg.SelectedPath;
            }
        }

        void Revalidate()
        {
            var c = Game.Check(Dir);
            if (c.Ok && bannerDir != Dir)
            {
                if (banner != null)
                    banner.Dispose();
                banner = Banner.Load(Dir);
                bannerDir = Dir;
                Invalidate(true);
            }
            if (!c.Ok)
            {
                checkText.ForeColor = Theme.Warn;
                checkText.Text = T.Get("su_bad") + "\n• " + string.Join("\n• ", c.Problems);
            }
            else
            {
                checkText.ForeColor = Theme.Good;
                checkText.Text = c.Release;
            }
            bool installed = Installer.IsInstalled(Dir);
            btnInstall.Enabled = c.Ok;
            btnInstall.Text = installed ? T.Get("su_update") : T.Get("su_install");
            btnUninstall.Visible = installed;
        }

        void Log(string line)
        {
            logBox.AppendText(line + Environment.NewLine);
        }

        InstallOptions Options()
        {
            return new InstallOptions
            {
                DesktopShortcut = chkDesktop.Checked,
                StartMenuShortcut = chkStart.Checked,
            };
        }

        void RunInstall()
        {
            if (!EnsureWritable("/install"))
                return;
            var o = Options();
            if (Run(() => new Installer(Log).Install(Dir, o)))
            {
                btnInstall.Visible = false;
                btnUninstall.Visible = false;
                btnLaunch.Visible = true;
                MessageBox.Show(this, T.Get("su_success"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void RunUninstall()
        {
            if (MessageBox.Show(this, T.Get("su_uninstall_confirm"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            if (!EnsureWritable("/uninstall"))
                return;
            if (Run(() => new Installer(Log).Uninstall(Dir)))
            {
                MessageBox.Show(this, T.Get("su_removed"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Revalidate();
            }
        }

        bool Run(Action work)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                work();
                return true;
            }
            catch (Exception ex)
            {
                Log("!! " + ex.Message);
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        /// <summary>Folders under Program Files need admin rights: restart elevated for that folder.</summary>
        bool EnsureWritable(string verb)
        {
            if (Installer.CanWrite(Dir))
                return true;
            if (Installer.IsElevated())
            {
                MessageBox.Show(this, T.Get("err_nowrite", Dir), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            var args = verb + " /dir=\"" + Dir + "\"" + (verb == "/install" ? Program.OptionArgs(Options()) : "");
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, args) { Verb = "runas", UseShellExecute = true });
                Close();
            }
            catch (Exception)
            {
                // the user declined the UAC prompt
            }
            return false;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (banner != null)
                banner.Dispose();
            base.OnFormClosed(e);
        }
    }
}
