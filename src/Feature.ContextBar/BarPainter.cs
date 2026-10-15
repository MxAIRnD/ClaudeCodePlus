using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Feature.ContextBar;

/// <summary>
/// Draws the context line into a bitmap the size of its slot, in a <see cref="BarStyle"/>. At
/// the left the percentage, in the color at the line's tip, and the counts in grey; over the
/// rest of the width the line: a crisp thin rounded gradient line over the used fraction, the
/// gradient pinned to the whole length (so the tip's color reads the level), a neon halo under
/// it (the line and the knob drawn again, blurred), a knob or dot at the tip, a faint tick
/// where auto-compaction runs, and the style's track behind it, if any. Everything scales with
/// the bitmap's height. Pure drawing: no window, no state.
/// </summary>
public static class BarPainter
{
    private static readonly Color CountsInk = Color.FromArgb(128, 128, 128);
    private static readonly Color TickInk = Color.FromArgb(110, 148, 163, 184);
    private static readonly Color TrackInk = Color.FromArgb(128, 128, 128);

    /// <summary>
    /// The gradient's color at <paramref name="t"/> (0..1; outside it clamped, a NaN as 0), in
    /// <paramref name="style"/> (the <see cref="BarStyle.Default"/> when null).
    /// </summary>
    public static Color ColorAt(double t, BarStyle? style = null)
    {
        IReadOnlyList<(float Position, Color Color)> stops = (style ?? BarStyle.Default).Stops;
        float x = (float)Math.Clamp(double.IsFinite(t) ? t : 0, 0, 1);
        int i = 0;
        while (i < stops.Count - 2 && x > stops[i + 1].Position)
        {
            i++;
        }

        (float p0, Color a) = stops[i];
        (float p1, Color b) = stops[i + 1];
        float f = p1 == p0 ? 0 : (x - p0) / (p1 - p0);
        return Color.FromArgb(Mix(a.R, b.R, f), Mix(a.G, b.G, f), Mix(a.B, b.B, f));
    }

