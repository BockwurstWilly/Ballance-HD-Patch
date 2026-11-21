using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BallanceHD
{
    /// <summary>
    /// Controller settings shared by the game mod and the launcher: ModLoader\Configs\compiware.ballancehd.cfg.
    /// The mod shows them in the game (Options - Mods - Ballance HD), the launcher on its "Layout" page,
    /// the bridge reads them while the game runs (and again when the file changes).
    /// </summary>
    class PadConfig
    {
        public const string File = @"ModLoader\Configs\compiware.ballancehd.cfg";

        // button names as stored in the file (and shown in the game's mod menu)
        public static readonly string[] Buttons =
        {
            "A / Cross", "B / Circle", "X / Square", "Y / Triangle", "LB / L1", "RB / R1", "LT / L2", "RT / R2",
            "Start / Options", "Back / Share", "L3", "R3", "-",
        };
        // glyph file names (ModLoader\BallanceHD\glyphs\<pad>_<glyph>.tga, launcher resources) per button
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
            new Action("CameraLift", "Kamera anheben (halten)", "Raise camera (hold)", "Y / Triangle"),
            new Action("CameraLeft", "Kamera 90° links", "Camera 90° left", "LB / L1"),
            new Action("CameraRight", "Kamera 90° rechts", "Camera 90° right", "RB / R1"),
            new Action("SkipTutorial", "Einführung überspringen", "Skip tutorial", "X / Square"),
            new Action("Respawn", "Kugel neu setzen", "Respawn the ball", "Back / Share"),
        };

        public readonly Dictionary<string, string> Map = new Dictionary<string, string>();
        public bool AnalogRoll = true;
        public bool FreeCamera = true;
        public bool InvertY;
        public int CameraSpeed = 100;      // percent

        public static PadConfig Load(string dir)
        {
            var c = new PadConfig();
            var cfg = BmlCfg.Load(Path.Combine(dir, File));
            foreach (var a in Actions)
            {
                var v = cfg.Get("Buttons", a.Key);
                c.Map[a.Key] = v != null && Array.IndexOf(Buttons, v.Trim()) >= 0 ? v.Trim() : a.Default;
            }
            c.AnalogRoll = cfg.GetBool("Controller", "AnalogRoll", c.AnalogRoll);
            c.FreeCamera = cfg.GetBool("Controller", "FreeCamera", c.FreeCamera);
            c.InvertY = cfg.GetBool("Controller", "InvertY", c.InvertY);
            c.CameraSpeed = Math.Max(25, Math.Min(300, cfg.GetInt("Controller", "CameraSpeed", c.CameraSpeed)));
            return c;
        }

        public void Save(string dir)
        {
            var cfg = BmlCfg.Load(Path.Combine(dir, File));
            foreach (var a in Actions)
                cfg.Set("Buttons", 'S', a.Key, Map[a.Key]);
            cfg.SetBool("Controller", "AnalogRoll", AnalogRoll);
            cfg.SetBool("Controller", "FreeCamera", FreeCamera);
            cfg.SetBool("Controller", "InvertY", InvertY);
            cfg.SetInt("Controller", "CameraSpeed", CameraSpeed);
            cfg.Save();
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
