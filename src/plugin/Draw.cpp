// Ballance HD plugin - 2D drawing (see Draw.h).
#include "CKAll.h"
#include "Draw.h"
#include "win.h"

namespace draw
{
    // ---- font atlas: font.bin = "HDF1", int count, int first, int atlasW, int atlasH, float size, float line,
    //      then per character: short x, y, w, h, xoff, yoff, advance (atlas pixels at "size") ----
    struct Glyph { short x, y, w, h, xo, yo, adv; };
    static Glyph g_glyph[256];
    static bool g_has[256];
    static float g_size = 48, g_line = 56;
    static int g_aw = 1, g_ah = 1;
    static CK_ID g_fontTex = 0;
    static CKContext *g_ctx = NULL;

    // textures by file name
    struct TexEntry { char name[64]; CK_ID id; bool missing; };
    static TexEntry g_tex[96];
    static int g_texCount = 0;

    static CKRenderContext *g_dev = NULL;
    static VxRect g_oldView;
    static CKDWORD g_oldStates[12];
    static const VXRENDERSTATETYPE g_states[12] = {
        VXRENDERSTATE_ZENABLE, VXRENDERSTATE_ZWRITEENABLE, VXRENDERSTATE_ALPHABLENDENABLE, VXRENDERSTATE_SRCBLEND,
        VXRENDERSTATE_DESTBLEND, VXRENDERSTATE_CULLMODE, VXRENDERSTATE_LIGHTING, VXRENDERSTATE_FOGENABLE,
        VXRENDERSTATE_ALPHATESTENABLE, VXRENDERSTATE_SPECULARENABLE, VXRENDERSTATE_SHADEMODE, VXRENDERSTATE_FILLMODE };

    static bool Same(const char *a, const char *b)
    {
        while (*a && *a == *b) { a++; b++; }
        return *a == *b;
    }

    static void Copy(char *dst, const char *src, int n)
    {
        int i = 0;
        for (; i < n - 1 && src[i]; i++)
            dst[i] = src[i];
        dst[i] = 0;
    }

    CKTexture *Texture(const char *relative)
    {
        if (!g_ctx)
            return NULL;
        for (int i = 0; i < g_texCount; i++)
        {
            if (!Same(g_tex[i].name, relative))
                continue;
            if (g_tex[i].missing)
                return NULL;
            CKTexture *t = (CKTexture *)g_ctx->GetObject(g_tex[i].id);
            if (t)
                return t;
            g_tex[i].id = 0;           // destroyed with a level: load again below
            break;
        }
        if (!hd_file_exists(relative))
        {
            if (g_texCount < 96)
            {
                Copy(g_tex[g_texCount].name, relative, 64);
                g_tex[g_texCount].missing = true;
                g_texCount++;
            }
            hd_log(relative);
            return NULL;
        }
        CKTexture *t = (CKTexture *)g_ctx->CreateObject(CKCID_TEXTURE, (CKSTRING) "BallanceHD texture");
        if (!t)
            return NULL;
        t->ModifyObjectFlags(CK_OBJECT_NOTTOBESAVED | CK_OBJECT_NOTTOBEDELETED, 0);
        t->SetDesiredVideoFormat(_32_ARGB8888);
        if (!t->LoadImage((CKSTRING)hd_game_path(relative)))
        {
            g_ctx->DestroyObject(t);
            return NULL;
        }
        int slot = -1;
        for (int i = 0; i < g_texCount; i++)
            if (Same(g_tex[i].name, relative))
                slot = i;
        if (slot < 0 && g_texCount < 96)
            slot = g_texCount++;
        if (slot >= 0)
        {
            Copy(g_tex[slot].name, relative, 64);
            g_tex[slot].id = t->GetID();
            g_tex[slot].missing = false;
        }
        return t;
    }

    void Init(CKContext *context)
    {
        g_ctx = context;
        char buf[16 + 256 * 14];
        int n = hd_read_file("BallanceHD\\font.bin", buf, sizeof(buf));
        if (n < 28 || buf[0] != 'H' || buf[1] != 'D' || buf[2] != 'F' || buf[3] != '1')
        {
            hd_log("font.bin missing or invalid");
            return;
        }
        int count = *(int *)(buf + 4), first = *(int *)(buf + 8);
        g_aw = *(int *)(buf + 12);
        g_ah = *(int *)(buf + 16);
        g_size = *(float *)(buf + 20);
        g_line = *(float *)(buf + 24);
        const short *s = (const short *)(buf + 28);
        for (int i = 0; i < count && first + i < 256 && 28 + (i + 1) * 14 <= n; i++, s += 7)
        {
            Glyph &g = g_glyph[first + i];
            g.x = s[0]; g.y = s[1]; g.w = s[2]; g.h = s[3]; g.xo = s[4]; g.yo = s[5]; g.adv = s[6];
            g_has[first + i] = true;
        }
    }

