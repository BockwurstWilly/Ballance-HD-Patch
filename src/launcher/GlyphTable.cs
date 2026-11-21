using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BallanceHD
{
    /// <summary>
    /// The controller layout as a table of button glyphs (UI Elements by Arks, CC BY 4.0) - Xbox or PlayStation,
    /// following the controller that is connected.
    /// </summary>
    class GlyphTable : Control
    {
        public class Row
        {
            public readonly string[] Keys;
            public readonly string Action;
            public Row(string[] keys, string action) { Keys = keys; Action = action; }
        }

        public Row[] Rows = new Row[0];
        string kind = "xbox";
        readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();

        public GlyphTable()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public string Kind
        {
            get { return kind; }
            set
            {
                if (value == kind)
                    return;
                kind = value;
                Invalidate();
            }
        }

        Image Glyph(string key)
        {
            var id = kind + "_" + key;
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
            using (var border = new SolidBrush(Theme.Border))
                g.FillRectangle(border, ClientRectangle);
            if (Rows.Length == 0)
                return;
            float rowH = (Height - 2f) / Rows.Length;
            int glyph = (int)(rowH - Theme.S(5));
            for (int i = 0; i < Rows.Length; i++)
            {
                var r = new RectangleF(1, 1 + i * rowH, Width - 2, rowH - 1);
                using (var b = new SolidBrush(i % 2 == 0 ? Theme.PanelLight : Theme.Panel))
                    g.FillRectangle(b, r);
                float x = r.X + Theme.S(8);
                foreach (var key in Rows[i].Keys)
                {
                    var img = Glyph(key);
                    if (img != null)
                        g.DrawImage(img, x, r.Y + (r.Height - glyph) / 2f, glyph, glyph);
                    x += glyph + Theme.S(4);
                }
                TextRenderer.DrawText(g, Rows[i].Action, Theme.Body, new Rectangle((int)(r.X + Theme.S(190)), (int)r.Y, (int)(r.Width - Theme.S(190)), (int)r.Height),
                    Theme.AccentLight, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var img in cache.Values)
                    if (img != null)
                        img.Dispose();
            base.Dispose(disposing);
        }
    }
}
