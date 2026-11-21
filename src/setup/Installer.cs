using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace BallanceHD
{
    class InstallOptions
    {
        public bool DesktopShortcut = true;
        public bool StartMenuShortcut = true;
    }

    /// <summary>
    /// Install / update / uninstall. Every file the patch writes is listed in _HDPatch\files.txt; every original
    /// file it replaces, changes or moves away is first copied to _HDPatch\backup. Uninstall deletes the listed
    /// files and copies the backup back, so the game folder ends up as it was (save games are never touched).
    /// </summary>
    class Installer
    {
        public const string SetupCopy = "BallanceHD_Setup.exe";
        const string StateIni = @"_HDPatch\install.ini";
        const string FileList = @"_HDPatch\files.txt";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BallanceHDPatch";
        public const string ShortcutName = "Ballance - HD Launcher.lnk";
        static readonly string[] UninstallShortcuts = { "Ballance – HD-Patch entfernen.lnk", "Ballance - Remove HD Patch.lnk" };

        // originals that are moved out of the game: dgVoodoo's Direct3D 8 wrapper (GOG/Steam ship v2.79) draws the
        // menus and sky black with the new player, the plain Windows Direct3D 8 renders them correctly
        static readonly string[] MovedAway = { @"Bin\D3D8.dll" };
        // originals that are changed in place
        static readonly string[] Modified = { Game.RenderIniPath, Game.PlayerIni };
        // files the game or the mod loader create at run time (removed on uninstall unless they existed before)
        static readonly string[] RuntimeFiles = { Game.PlayerIni, @"Bin\Player.log", @"Bin\AngelScript.log" };

        readonly Action<string> log;
        public Installer(Action<string> log) { this.log = log; }

        public static bool IsInstalled(string dir)
        {
            return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, StateIni));
        }

        // ---- install ---------------------------------------------------------------------------------

        public void Install(string dir, InstallOptions o)
        {
            if (Game.IsRunning(dir))
                throw new InvalidOperationException(T.Get("err_running"));
            var check = Game.Check(dir);
            if (!check.Ok)
                throw new InvalidOperationException(T.Get("err_check") + "\n" + string.Join("\n", check.Problems));
            bool update = IsInstalled(dir);
            var backup = Path.Combine(dir, Game.BackupDir);
            var state = IniDoc.Load(Path.Combine(dir, StateIni));
            var files = new HashSet<string>(ReadList(dir), StringComparer.OrdinalIgnoreCase);
            if (!update)
            {
                state.Set("Install", "HadModLoader", Directory.Exists(Path.Combine(dir, "ModLoader")));
                foreach (var f in RuntimeFiles)
                    state.Set("Existed", f, File.Exists(Path.Combine(dir, f)));
            }
            else
            {
                log(T.Get("log_update"));
            }
            state.Set("Install", "Version", Version.Text);
            state.Save();

            // 0. settings first: an update from 1.x takes them over from the BML+ files removed below
            var settings = HdSettings.Load(dir);
            var padCfg = PadConfig.Load(dir);

            // the payload's file list (paths relative to the game folder)
            var payload = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
                foreach (var e in zip.Entries)
                    if (!e.FullName.EndsWith("/") && !e.FullName.StartsWith("doc/", StringComparison.OrdinalIgnoreCase))
                        payload.Add(e.FullName.Replace('/', '\\'));
            if (update)
            {
                RemoveObsolete(dir, backup, files, payload, state);
                var doc = Path.Combine(dir, Game.PatchDir, "doc");
                if (Directory.Exists(doc))
                    Directory.Delete(doc, true);   // the documents come fresh from the payload
            }

            // 1. originals: back up what is changed, move away what breaks the picture
            //    (only on the first install: on an update these files are already the patch's own)
            if (!update)
                foreach (var f in Modified)
                    BackUp(dir, backup, f);
            foreach (var f in MovedAway)
            {
                var p = Path.Combine(dir, f);
                if (!File.Exists(p))
                    continue;
                BackUp(dir, backup, f);
                File.Delete(p);
                log(T.Get("log_aside", f));
            }
            if (!update)
                log(T.Get("log_backup", backup));

            // 2. the payload (payload.zip: paths relative to the game folder; doc/... goes to _HDPatch\doc)
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
            {
                foreach (var e in zip.Entries.OrderBy(x => x.FullName))
                {
                    if (e.FullName.EndsWith("/"))
                        continue;
                    var rel = e.FullName.Replace('/', '\\');
                    bool doc = rel.StartsWith(@"doc\", StringComparison.OrdinalIgnoreCase);
                    var relTarget = doc ? Path.Combine(Game.PatchDir, rel) : rel;
                    var target = Path.Combine(dir, relTarget);
                    if (!doc && !files.Contains(relTarget) && File.Exists(target))
                        BackUp(dir, backup, relTarget);
                    using (var es = e.Open())
                    using (var ms = new MemoryStream())
                    {
                        es.CopyTo(ms);
                        FileUtil.WriteAtomic(target, ms.ToArray());
                    }
                    Unblock(target);
                    if (!doc)
                        files.Add(relTarget);
                    log(T.Get("log_copy", relTarget));
                }
            }
            // the plugin's in-game font, rendered here from the Windows font (no font file is shipped)
            FontAtlas.Make(dir);
            files.Add(FontAtlas.Tga);
            files.Add(FontAtlas.Bin);
            log(T.Get("log_copy", FontAtlas.Tga));
            WriteList(dir, files);

            // 3. settings: a fresh install starts with the HD defaults (native resolution of the main display),
            //    an update keeps the player's choices
            if (!update)
                settings.GameLanguage = OriginalLanguage(settings.GameLanguage);
            settings.Save(dir);
            settings.Apply(dir);
            padCfg.Save(dir);
            var d = Displays.Pick(Displays.All(), settings.Display);
            var res = settings.Resolution(d);
            log(T.Get("log_settings", res.W, res.H, settings.EffectiveFps(d)));

            // 4. uninstaller copy, shortcuts, Apps & Features entry
            var self = Assembly.GetExecutingAssembly().Location;
            var copy = Path.Combine(dir, Game.PatchDir, SetupCopy);
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(copy), StringComparison.OrdinalIgnoreCase))
                File.Copy(self, copy, true);
            var launcher = Path.Combine(dir, Game.LauncherExe);
            Shortcut.Create(Path.Combine(dir, ShortcutName), launcher, dir, launcher);
            if (o.StartMenuShortcut)
            {
                Shortcut.Create(Path.Combine(StartMenuDir(), ShortcutName), launcher, dir, launcher);
                Shortcut.Create(Path.Combine(StartMenuDir(), T.Get("lnk_uninstall") + ".lnk"), copy, dir, launcher, "/uninstall", "Ballance - Remove HD Patch");
            }
            if (o.DesktopShortcut)
                Shortcut.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName), launcher, dir, launcher);
            log(T.Get("log_shortcuts"));
            Register(dir);
            log(T.Get("log_done"));
        }

        /// <summary>
        /// Update: files of an older version that the new one no longer has (1.x: BML+, AngelScript, the mod, its
        /// fonts and glyphs, the bundled Visual C++ installer) go; where one of them had replaced an original, the
        /// original comes back from the backup. The mod loader folder goes if the patch created it.
        /// </summary>
        void RemoveObsolete(string dir, string backup, HashSet<string> files, HashSet<string> payload, IniDoc state)
        {
            foreach (var rel in files.Where(f => !payload.Contains(f)).ToList())
            {
                Delete(Path.Combine(dir, rel));
                files.Remove(rel);
                var original = Path.Combine(backup, rel);
                if (File.Exists(original))
                {
                    CopyRetrying(original, Path.Combine(dir, rel));
                    File.Delete(original);
                    log(T.Get("log_restore", rel));
                }
            }
            var ml = Path.Combine(dir, "ModLoader");
            if (!state.GetBool("Install", "HadModLoader", false) && Directory.Exists(ml))
            {
                Directory.Delete(ml, true);
                log(T.Get("log_delete", "ModLoader\\"));
            }
            var redist = Path.Combine(dir, Game.PatchDir, "redist");
            if (Directory.Exists(redist))
                Directory.Delete(redist, true);
            WriteList(dir, files);
        }

        /// <summary>The language the original game was set to (GOG writes it to the registry), else Windows'.</summary>
        static int OriginalLanguage(int fallback)
        {
            string[] keys =
            {
                @"Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Ballance\Settings",
                @"Software\Classes\VirtualStore\MACHINE\SOFTWARE\Ballance\Settings",
            };
            foreach (var k in keys)
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(k))
                    {
                        var v = key == null ? null : key.GetValue("language");
                        if (v is int && (int)v >= 0 && (int)v <= 4)
                            return (int)v;
                    }
                }
                catch (Exception)
                {
                }
            }
            try
            {
                using (var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var key = hk.OpenSubKey(@"SOFTWARE\Ballance\Settings"))
                {
                    var v = key == null ? null : key.GetValue("language");
                    if (v is int && (int)v >= 0 && (int)v <= 4)
                        return (int)v;
                }
            }
            catch (Exception)
            {
            }
            return fallback;
        }

        static void BackUp(string dir, string backup, string rel)
        {
            var src = Path.Combine(dir, rel);
            var dst = Path.Combine(backup, rel);
            if (!File.Exists(src) || File.Exists(dst))
                return;     // the first backup is the original: never overwrite it
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, false);
        }

        static IEnumerable<string> ReadList(string dir)
        {
            var p = Path.Combine(dir, FileList);
            return File.Exists(p) ? File.ReadAllLines(p).Where(l => l.Trim().Length > 0) : Enumerable.Empty<string>();
        }

        static void WriteList(string dir, IEnumerable<string> files)
        {
            var p = Path.Combine(dir, FileList);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllLines(p, files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

        static string StartMenuDir()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        }

        static void Register(string dir)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "Ballance - HD Patch");
                k.SetValue("DisplayVersion", Version.Text);
                k.SetValue("Publisher", "CompiWare");
                k.SetValue("InstallLocation", dir);
                k.SetValue("DisplayIcon", Path.Combine(dir, Game.LauncherExe));
                k.SetValue("UninstallString", "\"" + Path.Combine(dir, Game.PatchDir, SetupCopy) + "\" /uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", 40000, RegistryValueKind.DWord);
            }
        }

        // ---- uninstall -------------------------------------------------------------------------------

        public void Uninstall(string dir)
        {
            if (Game.IsRunning(dir))
                throw new InvalidOperationException(T.Get("err_running"));
            if (!IsInstalled(dir))
                throw new InvalidOperationException(T.Get("err_nobackup"));
            var state = IniDoc.Load(Path.Combine(dir, StateIni));
            var backup = Path.Combine(dir, Game.BackupDir);

            // 1. everything the patch wrote (both states of the widescreen mod)
            foreach (var rel in ReadList(dir))
            {
                Delete(Path.Combine(dir, rel));
                if (rel.EndsWith(".mod.as", StringComparison.OrdinalIgnoreCase))
                    Delete(Path.Combine(dir, rel + ".off"));
            }
            // 2. run-time files that were not there before
            foreach (var f in RuntimeFiles)
                if (!state.GetBool("Existed", f, false))
                    Delete(Path.Combine(dir, f));
            // 3. the mod loader folder (configs, logs, cache) if the patch created it
            var ml = Path.Combine(dir, "ModLoader");
            if (!state.GetBool("Install", "HadModLoader", false) && Directory.Exists(ml))
            {
                Directory.Delete(ml, true);
                log(T.Get("log_delete", "ModLoader\\"));
            }
            // 4. originals back
            if (Directory.Exists(backup))
            {
                foreach (var b in Directory.GetFiles(backup, "*", SearchOption.AllDirectories))
                {
                    var rel = b.Substring(backup.Length + 1);
                    var target = Path.Combine(dir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    CopyRetrying(b, target);
                    log(T.Get("log_restore", rel));
                }
            }
            // 4b. the plugin's own folder (settings, log, glyphs, font)
            var pluginDir = Path.Combine(dir, Game.PluginDir);
            if (Directory.Exists(pluginDir))
            {
                Directory.Delete(pluginDir, true);
                log(T.Get("log_delete", Game.PluginDir + "\\"));
            }
            // 5. empty folders the patch left behind
            foreach (var d in new[] { @"ModLoader\Mods", @"ModLoader\Fonts", @"ModLoader\Configs", "ModLoader" })
            {
                var p = Path.Combine(dir, d);
                if (Directory.Exists(p) && !Directory.EnumerateFileSystemEntries(p).Any())
                    Directory.Delete(p);
            }
            // 6. shortcuts and the Apps & Features entry are shared by name: only remove them when they belong here
            Delete(Path.Combine(dir, ShortcutName));
            DeleteShortcut(Path.Combine(StartMenuDir(), ShortcutName), dir);
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName), dir);
            foreach (var name in UninstallShortcuts)
                DeleteShortcut(Path.Combine(StartMenuDir(), name), dir);
            var patch = Path.Combine(dir, Game.PatchDir);
            try
            {
                if (Directory.Exists(patch))
                    Directory.Delete(patch, true);
            }
            catch (Exception ex)
            {
                // only the folder of a still-running uninstaller copy can be locked; the game is clean
                log(T.Get("log_leftover", patch, ex.Message));
            }
            if (RegisteredFor(dir))
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            log(T.Get("log_uninstalled"));
        }

        /// <summary>The launcher that started the uninstall may still be closing: give it a few seconds.</summary>
        static void CopyRetrying(string from, string to)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(to))
                        File.SetAttributes(to, FileAttributes.Normal);
                    File.Copy(from, to, true);
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= 25)
                        throw;
                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        void DeleteShortcut(string lnk, string dir)
        {
            try
            {
                if (File.Exists(lnk) && IsInside(Shortcut.Target(lnk), dir))
                    Delete(lnk);
            }
            catch (Exception)
            {
            }
        }

        static bool RegisteredFor(string dir)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                return k != null && IsInside(k.GetValue("InstallLocation") as string, dir);
        }

        static bool IsInside(string path, string dir)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            var root = Path.GetFullPath(dir).TrimEnd('\\') + '\\';
            return (Path.GetFullPath(path).TrimEnd('\\') + '\\').StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        void Delete(string path)
        {
            for (int attempt = 0; File.Exists(path); attempt++)
            {
                try
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    log(T.Get("log_delete", Path.GetFileName(path)));
                }
                catch (IOException)
                {
                    if (attempt >= 25)
                        throw;
                    System.Threading.Thread.Sleep(200);    // the launcher may still be closing
                }
            }
        }

        // ---- helpers ---------------------------------------------------------------------------------

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool DeleteFile(string path);

        /// <summary>Drop the "downloaded from the internet" mark so Windows loads the DLLs without prompts.</summary>
        static void Unblock(string path)
        {
            DeleteFile(path + ":Zone.Identifier");
        }

        public static bool CanWrite(string dir)
        {
            try
            {
                var probe = Path.Combine(dir, Game.BinDir, "_hdpatch_write_test.tmp");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsElevated()
        {
            using (var id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
