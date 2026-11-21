using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace BallanceHD
{
    class DisplayInfo
    {
        public string Device;       // \\.\DISPLAY1
        public string Name;         // monitor name
        public int Number;          // 1-based, as Windows shows it
        public int X, Y, Width, Height, Hz;
        public bool Primary;
    }

    /// <summary>
    /// Monitors with their real pixel size and refresh rate (EnumDisplaySettings reports physical pixels,
    /// independent of the DPI awareness of the caller).
    /// </summary>
    static class Displays
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool EnumDisplayDevices(string device, int index, ref DISPLAY_DEVICE dd, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);

        const int ENUM_CURRENT_SETTINGS = -1;
        const int ATTACHED_TO_DESKTOP = 0x1, PRIMARY_DEVICE = 0x4;

        public static List<DisplayInfo> All()
        {
            var list = new List<DisplayInfo>();
            for (int i = 0; ; i++)
            {
                var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)) };
                if (!EnumDisplayDevices(null, i, ref dd, 0))
                    break;
                if ((dd.StateFlags & ATTACHED_TO_DESKTOP) == 0)
                    continue;
                var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                if (!EnumDisplaySettings(dd.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
                    continue;
                var mon = new DISPLAY_DEVICE { cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)) };
                string name = EnumDisplayDevices(dd.DeviceName, 0, ref mon, 0) && !string.IsNullOrEmpty(mon.DeviceString) ? mon.DeviceString : dd.DeviceString;
                int number = 0;
                var digits = new string(dd.DeviceName.Where(char.IsDigit).ToArray());
                int.TryParse(digits, out number);
                list.Add(new DisplayInfo
                {
                    Device = dd.DeviceName,
                    Name = name,
                    Number = number > 0 ? number : list.Count + 1,
                    X = dm.dmPositionX,
                    Y = dm.dmPositionY,
                    Width = dm.dmPelsWidth,
                    Height = dm.dmPelsHeight,
                    Hz = dm.dmDisplayFrequency > 1 ? dm.dmDisplayFrequency : 60,
                    Primary = (dd.StateFlags & PRIMARY_DEVICE) != 0,
                });
            }
            if (list.Count == 0)
            {
                var b = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                list.Add(new DisplayInfo { Device = "", Name = "Display", Number = 1, Width = b.Width, Height = b.Height, Hz = 60, Primary = true });
            }
            return list.OrderBy(d => d.Number).ToList();
        }

        /// <summary>The display with that device name, else the primary one.</summary>
        public static DisplayInfo Pick(List<DisplayInfo> all, string device)
        {
            var d = all.FirstOrDefault(x => string.Equals(x.Device, device, StringComparison.OrdinalIgnoreCase));
            return d ?? all.FirstOrDefault(x => x.Primary) ?? all[0];
        }

        /// <summary>32-bit modes of a display (for exclusive fullscreen), largest first, each size once.</summary>
        public static List<Size2> Modes(string device)
        {
            var set = new HashSet<long>();
            var list = new List<Size2>();
            for (int i = 0; ; i++)
            {
                var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                if (!EnumDisplaySettings(string.IsNullOrEmpty(device) ? null : device, i, ref dm))
                    break;
                if (dm.dmBitsPerPel != 32 || dm.dmPelsWidth < 640 || dm.dmPelsHeight < 480)
                    continue;
                if (set.Add(((long)dm.dmPelsWidth << 32) | (uint)dm.dmPelsHeight))
                    list.Add(new Size2(dm.dmPelsWidth, dm.dmPelsHeight));
            }
            return list.OrderByDescending(s => s.W).ThenByDescending(s => s.H).ToList();
        }
    }

    struct Size2
    {
        public int W, H;
        public Size2(int w, int h) { W = w; H = h; }
        public override string ToString() { return W + " × " + H; }
    }
}
