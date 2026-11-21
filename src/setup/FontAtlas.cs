using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace BallanceHD
{
    /// <summary>
    /// The plugin's texts in the game are drawn from a character atlas. The setup renders it on the player's PC
    /// from the Windows font Segoe UI (part of every Windows 10/11), so no font file is shipped with the patch.
    ///   BallanceHD\font.tga  white, alpha = coverage (32-bit TGA, top-left origin)
    ///   BallanceHD\font.bin  "HDF1", int count, int first, int atlasW, int atlasH, float size, float line,
    ///                        then per character 7 x int16: x, y, w, h, xoff, yoff (to the line top), advance
    /// </summary>
    static class FontAtlas
    {
        public const string Tga = @"BallanceHD\font.tga", Bin = @"BallanceHD\font.bin";

        public static void Make(string gameDir, int size = 48)
        {
            const int first = 32, count = 224, atlasW = 1024;
            using (var font = PickFont(size))
            using (var fmt = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                int cell = size * 2, pad = size / 2;
                var cells = new Bitmap[count];
                var box = new int[count, 4];
                var adv = new int[count];
                using (var probe = new Bitmap(cell, cell, PixelFormat.Format32bppArgb))
                using (var mg = Graphics.FromImage(probe))
                {
                    for (int i = 0; i < count; i++)
                    {
                        char c = (char)(first + i);
                        if (c >= (char)127 && c < (char)160)
                            continue;   // control characters
                        adv[i] = (int)Math.Round(mg.MeasureString(c.ToString(), font, 1000, fmt).Width);
                        var bmp = new Bitmap(cell, cell, PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.Transparent);
                            g.TextRenderingHint = TextRenderingHint.AntiAlias;
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.DrawString(c.ToString(), font, Brushes.White, pad, pad, fmt);
                        }
                        int x0 = cell, y0 = cell, x1 = -1, y1 = -1;
                        var d = bmp.LockBits(new Rectangle(0, 0, cell, cell), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        var px = new byte[d.Stride * cell];
                        Marshal.Copy(d.Scan0, px, 0, px.Length);
                        bmp.UnlockBits(d);
                        for (int y = 0; y < cell; y++)
                            for (int x = 0; x < cell; x++)
                                if (px[y * d.Stride + x * 4 + 3] > 0)
                                {
                                    if (x < x0) x0 = x;
                                    if (y < y0) y0 = y;
                                    if (x > x1) x1 = x;
                                    if (y > y1) y1 = y;
                                }
                        if (x1 < 0)
                        {
                            bmp.Dispose();   // blank (space)
                            continue;
                        }
                        x0 = Math.Max(0, x0 - 1); y0 = Math.Max(0, y0 - 1); x1 = Math.Min(cell - 1, x1 + 1); y1 = Math.Min(cell - 1, y1 + 1);
                        box[i, 0] = x0; box[i, 1] = y0; box[i, 2] = x1 - x0 + 1; box[i, 3] = y1 - y0 + 1;
                        cells[i] = bmp;
                    }
                }
                // shelf packing
                var pos = new int[count, 2];
                int cx = 1, cy = 1, rowH = 0;
                for (int i = 0; i < count; i++)
                {
                    if (cells[i] == null)
                        continue;
                    int w = box[i, 2], h = box[i, 3];
                    if (cx + w + 1 > atlasW) { cx = 1; cy += rowH + 2; rowH = 0; }
                    pos[i, 0] = cx; pos[i, 1] = cy;
                    cx += w + 2;
                    rowH = Math.Max(rowH, h);
                }
                int atlasH = 64;
                while (atlasH < cy + rowH + 1)
                    atlasH *= 2;
                var pixels = new byte[atlasW * atlasH * 4];
                using (var atlas = new Bitmap(atlasW, atlasH, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(atlas))
                    {
                        g.Clear(Color.FromArgb(0, 255, 255, 255));
                        g.CompositingMode = CompositingMode.SourceCopy;
                        for (int i = 0; i < count; i++)
                            if (cells[i] != null)
                                g.DrawImage(cells[i], new Rectangle(pos[i, 0], pos[i, 1], box[i, 2], box[i, 3]),
                                            new Rectangle(box[i, 0], box[i, 1], box[i, 2], box[i, 3]), GraphicsUnit.Pixel);
                    }
                    var ad = atlas.LockBits(new Rectangle(0, 0, atlasW, atlasH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    for (int y = 0; y < atlasH; y++)
                        Marshal.Copy(new IntPtr(ad.Scan0.ToInt64() + (long)y * ad.Stride), pixels, y * atlasW * 4, atlasW * 4);
                    atlas.UnlockBits(ad);
                }
                foreach (var b in cells)
                    if (b != null)
                        b.Dispose();
                for (int p = 0; p < pixels.Length; p += 4)
                {
                    pixels[p] = 255; pixels[p + 1] = 255; pixels[p + 2] = 255;
                }
                var hdr = new byte[18];
                hdr[2] = 2; hdr[12] = (byte)(atlasW & 255); hdr[13] = (byte)(atlasW >> 8); hdr[14] = (byte)(atlasH & 255); hdr[15] = (byte)(atlasH >> 8);
                hdr[16] = 32; hdr[17] = 0x28;
                Directory.CreateDirectory(Path.Combine(gameDir, Path.GetDirectoryName(Tga)));
                using (var ms = new MemoryStream())
                {
                    ms.Write(hdr, 0, 18);
                    ms.Write(pixels, 0, pixels.Length);
                    FileUtil.WriteAtomic(Path.Combine(gameDir, Tga), ms.ToArray());
                }
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(new[] { (byte)'H', (byte)'D', (byte)'F', (byte)'1' });
                    bw.Write(count); bw.Write(first); bw.Write(atlasW); bw.Write(atlasH);
                    bw.Write((float)size); bw.Write((float)font.GetHeight());
                    for (int i = 0; i < count; i++)
                    {
                        bool has = cells[i] != null;
                        bw.Write((short)(has ? pos[i, 0] : 0)); bw.Write((short)(has ? pos[i, 1] : 0));
                        bw.Write((short)(has ? box[i, 2] : 0)); bw.Write((short)(has ? box[i, 3] : 0));
                        bw.Write((short)(has ? box[i, 0] - pad : 0)); bw.Write((short)(has ? box[i, 1] - pad : 0));
                        bw.Write((short)adv[i]);
                    }
                    bw.Flush();
                    FileUtil.WriteAtomic(Path.Combine(gameDir, Bin), ms.ToArray());
                }
            }
        }

        static Font PickFont(int size)
        {
            foreach (var name in new[] { "Segoe UI", "Tahoma", "Arial" })
            {
                var f = new Font(name, size, FontStyle.Regular, GraphicsUnit.Pixel);
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                    return f;
                f.Dispose();
            }
            return new Font(FontFamily.GenericSansSerif, size, FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }
}
