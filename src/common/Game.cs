using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace BallanceHD
{
    class GameCheck
    {
        public bool Ok;
        public string Release = "";             // text for the UI
        public bool KnownRenderEngine;
        public bool VcRuntime;
        public readonly List<string> Problems = new List<string>();
    }

    static class Game
    {
        public const string PlayerExe = @"Bin\Player.exe";
        public const string PlayerIni = @"Bin\Player.ini";
        public const string BinDir = "Bin";
        public const string LauncherExe = "BallanceHDLauncher.exe";
        public const string PatchDir = "_HDPatch";
        public const string BackupDir = @"_HDPatch\backup";
        public const string SettingsIni = @"_HDPatch\BallanceHD.ini";
        public const string RenderIniPath = @"RenderEngines\CK2_3D.ini";
        public const string BmlCfgPath = @"ModLoader\Configs\BML.cfg";
        public const string ModFile = @"ModLoader\Mods\BallanceHD.mod.as";
        public const string GogId = "1918420377";
        public const string SteamId = "2000770";

        // CK2_3D.dll of the GOG/Steam release: BML+'s widescreen projection hook is made for exactly this build
        const string KnownCk2Md5 = "BE7692ACAB0C16C8FFC5FDC1D3ABBB15";

        static readonly string[] Required = { "base.cmo", "Database.tdb", @"Bin\Player.exe", @"Bin\CK2.dll", @"Bin\VxMath.dll", @"RenderEngines\CK2_3D.dll", @"RenderEngines\CK2_3D.ini" };

        public static GameCheck Check(string dir)
        {
            var c = new GameCheck();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                c.Problems.Add(T.Get("chk_nodir"));
                return c;
            }
            foreach (var f in Required)
                if (!File.Exists(Path.Combine(dir, f)))
                    c.Problems.Add(T.Get("chk_missing", f));
            foreach (var d in new[] { "BuildingBlocks", "Managers", "Plugins", "Textures", "3D Entities" })
                if (!Directory.Exists(Path.Combine(dir, d)))
                    c.Problems.Add(T.Get("chk_missing", d + "\\"));
            c.Ok = c.Problems.Count == 0;
            if (!c.Ok)
                return c;
            if (File.Exists(Path.Combine(dir, "goggame-" + GogId + ".info")))
                c.Release = T.Get("chk_gog");
            else if (File.Exists(Path.Combine(dir, "steam_appid.txt")) || dir.IndexOf("steamapps", StringComparison.OrdinalIgnoreCase) >= 0)
                c.Release = T.Get("chk_steam");
            else
                c.Release = T.Get("chk_other");
            c.KnownRenderEngine = Md5(Path.Combine(dir, @"RenderEngines\CK2_3D.dll")) == KnownCk2Md5;
            c.VcRuntime = HasVcRuntime();
            return c;
        }

        static string Md5(string path)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var s = File.OpenRead(path))
                    return BitConverter.ToString(md5.ComputeHash(s)).Replace("-", "");
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>BML+ needs the x86 Visual C++ 2015-2022 runtime.</summary>
        public static bool HasVcRuntime()
        {
            var sys = Environment.Is64BitOperatingSystem
                ? Environment.GetFolderPath(Environment.SpecialFolder.SystemX86)
                : Environment.GetFolderPath(Environment.SpecialFolder.System);
            return File.Exists(Path.Combine(sys, "vcruntime140.dll")) && File.Exists(Path.Combine(sys, "msvcp140.dll"));
        }

        public static bool IsGameDir(string dir)
        {
            return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "base.cmo")) && File.Exists(Path.Combine(dir, PlayerExe));
        }

        public static string FindInstallDir()
        {
            foreach (var d in Candidates())
            {
                try
                {
                    if (IsGameDir(d))
                        return Path.GetFullPath(d).TrimEnd('\\');
                }
                catch (Exception)
                {
                }
            }
            return null;
        }

        static IEnumerable<string> Candidates()
        {
            // the launcher sits in the game folder
            yield return AppDomain.CurrentDomain.BaseDirectory;
            yield return RegValue(@"SOFTWARE\GOG.com\Games\" + GogId, "path");
            yield return RegValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + GogId + "_is1", "InstallLocation");
            yield return RegValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App " + SteamId, "InstallLocation");
            foreach (var lib in SteamLibraries())
                yield return Path.Combine(lib, @"steamapps\common\Ballance");
            // shortcuts that point at Bin\Player.exe (GOG puts one on the public desktop)
            foreach (var desk in new[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) })
            {
                string[] links = new string[0];
                try { links = Directory.GetFiles(desk, "*Ballance*.lnk"); } catch (Exception) { }
                foreach (var lnk in links)
                {
                    string target = null;
                    try { target = Shortcut.Target(lnk); } catch (Exception) { }
                    if (!string.IsNullOrEmpty(target) && target.EndsWith(@"\Bin\Player.exe", StringComparison.OrdinalIgnoreCase))
                        yield return Path.GetDirectoryName(Path.GetDirectoryName(target));
                }
            }
            foreach (var root in new[] { @"C:\GOG Games\Ballance", @"C:\Program Files (x86)\GOG Galaxy\Games\Ballance", @"C:\Program Files (x86)\Ballance", @"C:\Program Files\Ballance", @"D:\GOG Games\Ballance" })
                yield return root;
        }

        static string RegValue(string key, string name)
        {
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using (var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var k = hk.OpenSubKey(key))
                    {
                        var v = k == null ? null : k.GetValue(name) as string;
                        if (!string.IsNullOrEmpty(v))
                            return v;
                    }
                }
                catch (Exception)
                {
                }
            }
            return null;
        }

        static IEnumerable<string> SteamLibraries()
        {
            var list = new List<string>();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    var steam = k == null ? null : k.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steam))
                    {
                        steam = steam.Replace('/', '\\');
                        list.Add(steam);
                        var vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                        if (File.Exists(vdf))
                            foreach (var line in File.ReadAllLines(vdf))
                            {
                                var t = line.Trim();
                                if (t.StartsWith("\"path\""))
                                    list.Add(t.Substring(6).Trim().Trim('"').Replace(@"\\", @"\"));
                            }
                    }
                }
            }
            catch (Exception)
            {
            }
            return list;
        }

        public static bool IsRunning(string dir)
        {
            return RunningProcess(dir) != null;
        }

        public static Process RunningProcess(string dir)
        {
            var exe = Path.GetFullPath(Path.Combine(dir, PlayerExe));
            foreach (var p in Process.GetProcessesByName("Player"))
            {
                try
                {
                    if (string.Equals(QueryPath(p), exe, StringComparison.OrdinalIgnoreCase))
                        return p;
                }
                catch (Exception)
                {
                }
            }
            return null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool QueryFullProcessImageName(IntPtr process, int flags, System.Text.StringBuilder name, ref int size);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        /// <summary>Works for 32-bit processes from a 64-bit caller and without admin rights.</summary>
        static string QueryPath(Process p)
        {
            var h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, p.Id);
            if (h == IntPtr.Zero)
                return null;
            try
            {
                var sb = new System.Text.StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
            }
            finally
            {
                CloseHandle(h);
            }
        }
    }

    /// <summary>Launcher choices (_HDPatch\BallanceHD.ini) plus the game settings they are written to.</summary>
    class HdSettings
    {
        public string UiLanguage = "auto";
        public string Display = "";          // device name, empty = primary
        public string Mode = "borderless";   // borderless | fullscreen | window
        public int Width, Height;            // fullscreen / window; 0 = native
        public int Msaa = 4;                 // 0, 2, 4, 8
        public int Fps = -1;                 // -1 = refresh rate, 0 = unlimited
        public bool Widescreen = true;
        public bool Pad = true;
        public int Deadzone = 35;            // percent of the stick range
        public bool Prompts = true;          // controller button prompts in the game
        // game
        public int GameLanguage = 1;         // 0 de, 1 en, 2 es, 3 it, 4 fr
        public bool SkipIntro = true;
        public bool ShowFps;
        public bool ShowTitle;
        public bool LifeBallFix = true;

        public static HdSettings Load(string dir)
        {
            var s = new HdSettings();
            s.GameLanguage = T.WindowsGerman ? 0 : 1;
            var ini = IniDoc.Load(Path.Combine(dir, Game.SettingsIni));
            s.UiLanguage = ini.Get("Launcher", "Language", s.UiLanguage);
            s.Display = ini.Get("Graphics", "Display", s.Display);
            s.Mode = ini.Get("Graphics", "Mode", s.Mode).ToLowerInvariant();
            if (s.Mode != "fullscreen" && s.Mode != "window")
                s.Mode = "borderless";
            s.Width = ini.GetInt("Graphics", "Width", 0);
            s.Height = ini.GetInt("Graphics", "Height", 0);
            s.Msaa = ini.GetInt("Graphics", "Antialias", s.Msaa);
            s.Fps = ini.GetInt("Graphics", "FrameRate", s.Fps);
            s.Widescreen = ini.GetBool("Graphics", "Widescreen", s.Widescreen);
            s.Pad = ini.GetBool("Controller", "Enabled", s.Pad);
            s.Deadzone = Math.Max(10, Math.Min(80, ini.GetInt("Controller", "DeadZone", s.Deadzone)));
            s.Prompts = ini.GetBool("Controller", "ButtonPrompts", s.Prompts);

            // the game's own files are the truth for game options
            var player = IniDoc.Load(Path.Combine(dir, Game.PlayerIni));
            s.GameLanguage = Math.Max(0, Math.Min(4, player.GetInt("Game", "Language", s.GameLanguage)));
            s.SkipIntro = player.GetBool("Game", "SkipOpening", s.SkipIntro);
            var bml = BmlCfg.Load(Path.Combine(dir, Game.BmlCfgPath));
            s.ShowFps = bml.GetBool("HUD", "ShowFPS", s.ShowFps);
            s.ShowTitle = bml.GetBool("HUD", "ShowTitle", s.ShowTitle);
            s.LifeBallFix = bml.GetBool("Tweak", "FixLifeBallFreeze", s.LifeBallFix);
            return s;
        }

        /// <summary>The pixel size the game will run at, given the chosen display.</summary>
        public Size2 Resolution(DisplayInfo d)
        {
            if (Mode == "borderless" || Width <= 0 || Height <= 0)
            {
                if (Mode == "window")
                {
                    // a window that leaves room for the task bar and title
                    int w = Math.Min(1280, d.Width * 3 / 4);
                    return new Size2(w, w * d.Height / d.Width);
                }
                return new Size2(d.Width, d.Height);
            }
            return new Size2(Width, Height);
        }

        /// <summary>
        /// Automatic in a window / borderless: VSync (BML+ "SetMaxFrameRate 0"; measured: an even 16.67 ms at 60 Hz,
        /// while its 60 fps limiter gives 17.3 ms frames = 57.5 fps = a repeated frame every 0.4 s, micro-stutter).
        /// The game runs at the same speed at any frame rate (measured 30 / 60 / 97 fps), so VSync at 48 or 144 Hz
        /// is fine. Exclusive fullscreen has no working VSync under Direct3D 8: there the limiter at the refresh rate.
        /// </summary>
        public bool UsesVSync { get { return Fps < 0 && Mode != "fullscreen"; } }

        public int EffectiveFps(DisplayInfo d)
        {
            return Fps < 0 ? d.Hz : Fps;
        }

        public void Save(string dir)
        {
            var path = Path.Combine(dir, Game.SettingsIni);
            var ini = IniDoc.Load(path);
            ini.Set("Launcher", "Language", UiLanguage);
            ini.Set("Graphics", "Display", Display ?? "");
            ini.Set("Graphics", "Mode", Mode);
            ini.Set("Graphics", "Width", Width);
            ini.Set("Graphics", "Height", Height);
            ini.Set("Graphics", "Antialias", Msaa);
            ini.Set("Graphics", "FrameRate", Fps);
            ini.Set("Graphics", "Widescreen", Widescreen);
            ini.Set("Controller", "Enabled", Pad);
            ini.Set("Controller", "DeadZone", Deadzone);
            ini.Set("Controller", "ButtonPrompts", Prompts);
            ini.Save();
        }

        /// <summary>Writes everything into the game's files: Player.ini, BML.cfg, CK2_3D.ini, the widescreen mod.</summary>
        public void Apply(string dir)
        {
            var all = Displays.All();
            var d = Displays.Pick(all, Display);
            var res = Resolution(d);

            var player = IniDoc.Load(Path.Combine(dir, Game.PlayerIni));
            player.Set("Startup", "LogMode", 1);
            player.Set("Startup", "Verbose", 0);
            player.Set("Startup", "ManualSetup", 0);
            player.Set("Graphics", "Driver", 0);
            player.Set("Graphics", "BitsPerPixel", 32);
            player.Set("Graphics", "Width", res.W);
            player.Set("Graphics", "Height", res.H);
            player.Set("Graphics", "FullScreen", Mode == "fullscreen");
            player.Set("Window", "ChildWindowRendering", 0);
            player.Set("Window", "Borderless", Mode == "borderless");
            player.Set("Window", "ClipCursor", 0);
            player.Set("Window", "AlwaysHandleInput", 0);
            if (Mode == "borderless")
            {
                player.Set("Window", "X", d.X);
                player.Set("Window", "Y", d.Y);
            }
            else
            {
                // centred by the player
                player.Set("Window", "X", int.MaxValue);
                player.Set("Window", "Y", int.MaxValue);
            }
            player.Set("Game", "Language", GameLanguage);
            player.Set("Game", "SkipOpening", SkipIntro);
            player.Set("Game", "ApplyHotfix", 1);
            player.Set("Game", "UnlockFramerate", 0);
            player.Set("Game", "UnlockWidescreen", 1);
            player.Set("Game", "UnlockHighResolution", 1);
            player.Set("Game", "Debug", 0);
            player.Set("Game", "Rookie", 0);
            player.Save();

            var bml = BmlCfg.Load(Path.Combine(dir, Game.BmlCfgPath));
            int fps = EffectiveFps(d);
            bml.SetBool("Graphics", "WidescreenFix", Widescreen);
            bml.SetBool("Graphics", "UnlockFrameRate", !UsesVSync && fps == 0);
            bml.SetInt("Graphics", "SetMaxFrameRate", UsesVSync ? 0 : fps == 0 ? 240 : fps);
            bml.SetBool("HUD", "ShowTitle", ShowTitle);
            bml.SetBool("HUD", "ShowFPS", ShowFps);
            bml.SetBool("Tweak", "FixLifeBallFreeze", LifeBallFix);
            bml.Save();

            RenderIni.Set(Path.Combine(dir, Game.RenderIniPath), new Dictionary<string, string>
            {
                { "Antialias", Msaa.ToString() },
                { "TextureVideoFormat", "_32_ARGB8888" },
                { "SpriteVideoFormat", "_32_ARGB8888" },
            });

            // the mod is always on; what it does is switched by marker files it checks (see BallanceHD.mod.as)
            var on = Path.Combine(dir, Game.ModFile);
            var off = on + ".off";
            if (File.Exists(off))
            {
                if (File.Exists(on))
                    File.Delete(off);
                else
                    File.Move(off, on);       // 1.0.0 switched the widescreen fix by renaming the mod
            }
            PadMarker.SetFlag(dir, "wide_off", !Widescreen);
            PadMarker.SetFlag(dir, "hints_off", !Prompts || !Pad);
            PadMarker.SetFlag(dir, "lang_de", GameLanguage == 0);
        }
    }

    /// <summary>
    /// Small marker files in ModLoader\BallanceHD that the game mod polls (it can only test whether a file exists):
    /// which controller is connected (pad_xbox / pad_ps, kept up to date by the launcher while the game runs),
    /// the language of the prompts and what is switched off.
    /// </summary>
    static class PadMarker
    {
        public const string Dir = @"ModLoader\BallanceHD";

        public static void SetFlag(string dir, string name, bool on)
        {
            try
            {
                var p = Path.Combine(dir, Dir, name);
                if (on && !File.Exists(p))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(p));
                    File.WriteAllText(p, "");
                }
                else if (!on && File.Exists(p))
                {
                    File.Delete(p);
                }
            }
            catch (Exception)
            {
                // a marker that cannot be written only costs the prompts
            }
        }

        public static void SetPad(string dir, PadKind kind)
        {
            SetFlag(dir, "pad_xbox", kind == PadKind.Xbox);
            SetFlag(dir, "pad_ps", kind == PadKind.PlayStation);
        }
    }

    static class Shortcut
    {
        public static void Create(string lnk, string target, string workDir, string icon, string arguments = "", string description = "Ballance - HD Launcher")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnk));
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                dynamic sc = shell.CreateShortcut(lnk);
                sc.TargetPath = target;
                sc.Arguments = arguments;
                sc.WorkingDirectory = workDir;
                sc.IconLocation = icon + ",0";
                sc.Description = description;
                sc.Save();
                Marshal.FinalReleaseComObject(sc);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        public static string Target(string lnk)
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                dynamic sc = shell.CreateShortcut(lnk);
                string target = sc.TargetPath;
                Marshal.FinalReleaseComObject(sc);
                return target;
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
