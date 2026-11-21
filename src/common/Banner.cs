using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace BallanceHD
{
    /// <summary>The title banner of launcher and setup: game title plus the game's own splash picture.</summary>
    static class Banner
    {
        /// <summary>Bin\splash.bmp of the installed game (the package itself carries no game artwork); null if none.</summary>
        public static Bitmap Load(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir))
                    return null;
                var path = Path.Combine(dir, @"Bin\splash.bmp");
                if (!File.Exists(path))
                    return null;
                using (var img = Image.FromFile(path))
                {
                    // the lower part shows the scene; the upper part carries the "Ballance" lettering
                    // (and the version number in the bottom right corner is cut off as well)
                    int top = img.Height * 30 / 100, bottom = img.Height * 88 / 100, right = img.Width * 93 / 100;
                    var bmp = new Bitmap(right, bottom - top);
                    using (var g = Graphics.FromImage(bmp))
                        g.DrawImage(img, new Rectangle(0, 0, bmp.Width, bmp.Height), new Rectangle(0, top, right, bottom - top), GraphicsUnit.Pixel);
                    return bmp;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Paint(Graphics g, Rectangle r, Bitmap photo, string tag)
        {
            // dusk sky: night blue at the top, a warm glow towards the horizon
            using (var sky = new LinearGradientBrush(r, Color.FromArgb(18, 20, 34), Color.FromArgb(70, 42, 40), LinearGradientMode.Vertical))
                g.FillRectangle(sky, r);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            if (photo != null)
            {
                // the picture on the right, faded into the sky on the left
                int w = Math.Min(r.Width * 3 / 5, r.Height * photo.Width / photo.Height);
                var dst = new Rectangle(r.Right - w, r.Top, w, r.Height);
                int srcH = photo.Width * r.Height / w;
                srcH = Math.Min(srcH, photo.Height);
                g.DrawImage(photo, dst, new Rectangle(0, photo.Height - srcH, photo.Width, srcH), GraphicsUnit.Pixel);
                using (var fade = new LinearGradientBrush(new Rectangle(dst.Left - 1, r.Top, w / 2 + 2, r.Height), Color.FromArgb(30, 24, 36), Color.FromArgb(0, 30, 24, 36), LinearGradientMode.Horizontal))
                    g.FillRectangle(fade, dst.Left, r.Top, w / 2, r.Height);
            }
            int fh = Theme.S(30);
            using (var fade = new LinearGradientBrush(new Rectangle(r.Left, r.Bottom - fh - 1, r.Width, fh + 2), Color.FromArgb(0, Theme.Back), Theme.Back, LinearGradientMode.Vertical))
                g.FillRectangle(fade, r.Left, r.Bottom - fh, r.Width, fh);

            // the title block is laid out for a 150-unit banner and shrinks with smaller ones
            float k = r.Height / (float)Theme.S(150);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var big = Theme.Display(34f * k))
            using (var sub = Theme.Display(12f * k, FontStyle.Regular))
            using (var type = new Font("Consolas", 9f * Math.Max(k, 0.85f), FontStyle.Bold))
            using (var shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
            using (var warm = new SolidBrush(Theme.Brass))
            using (var text = new SolidBrush(Theme.Text))
            using (var glow = new SolidBrush(Theme.AccentLight))
            {
                Func<float, float> s = v => Theme.S(v * k);
                // a small rolling ball in front of the title
                float d = s(30), bx = s(24), by = r.Top + s(34);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(bx, by, d, d);
                    using (var ball = new PathGradientBrush(path))
                    {
                        ball.CenterPoint = new PointF(bx + d * 0.35f, by + d * 0.3f);
                        ball.CenterColor = Color.FromArgb(255, 236, 200, 150);
                        ball.SurroundColors = new[] { Color.FromArgb(255, 110, 70, 36) };
                        g.FillPath(ball, path);
                    }
                }
                using (var stripe = new Pen(Color.FromArgb(150, 80, 48, 22), Math.Max(1f, s(2))))
                {
                    g.DrawArc(stripe, bx + d * 0.18f, by, d * 0.64f, d, 0, 360);
                    g.DrawLine(stripe, bx + d * 0.06f, by + d * 0.5f, bx + d * 0.94f, by + d * 0.5f);
                }
                g.DrawString("BALLANCE", big, shadow, s(64), r.Top + s(24));
                g.DrawString("BALLANCE", big, text, s(62), r.Top + s(22));
                g.DrawString("H D   E D I T I O N", sub, warm, s(66), r.Top + s(78));
                Theme.Rule(g, (int)s(26), (int)s(330), r.Top + (int)s(110));
                g.DrawString(tag, type, glow, s(26), r.Top + s(120));
            }
            g.SmoothingMode = SmoothingMode.Default;
        }
    }
}
