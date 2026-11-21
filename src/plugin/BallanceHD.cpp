// Ballance HD - native Virtools plugin (feasibility test)
//
// A manager plugin (Managers\BallanceHD.dll): the player loads it like the game's own managers and calls it every
// frame. This test does the widescreen part of the BML+ mod and nothing else:
//   OnPreRender:  the camera about to be rendered gets the screen's aspect ratio and a field of view widened so the
//                 vertical view of the 4:3 original stays the same (Hor+); the original values are remembered
//   OnPostRender: the original values go back - the game's scripts never see a changed camera
// Built with clang for the Microsoft C++ ABI (the engine is MSVC-compiled), without the C runtime.

#include <math.h>
#include "CKAll.h"

// ---- the few Win32 functions the plugin uses (no windows.h: the SDK brings its own Windows types) ---------
extern "C" {
__declspec(dllimport) void *__stdcall CreateFileA(const char *, unsigned long, unsigned long, void *, unsigned long, unsigned long, void *);
__declspec(dllimport) int __stdcall WriteFile(void *, const void *, unsigned long, unsigned long *, void *);
__declspec(dllimport) void *__stdcall GetProcessHeap();
__declspec(dllimport) void *__stdcall HeapAlloc(void *, unsigned long, size_t);
__declspec(dllimport) int __stdcall HeapFree(void *, unsigned long, void *);
__declspec(dllimport) int __cdecl wsprintfA(char *, const char *, ...);
}
#define INVALID_FILE ((void *)(long)-1)

#define BALLANCEHD_GUID CKGUID(0x6b1d2c40, 0x3a7e41f1)

// ---- a tiny log, written with Win32 only -------------------------------------------------------
static void *g_log = INVALID_FILE;

static void Log(const char *text)
{
    if (g_log == INVALID_FILE)
        g_log = CreateFileA("..\\ModLoader\\BallanceHD\\plugin.log", 0x40000000 /* GENERIC_WRITE */, 1 /* FILE_SHARE_READ */, NULL, 2 /* CREATE_ALWAYS */, 0x80, NULL);
    if (g_log == INVALID_FILE)
        return;
    unsigned long n, len = 0;
    while (text[len])
        len++;
    WriteFile(g_log, text, len, &n, NULL);
    WriteFile(g_log, "\r\n", 2, &n, NULL);
}

static void LogInt(const char *text, int a, int b)
{
    char buf[256];
    wsprintfA(buf, "%s %d %d", text, a, b);
    Log(buf);
}

// ---- the manager -------------------------------------------------------------------------------
class BallanceHDManager : public CKBaseManager
{
public:
    explicit BallanceHDManager(CKContext *context) : CKBaseManager(context, BALLANCEHD_GUID, (CKSTRING)"Ballance HD")
    {
        context->RegisterNewManager(this);
    }

    CKDWORD GetValidFunctionsMask() override
    {
        return CKMANAGER_FUNC_OnPreRender | CKMANAGER_FUNC_OnPostRender;
    }

    CKERROR OnPreRender(CKRenderContext *dev) override
    {
        m_Camera = NULL;
        if (!dev)
            return CK_OK;
        CKCamera *cam = dev->GetAttachedCamera();
        int w = dev->GetWidth(), h = dev->GetHeight();
        if (!cam || w <= 0 || h <= 0)
            return CK_OK;
        cam->GetAspectRatio(m_AspectW, m_AspectH);
        m_Fov = cam->GetFov();
        if (m_AspectW <= 0 || m_AspectH <= 0 || m_AspectW * h == m_AspectH * w)
            return CK_OK;
        m_Camera = cam;
        // horizontal field of view for the screen's width with the original's vertical field of view
        float original = (float)m_AspectW / (float)m_AspectH;
        float screen = (float)w / (float)h;
        cam->SetAspectRatio(w, h);
        cam->SetFov((float)(2.0 * atan(tan(m_Fov * 0.5) * screen / original)));
        // the engine has worked out viewport and projection from the old values already: once more, now
        dev->ForceCameraSettingsUpdate();
        dev->PrepareCameras(CK_RENDER_USECURRENTSETTINGS);
        if (!m_Logged)
        {
            m_Logged = TRUE;
            LogInt("widescreen: camera aspect was", m_AspectW, m_AspectH);
            LogInt("widescreen: screen", w, h);
        }
        return CK_OK;
    }

    CKERROR OnPostRender(CKRenderContext *dev) override
    {
        if (m_Camera)
        {
            m_Camera->SetAspectRatio(m_AspectW, m_AspectH);
            m_Camera->SetFov(m_Fov);
            m_Camera = NULL;
        }
        return CK_OK;
    }

private:
    CKCamera *m_Camera = NULL;
    int m_AspectW = 4, m_AspectH = 3;
    float m_Fov = 0;
    CKBOOL m_Logged = FALSE;
};

// ---- plugin entry points -----------------------------------------------------------------------
static CKERROR InitInstance(CKContext *context)
{
    Log("Ballance HD plugin: InitInstance");
    new BallanceHDManager(context);
    return CK_OK;
}

static CKERROR ExitInstance(CKContext *context)
{
    BallanceHDManager *man = (BallanceHDManager *)context->GetManagerByGuid(BALLANCEHD_GUID);
    delete man;
    return CK_OK;
}

static CKPluginInfo *g_info = NULL;

PLUGIN_EXPORT int CKGetPluginInfoCount()
{
    return 1;
}

PLUGIN_EXPORT CKPluginInfo *CKGetPluginInfo(int index)
{
    if (!g_info)
    {
        Log("Ballance HD plugin: CKGetPluginInfo");
        g_info = new CKPluginInfo();
        g_info->m_GUID = BALLANCEHD_GUID;
        g_info->m_Extension = "";
        g_info->m_Description = "Ballance HD";
        g_info->m_Author = "CompiWare / BockwurstWilly";
        g_info->m_Summary = "Widescreen, controller and HD features for Ballance";
        g_info->m_Version = 0x000001;
        g_info->m_InitInstanceFct = InitInstance;
        g_info->m_ExitInstanceFct = ExitInstance;
        g_info->m_Type = CKPLUGIN_MANAGER_DLL;
    }
    return g_info;
}

// ---- no C runtime: memory comes from the process heap ------------------------------------------
void *operator new(size_t n) { return HeapAlloc(GetProcessHeap(), 0, n ? n : 1); }
void *operator new[](size_t n) { return HeapAlloc(GetProcessHeap(), 0, n ? n : 1); }
void operator delete(void *p) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }
void operator delete[](void *p) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }
void operator delete(void *p, size_t) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }
void operator delete[](void *p, size_t) noexcept { if (p) HeapFree(GetProcessHeap(), 0, p); }

extern "C" int __stdcall DllMain(void *, unsigned long, void *) { return 1; }
extern "C" int _fltused = 0;      // floating point without the C runtime