    unsigned int Argb(float r, float g, float b, float a)
    {
        int ia = (int)(a * 255.0f + 0.5f), ir = (int)(r * 255.0f + 0.5f), ig = (int)(g * 255.0f + 0.5f), ib = (int)(b * 255.0f + 0.5f);
        if (ia < 0) ia = 0; if (ia > 255) ia = 255;
        return ((unsigned int)ia << 24) | ((unsigned int)ir << 16) | ((unsigned int)ig << 8) | (unsigned int)ib;
    }

    void Begin(CKRenderContext *dev)
    {
        g_dev = dev;
        dev->GetViewRect(g_oldView);
        VxRect full(0.0f, 0.0f, (float)dev->GetWidth(), (float)dev->GetHeight());
        dev->SetViewRect(full);
        for (int i = 0; i < 12; i++)
            g_oldStates[i] = dev->GetState(g_states[i]);
        dev->SetState(VXRENDERSTATE_FILLMODE, VXFILL_SOLID);
        dev->SetState(VXRENDERSTATE_SHADEMODE, VXSHADE_GOURAUD);
        dev->SetState(VXRENDERSTATE_CULLMODE, VXCULL_NONE);
        dev->SetState(VXRENDERSTATE_SRCBLEND, VXBLEND_SRCALPHA);
        dev->SetState(VXRENDERSTATE_DESTBLEND, VXBLEND_INVSRCALPHA);
        dev->SetState(VXRENDERSTATE_ALPHATESTENABLE, FALSE);
        dev->SetState(VXRENDERSTATE_ZWRITEENABLE, FALSE);
        dev->SetState(VXRENDERSTATE_ZENABLE, FALSE);
        dev->SetState(VXRENDERSTATE_ALPHABLENDENABLE, TRUE);
        dev->SetState(VXRENDERSTATE_FOGENABLE, FALSE);
        dev->SetState(VXRENDERSTATE_SPECULARENABLE, FALSE);
        dev->SetState(VXRENDERSTATE_LIGHTING, FALSE);
        dev->SetTextureStageState(CKRST_TSS_ADDRESS, VXTEXTURE_ADDRESSCLAMP);
        dev->SetTextureStageState(CKRST_TSS_TEXTUREMAPBLEND, VXTEXTUREBLEND_MODULATEALPHA);
        dev->SetTextureStageState(CKRST_TSS_MINFILTER, VXTEXTUREFILTER_LINEAR);
        dev->SetTextureStageState(CKRST_TSS_MAGFILTER, VXTEXTUREFILTER_LINEAR);
    }

    void End()
    {
        if (!g_dev)
            return;
        for (int i = 0; i < 12; i++)
            g_dev->SetState(g_states[i], g_oldStates[i]);
        g_dev->SetTexture(NULL);
        g_dev->SetViewRect(g_oldView);
        g_dev = NULL;
    }

    // ---- quads: pre-transformed vertices, two triangles each ----
    struct Q { float x0, y0, x1, y1, u0, v0, u1, v1; };
    static CKWORD g_idx[6 * 256];
    static bool g_idxReady = false;

