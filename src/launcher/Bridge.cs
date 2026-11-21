using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace BallanceHD
{
    /// <summary>
    /// Ballance only knows the keyboard: this turns controller input into the game's default keys while the
    /// game window is in front (scan codes, which is what DirectInput reads), and hands the sticks to the game
    /// mod through SharedPad. The mod reports back whether a level is running, so in a level the sticks are
    /// left to it (stepless rolling, free camera) and only the menus get arrow keys from the left stick.
    ///   left stick              arrows in menus; in a level: stepless rolling (mod) - or arrows without the mod
    ///   d-pad                   arrows (menus, digital rolling)
    ///   right stick             free camera (mod); without it: 90° steps / raise
    ///   buttons                 as configured (Options - Controls in the game, or the launcher)
    /// </summary>
    sealed class Bridge
    {
        const ushort KUp = 0x148, KDown = 0x150, KLeft = 0x14B, KRight = 0x14D;   // extended scan codes
        const ushort KShift = 0x2A, KSpace = 0x39, KEnter = 0x1C, KEsc = 0x01, KQ = 0x10, KR = 0x13;

        readonly Process game;
        readonly string gameDir;
        readonly int deadzone;     // 0..32767
        readonly bool[] down = new bool[0x200];
        PadConfig cfg;
        DateTime cfgStamp;
        int cfgCheck;
        PadState last;
        int camStep;               // camera rotation sequence: 0 idle, else steps left
        ushort camKey;
        DateTime camNext;
        int camDir;                // direction the right stick currently pushes (-1, 0, 1); a push rotates once
        SharedPad shared;

        public Bridge(Process game, string dir, int deadzonePercent)
        {
            this.game = game;
            gameDir = dir;
            deadzone = 32767 * deadzonePercent / 100;
            LoadConfig();
        }

        void LoadConfig()
        {
            cfg = PadConfig.Load(gameDir);
            try { cfgStamp = File.GetLastWriteTimeUtc(Path.Combine(gameDir, PadConfig.File)); } catch (Exception) { }
        }

        /// <summary>The game saves the mod menu's settings to the config file: pick them up while playing.</summary>
        void CheckConfig()
        {
            if (++cfgCheck < 60)
                return;
            cfgCheck = 0;
            try
            {
                if (File.GetLastWriteTimeUtc(Path.Combine(gameDir, PadConfig.File)) != cfgStamp)
                    LoadConfig();
            }
            catch (Exception)
            {
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

        void Key(ushort scan, bool press)
        {
            if (down[scan] == press)
                return;
            down[scan] = press;
            var i = new INPUT { type = 1 };
            i.u.ki.wScan = (ushort)(scan & 0xFF);
            i.u.ki.dwFlags = 0x0008u /* SCANCODE */ | (press ? 0u : 0x0002u /* KEYUP */) | ((scan & 0x100) != 0 ? 0x0001u /* EXTENDED */ : 0u);
            SendInput(1, new[] { i }, Marshal.SizeOf(typeof(INPUT)));
        }

        void ReleaseAll()
        {
            for (ushort k = 0; k < down.Length; k++)
                if (down[k])
                    Key(k, false);
            camStep = 0;
        }

        bool GameInFront()
        {
            uint pid;
            GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            return pid == (uint)game.Id;
        }

        /// <summary>Runs until the game has exited.</summary>
        public void Run(Pads pads)
        {
            timeBeginPeriod(1);
            try
            {
                try { shared = new SharedPad(); } catch (Exception) { }
                var kind = (PadKind)(-1);
                while (!game.HasExited)
                {
                    var s = pads.Poll();
                    if (pads.Kind != kind)
                    {
                        kind = pads.Kind;
                    }
                    CheckConfig();
                    bool front = GameInFront();
                    if (shared != null)
                        shared.Write(front ? s : new PadState(), kind);
                    if (front)
                        Update(s);
                    else
                        ReleaseAll();
                    last = s;
                    Thread.Sleep(8);
                }
            }
            finally
            {
                ReleaseAll();
                if (shared != null)
                    shared.Dispose();
                PadMarker.SetPad(gameDir, PadKind.None);
                timeEndPeriod(1);
            }
        }

        bool Pressed(PadState s, int mask) { return s.HasMask(mask) && !last.HasMask(mask); }

        bool Mod(int state) { return shared != null && shared.Has(state); }

        /// <summary>While the game runs, the mod's settings are the current ones (they can be changed in the game).</summary>
        int MaskOf(string action)
        {
            if (shared != null && shared.ModAlive)
                for (int i = 0; i < PadConfig.Actions.Length; i++)
                    if (PadConfig.Actions[i].Key == action)
                        return shared.Mask(i);
            return cfg.MaskOf(action);
        }

        void Update(PadState s)
        {
            // the mod's settings panel (Options - Controls) has the controller: the game gets nothing
            if (Mod(SharedPad.StatePanel))
            {
                ReleaseAll();
                return;
            }
            bool playing = Mod(SharedPad.StateInLevel) && !Mod(SharedPad.StatePaused);
            bool freeCam = shared != null && shared.ModAlive ? Mod(SharedPad.StateFreeCam) : false;
            bool analog = playing && Mod(SharedPad.StateAnalog);

            // --- camera rotation in 90° steps: Shift down, arrow tapped, Shift up (one step per push) ---
            int dir = 0;
            if (!freeCam)
            {
                // without the free camera the right stick does what the shoulder buttons do
                if (s.RX < -deadzone * 2 && Math.Abs((int)s.RX) > Math.Abs((int)s.RY)) dir = -1;
                else if (s.RX > deadzone * 2 && Math.Abs((int)s.RX) > Math.Abs((int)s.RY)) dir = 1;
                else if (Math.Abs((int)s.RX) < deadzone) dir = 0;
                else dir = camDir;
            }
            bool rotLeft = (dir == -1 && camDir != -1) || Pressed(s, MaskOf("CameraLeft"));
            bool rotRight = (dir == 1 && camDir != 1) || Pressed(s, MaskOf("CameraRight"));
            camDir = dir;
            if (camStep == 0 && (rotLeft || rotRight))
            {
                camKey = rotLeft ? KLeft : KRight;
                camStep = 4;
                camNext = DateTime.UtcNow;
            }
            if (camStep > 0)
            {
                StepCamera();
                return;     // movement keys resume once the rotation is through
            }

            // --- menus and digital rolling; in a level with stepless rolling the left stick belongs to the mod ---
            bool stick = !analog;
            bool up = s.Has(Pads.DPadUp) || (stick && s.LY > deadzone);
            bool dn = s.Has(Pads.DPadDown) || (stick && s.LY < -deadzone);
            bool lf = s.Has(Pads.DPadLeft) || (stick && s.LX < -deadzone);
            bool rt = s.Has(Pads.DPadRight) || (stick && s.LX > deadzone);
            Key(KUp, up && !dn);
            Key(KDown, dn && !up);
            Key(KLeft, lf && !rt);
            Key(KRight, rt && !lf);

            // --- buttons ---
            // with the free camera the mod raises the camera itself (the game's own animation would fight it)
            Key(KSpace, (!freeCam || !playing) && (s.HasMask(MaskOf("CameraLift")) || (!freeCam && s.RY > deadzone * 2)));
            Key(KEnter, s.HasMask(MaskOf("Confirm")));
            Key(KEsc, s.HasMask(MaskOf("Back")) || s.HasMask(MaskOf("Pause")));
            Key(KQ, s.HasMask(MaskOf("SkipTutorial")));
            Key(KR, s.HasMask(MaskOf("Respawn")));
        }

        void StepCamera()
        {
            if (DateTime.UtcNow < camNext)
                return;
            switch (camStep)
            {
                case 4:
                    // free the arrows first, so the tap below is a fresh key press
                    Key(KUp, false); Key(KDown, false); Key(KLeft, false); Key(KRight, false);
                    Key(KShift, true);
                    break;
                case 3:
                    Key(camKey, true);
                    break;
                case 2:
                    Key(camKey, false);
                    break;
                case 1:
                    Key(KShift, false);
                    break;
            }
            camStep--;
            camNext = DateTime.UtcNow.AddMilliseconds(35);
        }
    }
}
