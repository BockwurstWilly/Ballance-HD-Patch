// Test harness (not shipped): feeds synthetic controller states into the real Bridge against a running game,
// and checks the PlayStation report parser with hand-made reports.
//   BridgeTest.exe parse
//   BridgeTest.exe drive <gamePid> <shotExe> <outDir>
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace BallanceHD
{
    static class BridgeTest
    {
        static int fails;

        static void Expect(bool ok, string what)
        {
            Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
            if (!ok) fails++;
        }

        static int Main(string[] a)
        {
            if (a[0] == "parse")
            {
                PadState s;
                // DualShock 4 USB: report 0x01, LX=0 (left), LY=0 (up), hat=0 (up), cross pressed, R1, options
                var ds4 = new byte[64];
                ds4[0] = 0x01; ds4[1] = 0; ds4[2] = 0; ds4[3] = 128; ds4[4] = 128; ds4[5] = 0x20 | 0x00; ds4[6] = 0x02 | 0x20; ds4[8] = 0; ds4[9] = 255;
                Expect(Pads.ParseSony(ds4, 64, false, out s), "DS4 USB parses");
                Expect(s.LX == -32768 && s.LY == 32767, "DS4 left stick up-left (" + s.LX + "," + s.LY + ")");
                Expect(s.Has(Pads.DPadUp) && s.Has(Pads.A) && s.Has(Pads.RB) && s.Has(Pads.Start), "DS4 buttons: up, cross, R1, options");
                Expect(s.RightTrigger == 255, "DS4 R2 full");
                // DS4 Bluetooth full report 0x11: data from byte 3
                var bt = new byte[78];
                bt[0] = 0x11; bt[3] = 255; bt[4] = 128; bt[5] = 128; bt[6] = 128; bt[7] = 0x08 | 0x40; bt[8] = 0x01;
                Expect(Pads.ParseSony(bt, 78, false, out s), "DS4 BT 0x11 parses");
                Expect(s.LX == 32767 && !s.Has(Pads.DPadUp) && s.Has(Pads.B) && s.Has(Pads.LB), "DS4 BT: stick right, hat released, circle, L1");
                // DualSense USB: LX at +0, triggers at +4/+5, hat|face at +7, shoulders at +8
                var dse = new byte[64];
                dse[0] = 0x01; dse[1] = 128; dse[2] = 255; dse[3] = 128; dse[4] = 0; dse[5] = 200; dse[6] = 0; dse[8] = 0x06 | 0x80; dse[9] = 0x10;
                Expect(Pads.ParseSony(dse, 64, true, out s), "DualSense USB parses");
                Expect(s.LY == -32768 && s.Has(Pads.DPadLeft) && s.Has(Pads.Y) && s.Has(Pads.Back) && s.LeftTrigger == 200, "DualSense: stick down, d-pad left, triangle, create, L2");
                // DualSense Bluetooth full 0x31: data from byte 2
                var dsb = new byte[78];
                dsb[0] = 0x31; dsb[2] = 0; dsb[3] = 128; dsb[4] = 128; dsb[5] = 0; dsb[9] = 0x08 | 0x20; dsb[10] = 0x02;
                Expect(Pads.ParseSony(dsb, 78, true, out s), "DualSense BT 0x31 parses");
                Expect(s.LX == -32768 && s.Has(Pads.A) && s.Has(Pads.RB), "DualSense BT: stick left, cross, R1");
                Expect(!Pads.ParseSony(new byte[] { 0x05, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 10, false, out s), "unknown report ignored");
                Console.WriteLine(fails == 0 ? "ALL OK" : fails + " FAILED");
                return fails;
            }

            // drive: synthetic states through the real Bridge (it only sends keys while the game is in front)
            var game = Process.GetProcessById(int.Parse(a[1]));
            var bridge = new Bridge(game, ".", 35);
            var update = typeof(Bridge).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
            var lastField = typeof(Bridge).GetField("last", BindingFlags.NonPublic | BindingFlags.Instance);
            Action<PadState, int, string> hold = (st, ms, shot) =>
            {
                var end = DateTime.UtcNow.AddMilliseconds(ms);
                while (DateTime.UtcNow < end)
                {
                    update.Invoke(bridge, new object[] { st });
                    lastField.SetValue(bridge, st);
                    Thread.Sleep(8);
                }
                if (shot != null)
                    Process.Start(new ProcessStartInfo(a[2], "\"" + a[3] + "\\" + shot + ".png\" 960") { UseShellExecute = false }).WaitForExit();
            };
            var idle = new PadState();
            if (a.Length > 4 && a[4] == "menu")
            {
                // pause menu with Start, move down with the d-pad, then back out with B
                hold(new PadState { Buttons = Pads.Start }, 100, null);
                hold(idle, 1200, "menu0_pause");
                hold(new PadState { Buttons = Pads.DPadDown }, 100, null);
                hold(idle, 500, "menu1_down");
                hold(new PadState { Buttons = Pads.B }, 100, null);
                hold(idle, 1200, "menu2_back");
                return 0;
            }
            hold(idle, 300, "pad0_start");
            hold(new PadState { Buttons = Pads.RB }, 60, null);     // RB: rotate right
            hold(idle, 1500, "pad1_rb_rotated");
            hold(new PadState { RX = -32000 }, 200, null);          // right stick left: rotate back
            hold(idle, 1500, "pad2_rstick_back");
            hold(new PadState { Buttons = Pads.Y }, 1200, "pad3_y_lift");   // camera lift while held
            hold(idle, 800, null);
            hold(new PadState { LY = 30000 }, 700, null);           // left stick up: roll forward
            hold(idle, 600, "pad4_rolled");
            hold(new PadState { Buttons = Pads.Start }, 100, null); // pause menu
            hold(idle, 900, "pad5_pause");
            hold(new PadState { Buttons = Pads.B }, 100, null);     // back out of the pause menu
            hold(idle, 900, "pad6_resumed");
            return 0;
        }
    }
}

