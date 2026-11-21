using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BallanceHD
{
    /// <summary>
    /// One or more controller button glyphs side by side (UI Elements by Arks, CC BY 4.0) - Xbox or
    /// PlayStation, following the controller that is connected.
    /// </summary>
    class GlyphIcon : Control
    {
        static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();
        string kind = "xbox";
        string[] keys = new string[0];

        public GlyphIcon()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        public string Kind
        {
            get { return kind; }
            set { if (value != kind) { kind = value; Invalidate(); } }
        }

        public string[] Keys
        {
            get { return keys; }
            set { keys = value ?? new string[0]; Invalidate(); }
        }

        static Image Glyph(string id)
        {
            Image img;
            if (cache.TryGetValue(id, out img))
                return img;
            var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("glyph/" + id + ".png");
            img = s == null ? null : Image.FromStream(s);
            cache[id] = img;
            return img;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            int size = Height;
            int x = 0;
            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;
                var img = Glyph(kind + "_" + key);
                if (img != null)
                    g.DrawImage(img, x, 0, size, size);
                x += size + Theme.S(4);
            }
        }
    }
}