    /// <summary>
    /// Draws <paramref name="figures"/> into <paramref name="target"/> (32-bit ARGB; cleared to
    /// transparent first) in <paramref name="style"/> (the <see cref="BarStyle.Default"/> when
    /// null): the texts at the left, the line over the rest; a slot too narrow for a line after
    /// the texts gets the texts alone. Throws <see cref="ArgumentNullException"/>.
    /// </summary>
    public static void Render(Bitmap target, ContextFigures figures, BarStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(figures);
        BarStyle s = style ?? BarStyle.Default;
        int w = target.Width;
        int h = target.Height;
        using Graphics g = Graphics.FromImage(target);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        Color tip = ColorAt(figures.Fraction, s);
        float fontPx = Math.Max(9f, h * 0.42f);
        using var bold = new Font("Segoe UI", fontPx, FontStyle.Bold, GraphicsUnit.Pixel);
        using var regular = new Font("Segoe UI", fontPx, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces };
        string percent = figures.PercentText;
        string used = figures.Tokens is long tokens ? FiguresFile.FormatTokens(tokens) : "—";
        string window = " / " + FiguresFile.FormatTokens(figures.Window);
        float pad = h / 4f;
        float gap = h * 0.35f;
        SizeF percentSize = g.MeasureString(percent, bold, int.MaxValue, format);
        SizeF usedSize = g.MeasureString(used, bold, int.MaxValue, format);
        SizeF windowSize = g.MeasureString(window, regular, int.MaxValue, format);
        using (var ink = new SolidBrush(Darken(tip, s.TextDarken)))
        {
            g.DrawString(percent, bold, ink, pad, (h - percentSize.Height) / 2f, format);
        }

        using (var ink = new SolidBrush(CountsInk))
        {
            float x = pad + percentSize.Width + gap;
            g.DrawString(used, bold, ink, x, (h - usedSize.Height) / 2f, format);
            g.DrawString(window, regular, ink, x + usedSize.Width, (h - windowSize.Height) / 2f, format);
        }

        float x0 = pad + percentSize.Width + gap + usedSize.Width + windowSize.Width + (h / 2f);
        float x1 = w - pad;
        float length = x1 - x0;
        if (length < h)
        {
            return;
        }

        float y = h / 2f;
        float unit = h / 40f; // the footer row is 40 px tall at the usual display scale
        float thickness = Math.Max(2f, s.LineHeight * unit);

        DrawTrack(g, s, span: new RectangleF(x0, y - thickness, length, thickness * 2), x0, x1, y, thickness, unit);

        if (figures.ThresholdFraction is double tf)
        {
            using var tick = new Pen(TickInk, Math.Max(1f, unit));
            float tx = x0 + (float)(length * tf);
            g.DrawLine(tick, tx, y - (h * 0.18f), tx, y + (h * 0.18f));
        }

        float filled = (float)(length * figures.Fraction);
        if (filled < 1f)
        {
            return;
        }

        var span = new RectangleF(x0, y - thickness, length, thickness * 2);
        float tipX = x0 + filled;
        float knob = thickness * 1.3f;

        if (s.GlowRadius > 0 && s.GlowStrength > 0)
        {
            Glow(g, w, h, span, x0, tipX, y, thickness, knob, tip, s, unit);
        }

        using (LinearGradientBrush brush = Gradient(span, 255, s))
        using (var pen = new Pen(brush, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(pen, x0, y, tipX, y);
        }

        if (s.Track == TrackKind.Glass && filled > thickness * 2)
        {
            // a glossy highlight along the top of the used part
            using var shine = new Pen(Color.FromArgb(110, 255, 255, 255), Math.Max(1f, unit)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(shine, x0 + (thickness / 2f), y - (thickness * 0.28f), tipX - thickness, y - (thickness * 0.28f));
        }

        if (s.WhiteTip)
        {
            Dot(g, tipX, y, knob + (0.9f * unit), tip);
            Dot(g, tipX, y, knob, Color.White);
        }
        else
        {
            Dot(g, tipX, y, thickness * 1.7f, Color.FromArgb(90, tip));
            Dot(g, tipX, y, thickness * 1.05f, Lighten(tip, 0.55f));
        }
    }

    // The unused rest of the window, per the style: a grey or dotted line, the faint whole
    // gradient, or a tube (an outline with a margin around the line, glassy or not), all the
    // full length; the used part is painted over it afterwards.
    private static void DrawTrack(Graphics g, BarStyle s, RectangleF span, float x0, float x1, float y, float thickness, float unit)
    {
        switch (s.Track)
        {
            case TrackKind.Grey:
            {
                using var track = new Pen(Color.FromArgb(70, TrackInk), thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(track, x0, y, x1, y);
                break;
            }

            case TrackKind.Dotted:
            {
                using var track = new Pen(Color.FromArgb(120, TrackInk), Math.Max(1f, thickness * 0.5f))
                {
                    DashStyle = DashStyle.Dot,
                    DashCap = DashCap.Round,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                g.DrawLine(track, x0, y, x1, y);
                break;
            }

            case TrackKind.Ghost:
            {
                using LinearGradientBrush faint = Gradient(span, Math.Clamp(s.GhostAlpha, 0, 255), s);
                using var track = new Pen(faint, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(track, x0, y, x1, y);
                break;
            }

            case TrackKind.Tube:
            case TrackKind.Glass:
            {
                float margin = 2f * unit;
                var tube = new RectangleF(x0 - margin, y - (thickness / 2f) - margin, (x1 - x0) + (2 * margin), thickness + (2 * margin));
                using GraphicsPath path = RoundedRectangle(tube, tube.Height / 2f);
                if (s.Track == TrackKind.Glass)
                {
                    using var tint = new SolidBrush(Color.FromArgb(28, TrackInk));
                    g.FillPath(tint, path);
                    using var shine = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(1f, unit));
                    g.DrawLine(shine, tube.Left + tube.Height, tube.Top + (1.5f * unit), tube.Right - tube.Height, tube.Top + (1.5f * unit));
                }

                using var outline = new Pen(Color.FromArgb(110, TrackInk), Math.Max(1f, unit));
                g.DrawPath(outline, path);
                break;
            }

            default:
                break;
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // The halo: the line (a little thicker) and the knob's disc drawn into their own layer,
    // blurred three times with a box filter (close to a gaussian), amplified, and laid under
    // the crisp line.
    private static void Glow(Graphics g, int w, int h, RectangleF span, float x0, float tipX, float y, float thickness, float knob, Color tip, BarStyle s, float unit)
    {
        int radius = Math.Max(1, (int)Math.Round(s.GlowRadius * unit));
        using var layer = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (Graphics lg = Graphics.FromImage(layer))
        {
            lg.SmoothingMode = SmoothingMode.AntiAlias;
            lg.Clear(Color.Transparent);
            using LinearGradientBrush brush = Gradient(span, 255, s);
            using var pen = new Pen(brush, thickness * 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            lg.DrawLine(pen, x0, y, tipX, y);
            Dot(lg, tipX, y, knob * 1.9f, tip);
        }

        Blur(layer, radius, 3, s.GlowStrength * 2.4f);
        g.DrawImage(layer, 0, 0);
    }

    // Three box-blur passes over premultiplied BGRA, then every channel scaled by `gain` (the
    // blur spreads the alpha thin) and clamped, which keeps the premultiplied invariant.
    private static void Blur(Bitmap bitmap, int radius, int passes, float gain)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        try
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            int stride = data.Stride;
            var a = new byte[stride * h];
            Marshal.Copy(data.Scan0, a, 0, a.Length);
            var b = new byte[a.Length];
            for (int p = 0; p < passes; p++)
            {
                BlurRows(a, b, w, h, stride, radius);
                BlurColumns(b, a, w, h, stride, radius);
            }

            for (int i = 0; i < a.Length; i++)
            {
                a[i] = (byte)Math.Min(255, (int)Math.Round(a[i] * gain));
            }

            Marshal.Copy(a, 0, data.Scan0, a.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    // One horizontal box pass, per channel, pixels outside the row counting as transparent.
    private static void BlurRows(byte[] src, byte[] dst, int w, int h, int stride, int r)
    {
        int window = (2 * r) + 1;
        for (int y = 0; y < h; y++)
        {
            int row = y * stride;
            for (int c = 0; c < 4; c++)
            {
                int sum = 0;
                for (int x = 0; x <= Math.Min(r, w - 1); x++)
                {
                    sum += src[row + (x * 4) + c];
                }

                for (int x = 0; x < w; x++)
                {
                    dst[row + (x * 4) + c] = (byte)(sum / window);
                    int leaving = x - r;
                    int entering = x + r + 1;
                    if (leaving >= 0)
                    {
                        sum -= src[row + (leaving * 4) + c];
                    }

                    if (entering < w)
                    {
                        sum += src[row + (entering * 4) + c];
                    }
                }
            }
        }
    }

    // One vertical box pass, per channel, pixels outside the column counting as transparent.
    private static void BlurColumns(byte[] src, byte[] dst, int w, int h, int stride, int r)
    {
        int window = (2 * r) + 1;
        for (int x = 0; x < w; x++)
        {
            int column = x * 4;
            for (int c = 0; c < 4; c++)
            {
                int sum = 0;
                for (int y = 0; y <= Math.Min(r, h - 1); y++)
                {
                    sum += src[(y * stride) + column + c];
                }

                for (int y = 0; y < h; y++)
                {
                    dst[(y * stride) + column + c] = (byte)(sum / window);
                    int leaving = y - r;
                    int entering = y + r + 1;
                    if (leaving >= 0)
                    {
                        sum -= src[(leaving * stride) + column + c];
                    }

                    if (entering < h)
                    {
                        sum += src[(entering * stride) + column + c];
                    }
                }
            }
        }
    }

    // The gradient over the whole line, every stop at `alpha`; mirrored past the ends so the
    // round caps keep the end colors.
    private static LinearGradientBrush Gradient(RectangleF span, int alpha, BarStyle style)
    {
        var brush = new LinearGradientBrush(span, Color.Black, Color.Black, LinearGradientMode.Horizontal) { WrapMode = WrapMode.TileFlipX };
        var blend = new ColorBlend(style.Stops.Count);
        for (int i = 0; i < style.Stops.Count; i++)
        {
            blend.Positions[i] = style.Stops[i].Position;
            blend.Colors[i] = Color.FromArgb(alpha, style.Stops[i].Color);
        }

        brush.InterpolationColors = blend;
        return brush;
    }

    private static void Dot(Graphics g, float cx, float cy, float radius, Color color)
    {
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, cx - radius, cy - radius, radius * 2, radius * 2);
    }

    private static Color Lighten(Color c, float f) => Color.FromArgb(Mix(c.R, 255, f), Mix(c.G, 255, f), Mix(c.B, 255, f));

    private static Color Darken(Color c, float f) => Color.FromArgb(Mix(c.R, 0, f), Mix(c.G, 0, f), Mix(c.B, 0, f));

    private static int Mix(int a, int b, float f) => (int)Math.Round(a + ((b - a) * f));
}
