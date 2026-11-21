using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace BallanceHD
{
    /// <summary>One controller state in XInput terms (PlayStation pads are translated into it).</summary>
    struct PadState
    {
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LX, LY, RX, RY;     // y grows upwards

        public bool Has(ushort b) { return (Buttons & b) != 0; }

        /// <summary>Buttons plus the triggers as two extra bits (Pads.LTrigger / Pads.RTrigger).</summary>
        public bool HasMask(int mask)
        {
            if (mask == 0)
                return false;
            int all = Buttons | (LeftTrigger > 100 ? Pads.LTrigger : 0) | (RightTrigger > 100 ? Pads.RTrigger : 0);
            return (all & mask) != 0;
        }
    }

    /// <summary>
    /// Shared memory between the launcher and the game mod ("Local\BallanceHD_Pad", 128 bytes) - no file access,
    /// so nothing for the virus scanner to look at 60 times a second.
    ///   launcher:  0 seq, 4 lx, 8 ly, 12 rx, 16 ry (floats -1..1, y up), 20 buttons (+ trigger bits), 24 kind (1 Xbox, 2 PS), 28 seq
    ///   mod:      64 modSeq (counts frames), 68 state (1 in level, 2 paused, 4 settings panel has the controller,
    ///             8 free camera on, 16 stepless rolling on), 72.. button masks of the 9 actions (PadConfig.Actions order)
    /// </summary>
    sealed class SharedPad : IDisposable
    {
        public const string Name = @"Local\BallanceHD_Pad";
        public const int StateInLevel = 1, StatePaused = 2, StatePanel = 4, StateFreeCam = 8, StateAnalog = 16;
        readonly System.IO.MemoryMappedFiles.MemoryMappedFile map;
        readonly System.IO.MemoryMappedFiles.MemoryMappedViewAccessor view;
        int seq;
        int lastModSeq = -1, modQuiet = 999;

        public SharedPad()
        {
            map = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateOrOpen(Name, 128);
            view = map.CreateViewAccessor(0, 128);
        }

        static float Axis(short v) { return Math.Max(-1f, v / 32767f); }

        public void Write(PadState s, PadKind kind)
        {
            seq++;
            view.Write(0, seq);
            view.Write(4, Axis(s.LX));
            view.Write(8, Axis(s.LY));
            view.Write(12, Axis(s.RX));
            view.Write(16, Axis(s.RY));
            view.Write(20, s.Buttons | (s.LeftTrigger > 100 ? Pads.LTrigger : 0) | (s.RightTrigger > 100 ? Pads.RTrigger : 0));
            view.Write(24, kind == PadKind.Xbox ? 1 : kind == PadKind.PlayStation ? 2 : 0);
            view.Write(28, seq);
            int ms = view.ReadInt32(64);
            modQuiet = ms != lastModSeq ? 0 : modQuiet + 1;
            lastModSeq = ms;
        }

        /// <summary>The mod counts its frames: alive while that number moves (polled every 8 ms).</summary>
        public bool ModAlive { get { return modQuiet < 40; } }
        public int ModState { get { return ModAlive ? view.ReadInt32(68) : 0; } }
        public bool Has(int state) { return (ModState & state) != 0; }

        /// <summary>The button mask the mod uses for an action (its settings may have changed in the game).</summary>
        public int Mask(int action) { return view.ReadInt32(72 + 4 * action); }

        public void Dispose()
        {
            try
            {
                Write(new PadState(), PadKind.None);
                view.Dispose();
                map.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }
    enum PadKind { None, Xbox, PlayStation }

    /// <summary>
    /// Reads all connected controllers and merges them: Xbox pads through XInput, DualShock 4 / DualSense
    /// directly as HID devices (USB or Bluetooth) - no Steam Input or DS4Windows needed.
    /// </summary>
    sealed class Pads : IDisposable
    {
        public const ushort DPadUp = 0x0001, DPadDown = 0x0002, DPadLeft = 0x0004, DPadRight = 0x0008;
        public const ushort Start = 0x0010, Back = 0x0020, LThumb = 0x0040, RThumb = 0x0080;
        public const ushort LB = 0x0100, RB = 0x0200, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
        public const int LTrigger = 0x10000, RTrigger = 0x20000;

        // ---- XInput ----------------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public ushort wButtons;
            public byte bLeftTrigger, bRightTrigger;
            public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        static extern int XInputGetState14(int user, out XINPUT_STATE state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        static extern int XInputGetState910(int user, out XINPUT_STATE state);

        static int xinputVersion = 14;   // 14, 910, or 0 = none
        readonly bool[] xConnected = new bool[4];
        int xRetry;

        static bool XGet(int user, out XINPUT_STATE s)
        {
            s = new XINPUT_STATE();
            try
            {
                if (xinputVersion == 14)
                    return XInputGetState14(user, out s) == 0;
                if (xinputVersion == 910)
                    return XInputGetState910(user, out s) == 0;
            }
            catch (DllNotFoundException)
            {
                xinputVersion = xinputVersion == 14 ? 910 : 0;
            }
            catch (EntryPointNotFoundException)
            {
                xinputVersion = xinputVersion == 14 ? 910 : 0;
            }
            return false;
        }

        // ---- PlayStation (HID) -----------------------------------------------------------------------

        sealed class SonyPad
        {
            public string Path;
            public bool DualSense;
            public FileStream Stream;
            public Thread Reader;
            public volatile bool Alive = true;
            public PadState State;
            public readonly object Lock = new object();
        }

        readonly List<SonyPad> sony = new List<SonyPad>();
        DateTime nextScan = DateTime.MinValue;

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID, ProductID, VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
                          NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
                          NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
        [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
        [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data, ref HIDP_CAPS caps);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr w, int flags);
        [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int index, ref SP_DEVICE_INTERFACE_DATA d);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, int size, out int required, IntPtr info);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disposition, uint flags, IntPtr template);

        const ushort SonyVid = 0x054C;

        static bool KnownSony(ushort pid, out bool dualSense)
        {
            dualSense = pid == 0x0CE6 || pid == 0x0DF2;
            return dualSense || pid == 0x05C4 || pid == 0x09CC || pid == 0x0BA0;
        }

        static IEnumerable<string> HidPaths()
        {
            var list = new List<string>();
            Guid g;
            HidD_GetHidGuid(out g);
            var set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12 /* PRESENT | DEVICEINTERFACE */);
            if (set == IntPtr.Zero || set == new IntPtr(-1))
                return list;
            try
            {
                for (int i = 0; ; i++)
                {
                    var d = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA)) };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d))
                        break;
                    int size;
                    SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out size, IntPtr.Zero);
                    if (size <= 0)
                        continue;
                    var buf = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);   // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W
                        if (SetupDiGetDeviceInterfaceDetail(set, ref d, buf, size, out size, IntPtr.Zero))
                            list.Add(Marshal.PtrToStringUni(buf + 4));
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buf);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }
            return list;
        }

        void ScanSony()
        {
            lock (sony)
                sony.RemoveAll(p => !p.Alive);
            foreach (var path in HidPaths())
            {
                if (path.IndexOf("vid_054c", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                lock (sony)
                    if (sony.Exists(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase)))
                        continue;
                // read access is enough; share everything so Steam or the game keep working
                var h = CreateFile(path, 0x80000000 /* GENERIC_READ */, 3, IntPtr.Zero, 3 /* OPEN_EXISTING */, 0, IntPtr.Zero);
                if (h.IsInvalid)
                    continue;
                var attr = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES)) };
                bool ds;
                if (!HidD_GetAttributes(h, ref attr) || attr.VendorID != SonyVid || !KnownSony(attr.ProductID, out ds))
                {
                    h.Dispose();
                    continue;
                }
                IntPtr pre;
                int len = 64;
                if (HidD_GetPreparsedData(h, out pre))
                {
                    var caps = new HIDP_CAPS();
                    if (HidP_GetCaps(pre, ref caps) == 0x00110000 /* HIDP_STATUS_SUCCESS */)
                    {
                        if (caps.UsagePage != 1 || (caps.Usage != 4 && caps.Usage != 5))
                        {
                            // not the gamepad collection (e.g. the touchpad/audio part of a composite device)
                            HidD_FreePreparsedData(pre);
                            h.Dispose();
                            continue;
                        }
                        len = caps.InputReportByteLength;
                    }
                    HidD_FreePreparsedData(pre);
                }
                var pad = new SonyPad { Path = path, DualSense = ds, Stream = new FileStream(h, FileAccess.Read, Math.Max(len, 1), false) };
                int reportLength = Math.Max(len, 10);
                pad.Reader = new Thread(() => ReadLoop(pad, reportLength)) { IsBackground = true, Name = "sony-pad" };
                lock (sony)
                    sony.Add(pad);
                pad.Reader.Start();
            }
        }

        static void ReadLoop(SonyPad pad, int len)
        {
            var buf = new byte[len];
            try
            {
                while (pad.Alive)
                {
                    int n = pad.Stream.Read(buf, 0, buf.Length);
                    if (n <= 0)
                        break;
                    PadState s;
                    if (ParseSony(buf, n, pad.DualSense, out s))
                        lock (pad.Lock)
                            pad.State = s;
                }
            }
            catch (Exception)
            {
                // unplugged / Bluetooth gone
            }
            pad.Alive = false;
            try { pad.Stream.Dispose(); } catch (Exception) { }
        }

        static short Axis(byte v, bool invert)
        {
            int x = v * 257 - 32768;
            if (invert)
                x = -1 - x;
            return (short)x;
        }

        /// <summary>
        /// DualShock 4 USB 0x01 (64 bytes) / Bluetooth basic 0x01 (10 bytes): data from byte 1; Bluetooth full 0x11: from byte 3.
        /// DualSense USB 0x01 (64 bytes): from byte 1; Bluetooth full 0x31: from byte 2; Bluetooth basic 0x01: DS4 layout.
        ///   DS4 layout: +0 LX +1 LY +2 RX +3 RY +4 hat|face +5 shoulders/options +7 L2 +8 R2
        ///   DualSense:  +0 LX +1 LY +2 RX +3 RY +4 L2 +5 R2 +7 hat|face +8 shoulders/options
        /// </summary>
        public static bool ParseSony(byte[] r, int len, bool dualSense, out PadState s)
        {
            s = new PadState();
            if (len < 10)
                return false;
            int d;
            bool dsLayout;
            if (r[0] == 0x01 && (len >= 64 || !dualSense)) { d = 1; dsLayout = dualSense; }
            else if (r[0] == 0x01) { d = 1; dsLayout = false; }
            else if (r[0] == 0x11 && !dualSense && len >= 13) { d = 3; dsLayout = false; }
            else if (r[0] == 0x31 && dualSense && len >= 12) { d = 2; dsLayout = true; }
            else return false;
            if (d + 9 > len)
                return false;
            byte b1, b2, lt, rt;
            if (dsLayout) { b1 = r[d + 7]; b2 = r[d + 8]; lt = r[d + 4]; rt = r[d + 5]; }
            else { b1 = r[d + 4]; b2 = r[d + 5]; lt = r[d + 7]; rt = r[d + 8]; }
            ushort w = 0;
            int hat = b1 & 0x0F;
            ushort[] hatBits = { DPadUp, DPadUp | DPadRight, DPadRight, DPadDown | DPadRight, DPadDown, DPadDown | DPadLeft, DPadLeft, DPadUp | DPadLeft };
            if (hat < 8)
                w |= hatBits[hat];
            if ((b1 & 0x10) != 0) w |= X;        // square
            if ((b1 & 0x20) != 0) w |= A;        // cross
            if ((b1 & 0x40) != 0) w |= B;        // circle
            if ((b1 & 0x80) != 0) w |= Y;        // triangle
            if ((b2 & 0x01) != 0) w |= LB;
            if ((b2 & 0x02) != 0) w |= RB;
            if ((b2 & 0x10) != 0) w |= Back;     // share / create
            if ((b2 & 0x20) != 0) w |= Start;    // options
            if ((b2 & 0x40) != 0) w |= LThumb;
            if ((b2 & 0x80) != 0) w |= RThumb;
            s.Buttons = w;
            s.LeftTrigger = lt;
            s.RightTrigger = rt;
            s.LX = Axis(r[d], false);
            s.LY = Axis(r[d + 1], true);
            s.RX = Axis(r[d + 2], false);
            s.RY = Axis(r[d + 3], true);
            return true;
        }

        // ---- merged ----------------------------------------------------------------------------------

        public PadKind Kind { get; private set; }

        /// <summary>Polls all pads and returns the merged state (buttons ORed, the strongest stick wins).</summary>
        // tests: BALLANCEHD_FAKEPAD=<file in the PadLink format> replaces the real controllers
        static readonly string fakePad = Environment.GetEnvironmentVariable("BALLANCEHD_FAKEPAD");

        static bool ReadFake(out PadState s, out PadKind kind)
        {
            s = new PadState();
            kind = PadKind.None;
            try
            {
                byte[] b;
                using (var fs = new FileStream(fakePad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    b = new byte[32];
                    if (fs.Read(b, 0, 32) != 32)
                        return false;
                }
                Func<int, short> axis = at => (short)Math.Max(-32767, Math.Min(32767, BitConverter.ToSingle(b, at) * 32767));
                s.LX = axis(4); s.LY = axis(8); s.RX = axis(12); s.RY = axis(16);
                int buttons = BitConverter.ToInt32(b, 20);
                s.Buttons = (ushort)(buttons & 0xFFFF);
                s.LeftTrigger = (byte)((buttons & LTrigger) != 0 ? 255 : 0);
                s.RightTrigger = (byte)((buttons & RTrigger) != 0 ? 255 : 0);
                int k = BitConverter.ToInt32(b, 24);
                kind = k == 1 ? PadKind.Xbox : k == 2 ? PadKind.PlayStation : PadKind.None;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public PadState Poll()
        {
            var m = new PadState();
            Kind = PadKind.None;
            if (!string.IsNullOrEmpty(fakePad))
            {
                PadKind fk;
                ReadFake(out m, out fk);
                Kind = fk;
                return m;
            }
            bool rescanX = ++xRetry >= 120;   // disconnected XInput slots are slow to query: only every ~second
            if (rescanX)
                xRetry = 0;
            for (int i = 0; i < 4 && xinputVersion != 0; i++)
            {
                if (!xConnected[i] && !rescanX)
                    continue;
                XINPUT_STATE x;
                xConnected[i] = XGet(i, out x);
                if (!xConnected[i])
                    continue;
                Kind = PadKind.Xbox;
                Merge(ref m, new PadState
                {
                    Buttons = x.wButtons,
                    LeftTrigger = x.bLeftTrigger,
                    RightTrigger = x.bRightTrigger,
                    LX = x.sThumbLX,
                    LY = x.sThumbLY,
                    RX = x.sThumbRX,
                    RY = x.sThumbRY,
                });
            }
            if (DateTime.UtcNow >= nextScan)
            {
                nextScan = DateTime.UtcNow.AddSeconds(3);
                try { ScanSony(); } catch (Exception) { }
            }
            lock (sony)
            {
                foreach (var p in sony)
                {
                    if (!p.Alive)
                        continue;
                    PadState s;
                    lock (p.Lock)
                        s = p.State;
                    if (Kind == PadKind.None)
                        Kind = PadKind.PlayStation;
                    Merge(ref m, s);
                }
            }
            return m;
        }

        static void Merge(ref PadState m, PadState s)
        {
            m.Buttons |= s.Buttons;
            m.LeftTrigger = Math.Max(m.LeftTrigger, s.LeftTrigger);
            m.RightTrigger = Math.Max(m.RightTrigger, s.RightTrigger);
            if (Math.Abs((int)s.LX) + Math.Abs((int)s.LY) > Math.Abs((int)m.LX) + Math.Abs((int)m.LY)) { m.LX = s.LX; m.LY = s.LY; }
            if (Math.Abs((int)s.RX) + Math.Abs((int)s.RY) > Math.Abs((int)m.RX) + Math.Abs((int)m.RY)) { m.RX = s.RX; m.RY = s.RY; }
        }

        public void Dispose()
        {
            lock (sony)
            {
                foreach (var p in sony)
                {
                    p.Alive = false;
                    try { p.Stream.Dispose(); } catch (Exception) { }
                }
                sony.Clear();
            }
        }
    }
}
