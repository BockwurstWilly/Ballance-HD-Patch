/*
 * Ballance HD plugin - the Windows side: paths, log, settings file, controllers, keyboard.
 * Compiled as C with the Windows headers (see win.h for why it is separate from the C++ part).
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <setupapi.h>
#include <string.h>
#include <stdlib.h>
#include <math.h>
#include "win.h"

/* ---- paths and log ------------------------------------------------------------------------- */

static char g_root[MAX_PATH];        /* game folder with trailing backslash */
static char g_ini[MAX_PATH];
static char g_buf[MAX_PATH * 2];
static HANDLE g_log = INVALID_HANDLE_VALUE;
static CRITICAL_SECTION g_logLock;
static LARGE_INTEGER g_freq;

const char *hd_game_path(const char *relative)
{
    lstrcpynA(g_buf, g_root, sizeof(g_buf));
    lstrcatA(g_buf, relative);
    return g_buf;
}

int hd_file_exists(const char *relative)
{
    DWORD a = GetFileAttributesA(hd_game_path(relative));
    return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
}

int hd_read_file(const char *relative, void *buf, int size)
{
    DWORD n = 0;
    HANDLE h = CreateFileA(hd_game_path(relative), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return 0;
    ReadFile(h, buf, (DWORD)size, &n, NULL);
    CloseHandle(h);
    return (int)n;
}

void hd_log(const char *text)
{
    DWORD n;
    if (g_log == INVALID_HANDLE_VALUE)
        return;
    EnterCriticalSection(&g_logLock);
    WriteFile(g_log, text, lstrlenA(text), &n, NULL);
    WriteFile(g_log, "\r\n", 2, &n, NULL);
    LeaveCriticalSection(&g_logLock);
}

void hd_logi(const char *text, int a, int b)
{
    char buf[512];
    wsprintfA(buf, "%s %d %d", text, a, b);
    hd_log(buf);
}

double hd_time(void)
{
    LARGE_INTEGER t;
    QueryPerformanceCounter(&t);
    return (double)t.QuadPart / (double)g_freq.QuadPart;
}

/* ---- settings ------------------------------------------------------------------------------ */

int hd_ini_int(const char *section, const char *key, int def)
{
    return (int)GetPrivateProfileIntA(section, key, def, g_ini);
}

void hd_ini_str(const char *section, const char *key, const char *def, char *out, int size)
{
    GetPrivateProfileStringA(section, key, def, out, size, g_ini);
}

void hd_ini_set_int(const char *section, const char *key, int value)
{
    char buf[32];
    wsprintfA(buf, "%d", value);
    WritePrivateProfileStringA(section, key, buf, g_ini);
}

void hd_ini_set_str(const char *section, const char *key, const char *value)
{
    WritePrivateProfileStringA(section, key, value, g_ini);
}

/* ---- XInput -------------------------------------------------------------------------------- */

typedef struct {
    DWORD packet;
    WORD buttons;
    BYTE lt, rt;
    SHORT lx, ly, rx, ry;
} XSTATE;
typedef DWORD(WINAPI *XInputGetStateFn)(DWORD, XSTATE *);
static XInputGetStateFn g_xget;
static int g_xconnected[4];
static int g_xretry;

static float axis16(SHORT v) { return v < -32767 ? -1.0f : v / 32767.0f; }

/* ---- PlayStation controllers over HID ------------------------------------------------------- */
/*
 * Input reports (byte 0 = report id):
 *   DualShock 4  USB 0x01 (64 bytes) / Bluetooth basic 0x01: data from byte 1; Bluetooth full 0x11: from byte 3
 *   DualSense    USB 0x01 (64 bytes): from byte 1; Bluetooth full 0x31: from byte 2; Bluetooth basic 0x01: DS4 layout
 *   DS4 layout:  +0 LX +1 LY +2 RX +3 RY +4 hat|face +5 shoulders/options +7 L2 +8 R2
 *   DualSense:   +0 LX +1 LY +2 RX +3 RY +4 L2 +5 R2 +7 hat|face +8 shoulders/options
 */
#define MAX_SONY 4
typedef struct {
    volatile LONG used;          /* 1 while a reader thread owns the slot */
    char path[512];
    int dualsense;
    int len;
    HANDLE file;
    CRITICAL_SECTION lock;
    hd_pad state;
} SONY;
static SONY g_sony[MAX_SONY];
static volatile LONG g_running;
static HANDLE g_scanThread;

typedef struct { ULONG Size; USHORT VendorID, ProductID, VersionNumber; } HIDATTR;
typedef struct { USHORT Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength, Reserved[17];
                 USHORT counts[10]; } HIDCAPS;
typedef void(WINAPI *HidGuidFn)(GUID *);
typedef BOOLEAN(WINAPI *HidAttrFn)(HANDLE, HIDATTR *);
typedef BOOLEAN(WINAPI *HidPrepFn)(HANDLE, void **);
typedef BOOLEAN(WINAPI *HidFreeFn)(void *);
typedef LONG(WINAPI *HidCapsFn)(void *, HIDCAPS *);
static HidGuidFn pHidGuid;
static HidAttrFn pHidAttr;
static HidPrepFn pHidPrep;
static HidFreeFn pHidFree;
static HidCapsFn pHidCaps;

static float axis8(BYTE v, int invert)
{
    int x = (int)v * 257 - 32768;
    if (invert)
        x = -1 - x;
    return x < -32767 ? -1.0f : x / 32767.0f;
}

static int parse_sony(const BYTE *r, DWORD len, int dualsense, hd_pad *s)
{
    static const int hat[8] = { HD_DPAD_UP, HD_DPAD_UP | HD_DPAD_RIGHT, HD_DPAD_RIGHT, HD_DPAD_DOWN | HD_DPAD_RIGHT,
                                HD_DPAD_DOWN, HD_DPAD_DOWN | HD_DPAD_LEFT, HD_DPAD_LEFT, HD_DPAD_UP | HD_DPAD_LEFT };
    const BYTE *d;
    BYTE b1, b2, lt, rt;
    int ds, w = 0;
    if (len < 10)
        return 0;
    if (r[0] == 0x01 && (len >= 64 || !dualsense)) { d = r + 1; ds = dualsense; }
    else if (r[0] == 0x01) { d = r + 1; ds = 0; }
    else if (r[0] == 0x11 && !dualsense && len >= 13) { d = r + 3; ds = 0; }
    else if (r[0] == 0x31 && dualsense && len >= 12) { d = r + 2; ds = 1; }
    else return 0;
    if (d + 9 > r + len)
        return 0;
    if (ds) { b1 = d[7]; b2 = d[8]; lt = d[4]; rt = d[5]; }
    else { b1 = d[4]; b2 = d[5]; lt = d[7]; rt = d[8]; }
    if ((b1 & 0x0F) < 8) w |= hat[b1 & 0x0F];
    if (b1 & 0x10) w |= HD_X;          /* square */
    if (b1 & 0x20) w |= HD_A;          /* cross */
    if (b1 & 0x40) w |= HD_B;          /* circle */
    if (b1 & 0x80) w |= HD_Y;          /* triangle */
    if (b2 & 0x01) w |= HD_LB;
    if (b2 & 0x02) w |= HD_RB;
    if (b2 & 0x10) w |= HD_BACK;       /* share / create */
    if (b2 & 0x20) w |= HD_START;      /* options */
    if (b2 & 0x40) w |= HD_LTHUMB;
    if (b2 & 0x80) w |= HD_RTHUMB;
    if (lt > 100) w |= HD_LT;
    if (rt > 100) w |= HD_RT;
    s->buttons = w;
    s->lx = axis8(d[0], 0);
    s->ly = axis8(d[1], 1);
    s->rx = axis8(d[2], 0);
    s->ry = axis8(d[3], 1);
    s->kind = HD_PAD_PS;
    return 1;
}

static DWORD WINAPI sony_reader(LPVOID arg)
{
    SONY *p = (SONY *)arg;
    BYTE buf[128];
    DWORD n;
    hd_pad s;
    while (g_running)
    {
        if (!ReadFile(p->file, buf, p->len > (int)sizeof(buf) ? (DWORD)sizeof(buf) : (DWORD)p->len, &n, NULL) || n == 0)
            break;
        memset(&s, 0, sizeof(s));
        if (parse_sony(buf, n, p->dualsense, &s))
        {
            EnterCriticalSection(&p->lock);
            p->state = s;
            LeaveCriticalSection(&p->lock);
        }
    }
    CloseHandle(p->file);
    p->file = INVALID_HANDLE_VALUE;
    EnterCriticalSection(&p->lock);
    memset(&p->state, 0, sizeof(p->state));
    LeaveCriticalSection(&p->lock);
    InterlockedExchange(&p->used, 0);
    return 0;
}

static int known_sony(USHORT pid, int *ds)
{
    *ds = pid == 0x0CE6 || pid == 0x0DF2;
    return *ds || pid == 0x05C4 || pid == 0x09CC || pid == 0x0BA0;
}

static void scan_sony(void)
{
    GUID g;
    HDEVINFO set;
    SP_DEVICE_INTERFACE_DATA d;
    DWORD i, size;
    if (!pHidGuid)
        return;
    pHidGuid(&g);
    set = SetupDiGetClassDevsA(&g, NULL, NULL, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
    if (set == INVALID_HANDLE_VALUE)
        return;
    for (i = 0;; i++)
    {
        BYTE detailBuf[1024];
        SP_DEVICE_INTERFACE_DETAIL_DATA_A *detail = (SP_DEVICE_INTERFACE_DETAIL_DATA_A *)detailBuf;
        HANDLE h;
        HIDATTR attr;
        void *pre;
        int ds, j, len = 64, slot = -1, already = 0;
        d.cbSize = sizeof(d);
        if (!SetupDiEnumDeviceInterfaces(set, NULL, &g, i, &d))
            break;
        detail->cbSize = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA_A);
        if (!SetupDiGetDeviceInterfaceDetailA(set, &d, detail, sizeof(detailBuf), &size, NULL))
            continue;
        if (!strstr(detail->DevicePath, "vid_054c") && !strstr(detail->DevicePath, "VID_054C"))
            continue;
        for (j = 0; j < MAX_SONY; j++)
            if (g_sony[j].used && lstrcmpiA(g_sony[j].path, detail->DevicePath) == 0)
                already = 1;
        if (already)
            continue;
        h = CreateFileA(detail->DevicePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL);
        if (h == INVALID_HANDLE_VALUE)
            continue;
        attr.Size = sizeof(attr);
        if (!pHidAttr(h, &attr) || attr.VendorID != 0x054C || !known_sony(attr.ProductID, &ds))
        {
            CloseHandle(h);
            continue;
        }
        if (pHidPrep(h, &pre))
        {
            HIDCAPS caps;
            memset(&caps, 0, sizeof(caps));
            if (pHidCaps(pre, &caps) == 0x00110000)
            {
                if (caps.UsagePage != 1 || (caps.Usage != 4 && caps.Usage != 5))
                {
                    pHidFree(pre);
                    CloseHandle(h);
                    continue;      /* not the gamepad collection */
                }
                len = caps.InputReportByteLength;
            }
            pHidFree(pre);
        }
        for (j = 0; j < MAX_SONY; j++)
            if (InterlockedCompareExchange(&g_sony[j].used, 1, 0) == 0) { slot = j; break; }
        if (slot < 0)
        {
            CloseHandle(h);
            break;
        }
        lstrcpynA(g_sony[slot].path, detail->DevicePath, sizeof(g_sony[slot].path));
        g_sony[slot].dualsense = ds;
        g_sony[slot].len = len < 10 ? 10 : len;
        g_sony[slot].file = h;
        CloseHandle(CreateThread(NULL, 0, sony_reader, &g_sony[slot], 0, NULL));
        hd_log(ds ? "controller: DualSense connected" : "controller: DualShock 4 connected");
    }
    SetupDiDestroyDeviceInfoList(set);
}

/* the HID scan takes milliseconds: it runs on its own thread, never on the game's */
static DWORD WINAPI scan_thread(LPVOID arg)
{
    (void)arg;
    while (g_running)
    {
        scan_sony();
        Sleep(3000);
    }
    return 0;
}

/* tests: BALLANCEHD_FAKEPAD=<file> (32 bytes: int seq, float lx ly rx ry, int buttons, int kind, int seq) */
static char g_fake[MAX_PATH];

static int read_fake(hd_pad *out)
{
    HANDLE h;
    BYTE b[32];
    DWORD n = 0;
    if (!g_fake[0])
        return 0;
    h = CreateFileA(g_fake, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, 0, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return 1;
    ReadFile(h, b, 32, &n, NULL);
    CloseHandle(h);
    if (n == 32)
    {
        memcpy(&out->lx, b + 4, 4);
        memcpy(&out->ly, b + 8, 4);
        memcpy(&out->rx, b + 12, 4);
        memcpy(&out->ry, b + 16, 4);
        memcpy(&out->buttons, b + 20, 4);
        memcpy(&out->kind, b + 24, 4);
    }
    return 1;
}

void hd_pad_poll(hd_pad *out)
{
    int i, rescan;
    memset(out, 0, sizeof(*out));
    if (read_fake(out))
        return;
    rescan = ++g_xretry >= 120;        /* empty XInput slots are slow to query: only every ~2 s */
    if (rescan)
        g_xretry = 0;
    for (i = 0; i < 4 && g_xget; i++)
    {
        XSTATE x;
        if (!g_xconnected[i] && !rescan)
            continue;
        g_xconnected[i] = g_xget(i, &x) == 0;
        if (!g_xconnected[i])
            continue;
        out->kind = HD_PAD_XBOX;
        out->buttons |= x.buttons | (x.lt > 100 ? HD_LT : 0) | (x.rt > 100 ? HD_RT : 0);
        if (abs(x.lx) + abs(x.ly) > (int)((fabsf(out->lx) + fabsf(out->ly)) * 32767)) { out->lx = axis16(x.lx); out->ly = axis16(x.ly); }
        if (abs(x.rx) + abs(x.ry) > (int)((fabsf(out->rx) + fabsf(out->ry)) * 32767)) { out->rx = axis16(x.rx); out->ry = axis16(x.ry); }
    }
    for (i = 0; i < MAX_SONY; i++)
    {
        hd_pad s;
        if (!g_sony[i].used || g_sony[i].file == INVALID_HANDLE_VALUE)
            continue;
        EnterCriticalSection(&g_sony[i].lock);
        s = g_sony[i].state;
        LeaveCriticalSection(&g_sony[i].lock);
        if (s.kind == HD_PAD_NONE)
            continue;
        if (out->kind == HD_PAD_NONE)
            out->kind = HD_PAD_PS;
        out->buttons |= s.buttons;
        if (fabsf(s.lx) + fabsf(s.ly) > fabsf(out->lx) + fabsf(out->ly)) { out->lx = s.lx; out->ly = s.ly; }
        if (fabsf(s.rx) + fabsf(s.ry) > fabsf(out->rx) + fabsf(out->ry)) { out->rx = s.rx; out->ry = s.ry; }
    }
}

/* ---- keyboard -------------------------------------------------------------------------------- */

static unsigned char g_down[0x200];

void hd_key(unsigned short scan, int down)
{
    INPUT in;
    down = down ? 1 : 0;
    if (scan >= 0x200 || g_down[scan] == down)
        return;
    g_down[scan] = (unsigned char)down;
    memset(&in, 0, sizeof(in));
    in.type = INPUT_KEYBOARD;
    in.ki.wScan = scan & 0xFF;
    in.ki.dwFlags = KEYEVENTF_SCANCODE | (down ? 0 : KEYEVENTF_KEYUP) | ((scan & 0x100) ? KEYEVENTF_EXTENDEDKEY : 0);
    SendInput(1, &in, sizeof(in));
}

void hd_release_keys(void)
{
    unsigned short k;
    for (k = 0; k < 0x200; k++)
        if (g_down[k])
            hd_key(k, 0);
}

int hd_game_in_front(void)
{
    DWORD pid = 0;
    GetWindowThreadProcessId(GetForegroundWindow(), &pid);
    return pid == GetCurrentProcessId();
}

/* ---- start / stop ---------------------------------------------------------------------------- */

void hd_init(void)
{
    char exe[MAX_PATH];
    char *slash;
    int i;
    HMODULE x, hid;
    QueryPerformanceFrequency(&g_freq);
    InitializeCriticalSection(&g_logLock);
    /* the game folder: the player sits in <game>\Bin */
    GetModuleFileNameA(NULL, exe, sizeof(exe));
    slash = strrchr(exe, '\\');
    if (slash) *slash = 0;
    slash = strrchr(exe, '\\');
    if (slash && lstrcmpiA(slash + 1, "Bin") == 0)
        *slash = 0;
    lstrcpynA(g_root, exe, sizeof(g_root) - 2);
    lstrcatA(g_root, "\\");
    CreateDirectoryA(hd_game_path("BallanceHD"), NULL);
    lstrcpynA(g_ini, hd_game_path("BallanceHD\\Settings.ini"), sizeof(g_ini));
    g_log = CreateFileA(hd_game_path("BallanceHD\\plugin.log"), GENERIC_WRITE, FILE_SHARE_READ, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    hd_log("Ballance HD plugin 2.1.0");
    if (!GetEnvironmentVariableA("BALLANCEHD_FAKEPAD", g_fake, sizeof(g_fake)))
        g_fake[0] = 0;

    x = LoadLibraryA("xinput1_4.dll");
    if (!x) x = LoadLibraryA("xinput9_1_0.dll");
    if (x) g_xget = (XInputGetStateFn)GetProcAddress(x, "XInputGetState");
    hid = LoadLibraryA("hid.dll");
    if (hid)
    {
        pHidGuid = (HidGuidFn)GetProcAddress(hid, "HidD_GetHidGuid");
        pHidAttr = (HidAttrFn)GetProcAddress(hid, "HidD_GetAttributes");
        pHidPrep = (HidPrepFn)GetProcAddress(hid, "HidD_GetPreparsedData");
        pHidFree = (HidFreeFn)GetProcAddress(hid, "HidD_FreePreparsedData");
        pHidCaps = (HidCapsFn)GetProcAddress(hid, "HidP_GetCaps");
        if (!pHidGuid || !pHidAttr || !pHidPrep || !pHidFree || !pHidCaps)
            pHidGuid = NULL;
    }
    for (i = 0; i < MAX_SONY; i++)
    {
        InitializeCriticalSection(&g_sony[i].lock);
        g_sony[i].file = INVALID_HANDLE_VALUE;
    }
    for (i = 0; i < 4; i++)
        g_xconnected[i] = 1;          /* first poll looks at every slot */
    g_running = 1;
    g_scanThread = CreateThread(NULL, 0, scan_thread, NULL, 0, NULL);
}

void hd_shutdown(void)
{
    int i;
    hd_release_keys();
    g_running = 0;
    for (i = 0; i < MAX_SONY; i++)
        if (g_sony[i].used && g_sony[i].file != INVALID_HANDLE_VALUE)
            CancelIoEx(g_sony[i].file, NULL);
    if (g_scanThread)
        CloseHandle(g_scanThread);
    g_scanThread = NULL;
}
