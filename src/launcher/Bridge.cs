using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace BallanceHD
{
    /// <summary>
    /// Ballance only knows the keyboard: this turns controller input into the game's default keys while the
    /// game window is in front. Scan codes are sent (KEYEVENTF_SCANCODE), which is what DirectInput reads.
    ///   left stick / d-pad      arrows            roll the ball, move through menus
    ///   right stick ← →, LB/RB  Shift + ← / →     rotate the camera (one 90° step per push)
    ///   right stick ↑, LT/RT, Y Space (held)      raise the camera
    ///   A                       Enter             confirm
    ///   B, Start                Esc               back / pause
    ///   X                       Q                 skip the tutorial
    ///   View / Share            R                 respawn (BML+ Debug Utilities)
    /// </summary>
    sealed class Bridge
    {
        const ushort KUp = 0x148, KDown = 0x150, KLeft = 0x14B, KRight = 0x14D;   // extended scan codes
        const ushort KShift = 0x2A, KSpace = 0x39, KEnter = 0x1C, KEsc = 0x01, KQ = 0x10, KR = 0x13;

        readonly Process game;
        readonly string gameDir;
        readonly int deadzone;     // 0..32767
        readonly bool[] down = new bool[0x200];
        PadState last;
        int camStep;               // camera rotation sequence: 0 idle, else steps left
        ushort camKey;
        DateTime camNext;
        int camDir;                // direction the right stick currently pushes (-1, 0, 1); a push rotates once

        public Bridge(Process game, string dir, int deadzonePercent)
        {
            this.game = game;
            gameDir = dir;
            deadzone = 32767 * deadzonePercent / 100;
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
                var kind = (PadKind)(-1);
                while (!game.HasExited)
                {
                    var s = pads.Poll();
                    if (pads.Kind != kind)
                    {
                        // tells the game mod which button prompts to show
                        kind = pads.Kind;
                        PadMarker.SetPad(gameDir, kind);
                    }
                    if (GameInFront())
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
                PadMarker.SetPad(gameDir, PadKind.None);
                timeEndPeriod(1);
            }
        }

        bool Pressed(PadState s, ushort b) { return s.Has(b) && !last.Has(b); }

        void Update(PadState s)
        {
            // --- camera rotation: Shift down, arrow tapped, Shift up (one step per push) ---
            int dir = 0;
            if (s.RX < -deadzone * 2 && Math.Abs((int)s.RX) > Math.Abs((int)s.RY)) dir = -1;
            else if (s.RX > deadzone * 2 && Math.Abs((int)s.RX) > Math.Abs((int)s.RY)) dir = 1;
            else if (Math.Abs((int)s.RX) < deadzone) dir = 0;
            else dir = camDir;
            bool rotLeft = (dir == -1 && camDir != -1) || Pressed(s, Pads.LB);
            bool rotRight = (dir == 1 && camDir != 1) || Pressed(s, Pads.RB);
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

            // --- rolling / menu navigation ---
            bool up = s.Has(Pads.DPadUp) || s.LY > deadzone;
            bool dn = s.Has(Pads.DPadDown) || s.LY < -deadzone;
            bool lf = s.Has(Pads.DPadLeft) || s.LX < -deadzone;
            bool rt = s.Has(Pads.DPadRight) || s.LX > deadzone;
            Key(KUp, up && !dn);
            Key(KDown, dn && !up);
            Key(KLeft, lf && !rt);
            Key(KRight, rt && !lf);

            // --- camera lift (held) ---
            Key(KSpace, s.Has(Pads.Y) || s.LeftTrigger > 100 || s.RightTrigger > 100 || s.RY > deadzone * 2);

            // --- buttons ---
            Key(KEnter, s.Has(Pads.A));
            Key(KEsc, s.Has(Pads.B) || s.Has(Pads.Start));
            Key(KQ, s.Has(Pads.X));
            Key(KR, s.Has(Pads.Back));
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
