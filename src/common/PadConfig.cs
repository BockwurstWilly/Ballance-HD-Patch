using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BallanceHD
{
    /// <summary>
    /// Controller settings shared by the game plugin and the launcher: BallanceHD\Settings.ini ([Controller], [Buttons]).
    /// The plugin shows them in the game next to Options - Controls (and saves changes made there), the launcher on
    /// its "Layout" page. Versions up to 1.3.2 kept them in ModLoader\Configs\compiware.ballancehd.cfg: read once
    /// when the new file has none yet.
    /// </summary>
    class PadConfig
    {
        // button names as stored in the file (and shown in the game)
        public static readonly string[] Buttons =
        {
            "A / Cross", "B / Circle", "X / Square", "Y / Triangle", "LB / L1", "RB / R1", "LT / L2", "RT / R2",
            "Start / Options", "Back / Share", "L3", "R3", "-",
        };
        // glyph file names (BallanceHD\glyphs\<pad>_<glyph>.tga, launcher resources) per button
        public static readonly string[] Glyphs = { "a", "b", "x", "y", "lb", "rb", "lt", "rt", "start", "back", "l3", "r3", "" };

        public class Action
        {
            public readonly string Key, De, En, Default;
            public Action(string key, string de, string en, string def) { Key = key; De = de; En = en; Default = def; }
            public string Label { get { return T.German ? De : En; } }
        }

        public static readonly Action[] Actions =
        {
            new Action("Confirm", "Bestätigen", "Confirm", "A / Cross"),
            new Action("Back", "Zurück", "Back", "B / Circle"),
            new Action("Pause", "Pause-Menü", "Pause menu", "Start / Options"),
            new Action("CameraLift", "Kamera anheben", "Raise camera", "Y / Triangle"),
            new Action("CameraLeft", "Kamera 90° links", "Camera 90° left", "LB / L1"),
            new Action("CameraRight", "Kamera 90° rechts", "Camera 90° right", "RB / R1"),
            new Action("SkipTutorial", "Einführung überspringen", "Skip tutorial", "X / Square"),
            new Action("CameraDistance", "Kameraabstand wechseln", "Cycle camera distance", "Back / Share"),
        };

        public readonly Dictionary<string, string> Map = new Dictionary<string, string>();
        public bool AnalogRoll = true;
        public bool FreeCamera = true;
        public bool InvertY;
        public int CameraSpeed = 100;      // percent
        public int CameraDistance = 1;     // 0 near, 1 normal, 2 far, 3 very far

        public static PadConfig Load(string dir)
        {
            var c = new PadConfig();
            var ini = IniDoc.Load(Path.Combine(dir, Game.PluginIni));
            if (ini.Get("Buttons", "Confirm") == null && File.Exists(Path.Combine(dir, Game.OldPadCfg)))
            {
                var old = BmlCfg.Load(Path.Combine(dir, Game.OldPadCfg));
                foreach (var a in Actions)
                    c.Map[a.Key] = Valid(old.Get("Buttons", a.Key), a.Default);
                c.AnalogRoll = old.GetBool("Controller", "AnalogRoll", c.AnalogRoll);
                c.FreeCamera = old.GetBool("Controller", "FreeCamera", c.FreeCamera);
                c.InvertY = old.GetBool("Controller", "InvertY", c.InvertY);
                c.CameraSpeed = Math.Max(25, Math.Min(300, old.GetInt("Controller", "CameraSpeed", c.CameraSpeed)));
                c.CameraDistance = Math.Max(0, Math.Min(3, old.GetInt("Controller", "CameraDistance", c.CameraDistance)));
                return c;
            }
            foreach (var a in Actions)
                c.Map[a.Key] = Valid(ini.Get("Buttons", a.Key), a.Default);
            c.AnalogRoll = ini.GetBool("Controller", "AnalogRoll", c.AnalogRoll);
            c.FreeCamera = ini.GetBool("Controller", "FreeCamera", c.FreeCamera);
            c.InvertY = ini.GetBool("Controller", "InvertY", c.InvertY);
            c.CameraSpeed = Math.Max(25, Math.Min(300, ini.GetInt("Controller", "CameraSpeed", c.CameraSpeed)));
            c.CameraDistance = Math.Max(0, Math.Min(3, ini.GetInt("Controller", "CameraDistance", c.CameraDistance)));
            return c;
        }

        static string Valid(string v, string fallback)
        {
            return v != null && Array.IndexOf(Buttons, v.Trim()) >= 0 ? v.Trim() : fallback;
        }

        public void Save(string dir)
        {
            Directory.CreateDirectory(Path.Combine(dir, Game.PluginDir));
            var ini = IniDoc.Load(Path.Combine(dir, Game.PluginIni));
            foreach (var a in Actions)
                ini.Set("Buttons", a.Key, Map[a.Key]);
            ini.Set("Controller", "AnalogRoll", AnalogRoll);
            ini.Set("Controller", "FreeCamera", FreeCamera);
            ini.Set("Controller", "InvertY", InvertY);
            ini.Set("Controller", "CameraSpeed", CameraSpeed);
            ini.Set("Controller", "CameraDistance", CameraDistance);
            ini.Save();
        }
        public static void Reset(PadConfig c)
        {
            foreach (var a in Actions)
                c.Map[a.Key] = a.Default;
        }

        /// <summary>The pad button bits of a stored button name (triggers get two extra bits).</summary>
        public static int Mask(string button)
        {
            switch (button)
            {
                case "A / Cross": return Pads.A;
                case "B / Circle": return Pads.B;
                case "X / Square": return Pads.X;
                case "Y / Triangle": return Pads.Y;
                case "LB / L1": return Pads.LB;
                case "RB / R1": return Pads.RB;
                case "LT / L2": return Pads.LTrigger;
                case "RT / R2": return Pads.RTrigger;
                case "Start / Options": return Pads.Start;
                case "Back / Share": return Pads.Back;
                case "L3": return Pads.LThumb;
                case "R3": return Pads.RThumb;
            }
            return 0;
        }

        public static string Glyph(string button)
        {
            int i = Array.IndexOf(Buttons, button);
            return i >= 0 ? Glyphs[i] : "";
        }

        public int MaskOf(string action)
        {
            string b;
            return Map.TryGetValue(action, out b) ? Mask(b) : 0;
        }
    }
}
