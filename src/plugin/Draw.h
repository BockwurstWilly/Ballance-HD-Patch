// Ballance HD plugin - a small 2D drawing layer on top of the Virtools render context:
// screen-space quads (colour, texture), button glyph textures and text from a font atlas.
#pragma once

class CKContext;
class CKRenderContext;
class CKTexture;

namespace draw
{
    void Init(CKContext *context);                 // loads the font atlas (BallanceHD\font.tga + font.bin)
    CKTexture *Texture(const char *relative);      // a TGA from the game folder, loaded once (null if missing)

    void Begin(CKRenderContext *dev);              // sets the render states for 2D drawing
    void End();                                    // restores them

    unsigned int Argb(float r, float g, float b, float a);
    void Rect(float x0, float y0, float x1, float y1, unsigned int argb);
    void Image(CKTexture *tex, float x0, float y0, float x1, float y1, unsigned int argb);
    float TextWidth(const char *utf8, float size);
    void Text(float x, float y, float size, unsigned int argb, const char *utf8);   // y = top of the line
}
