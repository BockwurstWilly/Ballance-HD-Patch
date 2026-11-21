using System;
using System.IO;
using System.Windows.Forms;

namespace BallanceHD
{
    static class Program
    {
        /// <summary>
        /// BallanceHDLauncher.exe [/lang=de|en] [/snapshot=folder] [/apply] [/play]
        ///   /play       start the game right away with the saved settings
        ///   /apply      write the current settings into the game files and exit (setup, tests)
        ///   /snapshot   render every page to PNG and exit (documentation, tests)
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var dir = Game.FindInstallDir();
            if (dir == null)
            {
                MessageBox.Show(T.Get("err_nogame"), "Ballance - HD Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            var settings = HdSettings.Load(dir);
            T.SetLanguage(settings.UiLanguage);
            foreach (var a in args)
                if (a.StartsWith("/lang=", StringComparison.OrdinalIgnoreCase))
                    T.SetLanguage(a.Substring(6));
            if (!File.Exists(Path.Combine(dir, Game.PluginDll)))
            {
                MessageBox.Show(T.Get("err_notinstalled"), "Ballance - HD Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
            foreach (var a in args)
            {
                if (string.Equals(a, "/apply", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Save(dir);
                    settings.Apply(dir);
                    return 0;
                }
            }
            var form = new LauncherForm(dir);
            foreach (var a in args)
            {
                // /play: start right away with the saved settings (direct shortcut for the TV, tests)
                if (string.Equals(a, "/play", StringComparison.OrdinalIgnoreCase))
                {
                    form.Opacity = 0;
                    form.ShowInTaskbar = false;
                    form.Shown += (o, e) => form.BeginInvoke(new Action(form.PlayNow));
                }
                if (a.StartsWith("/snapshot=", StringComparison.OrdinalIgnoreCase))
                {
                    var folder = a.Substring(10).Trim('"');
                    form.Opacity = 0;
                    form.ShowInTaskbar = false;
                    form.Shown += (o, e) => form.Snapshot(folder);
                }
            }
            Application.Run(form);
            return 0;
        }
    }
}