    static void Quads(CKTexture *tex, const Q *q, int n, unsigned int argb)
    {
        if (!g_dev || n <= 0)
            return;
        if (n > 256)
            n = 256;
        if (!g_idxReady)
        {
            for (int i = 0; i < 256; i++)
            {
                CKWORD b = (CKWORD)(i * 4);
                g_idx[i * 6 + 0] = b; g_idx[i * 6 + 1] = b + 1; g_idx[i * 6 + 2] = b + 2;
                g_idx[i * 6 + 3] = b; g_idx[i * 6 + 4] = b + 2; g_idx[i * 6 + 5] = b + 3;
            }
            g_idxReady = true;
        }
        VxDrawPrimitiveData *d = g_dev->GetDrawPrimitiveStructure(CKRST_DP_CL_VCT, n * 4);
        if (!d || !d->PositionPtr || !d->ColorPtr || !d->TexCoordPtr)
            return;
        char *pos = (char *)d->PositionPtr, *col = (char *)d->ColorPtr, *uv = (char *)d->TexCoordPtr;
        for (int i = 0; i < n; i++)
        {
            // pixel centres: -0.5 keeps glyphs and text crisp
            float xs[4] = { q[i].x0 - 0.5f, q[i].x1 - 0.5f, q[i].x1 - 0.5f, q[i].x0 - 0.5f };
            float ys[4] = { q[i].y0 - 0.5f, q[i].y0 - 0.5f, q[i].y1 - 0.5f, q[i].y1 - 0.5f };
            float us[4] = { q[i].u0, q[i].u1, q[i].u1, q[i].u0 };
            float vs[4] = { q[i].v0, q[i].v0, q[i].v1, q[i].v1 };
            for (int k = 0; k < 4; k++)
            {
                VxVector4 *p = (VxVector4 *)pos;
                p->x = xs[k]; p->y = ys[k]; p->z = 0.0f; p->w = 1.0f;
                *(CKDWORD *)col = argb;
                VxUV *t = (VxUV *)uv;
                t->u = us[k]; t->v = vs[k];
                pos += d->PositionStride;
                col += d->ColorStride;
                uv += d->TexCoordStride;
            }
        }
        g_dev->SetTexture(tex);
        g_dev->DrawPrimitive(VX_TRIANGLELIST, g_idx, n * 6, d);
    }

    void Rect(float x0, float y0, float x1, float y1, unsigned int argb)
    {
        Q q = { x0, y0, x1, y1, 0, 0, 1, 1 };
        Quads(NULL, &q, 1, argb);
    }

    void Image(CKTexture *tex, float x0, float y0, float x1, float y1, unsigned int argb)
    {
        if (!tex)
            return;
        Q q = { x0, y0, x1, y1, 0, 0, 1, 1 };
        Quads(tex, &q, 1, argb);
    }

    // UTF-8 (the source texts) to Latin-1 code points
    static int Next(const char *&s)
    {
        unsigned char c = (unsigned char)*s;
        if (!c)
            return 0;
        s++;
        if (c < 0x80)
            return c;
        if ((c & 0xE0) == 0xC0 && *s)
        {
            int cp = ((c & 0x1F) << 6) | ((unsigned char)*s & 0x3F);
            s++;
            return cp;
        }
        while ((*s & 0xC0) == 0x80)
            s++;                   // outside Latin-1: skipped
        return '?';
    }

    float TextWidth(const char *utf8, float size)
    {
        if (g_size <= 0)
            return 0;
        float k = size / g_size, w = 0;
        const char *s = utf8;
        int c;
        while ((c = Next(s)) != 0)
            if (c < 256 && g_has[c])
                w += g_glyph[c].adv * k;
        return w;
    }

    void Text(float x, float y, float size, unsigned int argb, const char *utf8)
    {
        if (!g_ctx || g_size <= 0)
            return;
        CKTexture *font = g_fontTex ? (CKTexture *)g_ctx->GetObject(g_fontTex) : NULL;
        if (!font)
        {
            g_fontTex = 0;
            font = Texture("BallanceHD\\font.tga");
            if (!font)
                return;
            g_fontTex = font->GetID();
        }
        float k = size / g_size;
        Q q[256];
        int n = 0;
        const char *s = utf8;
        int c;
        float px = x;
        while ((c = Next(s)) != 0 && n < 256)
        {
            if (c >= 256 || !g_has[c])
                continue;
            const Glyph &g = g_glyph[c];
            if (g.w > 0 && g.h > 0)
            {
                Q &e = q[n++];
                e.x0 = px + g.xo * k;
                e.y0 = y + g.yo * k;
                e.x1 = e.x0 + g.w * k;
                e.y1 = e.y0 + g.h * k;
                e.u0 = (float)g.x / g_aw;
                e.v0 = (float)g.y / g_ah;
                e.u1 = (float)(g.x + g.w) / g_aw;
                e.v1 = (float)(g.y + g.h) / g_ah;
            }
            px += g.adv * k;
        }
        Quads(font, q, n, argb);
    }
}
