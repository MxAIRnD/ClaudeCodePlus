using System;
using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace Feature.ContextBar.Tests;

/// <summary>Black-box behavior of <see cref="BarPainter"/>: the gradient's colors and what a rendered slot holds.</summary>
public sealed class BarPainterTests
{
    private static ContextFigures Figures(long? tokens, long window = 1_000_000, long? threshold = 920_000) =>
        new("abc", tokens, window, threshold, 0, false);

    private static bool AnyInk(Bitmap b, int x0, int x1, int y, int minAlpha = 40)
    {
        for (int x = Math.Max(0, x0); x < Math.Min(b.Width, x1); x++)
        {
            if (b.GetPixel(x, y).A > minAlpha)
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void The_Aurora_gradient_runs_cyan_through_green_lime_yellow_and_orange_to_red()
    {
        BarStyle aurora = BarStyle.Aurora;
        Assert.Equal(Color.FromArgb(34, 211, 238), BarPainter.ColorAt(0, aurora));
        Assert.Equal(Color.FromArgb(34, 197, 94), BarPainter.ColorAt(0.25, aurora));
        Assert.Equal(Color.FromArgb(163, 230, 53), BarPainter.ColorAt(0.5, aurora));
        Assert.Equal(Color.FromArgb(250, 204, 21), BarPainter.ColorAt(0.65, aurora));
        Assert.Equal(Color.FromArgb(251, 146, 60), BarPainter.ColorAt(0.8, aurora));
        Assert.Equal(Color.FromArgb(239, 68, 68), BarPainter.ColorAt(1, aurora));
        Assert.Equal(Color.FromArgb(34, 211, 238), BarPainter.ColorAt(-1, aurora));
        Assert.Equal(Color.FromArgb(239, 68, 68), BarPainter.ColorAt(2, aurora));
        Assert.Equal(Color.FromArgb(34, 211, 238), BarPainter.ColorAt(double.NaN, aurora));
        // halfway between cyan and green
        Assert.Equal(Color.FromArgb(34, 204, 166), BarPainter.ColorAt(0.125, aurora));
    }

    [Fact]
    public void The_default_scheme_is_Aurora_Bold_sky_to_red_with_the_ghost_track()
    {
        Assert.Same(BarStyle.AuroraBoldGhost, BarStyle.Default);
        Assert.Equal(TrackKind.Ghost, BarStyle.Default.Track);
        Assert.Equal(30, BarStyle.Default.GhostAlpha);
        Assert.Equal(Color.FromArgb(14, 165, 233), BarPainter.ColorAt(0));
        // green holds from 15% to 35% and is over by 40%; gold at 50%, orange at 70%
        Assert.Equal(Color.FromArgb(22, 163, 74), BarPainter.ColorAt(0.15));
        Assert.Equal(Color.FromArgb(22, 163, 74), BarPainter.ColorAt(0.35));
        Assert.Equal(Color.FromArgb(132, 204, 22), BarPainter.ColorAt(0.42));
        Assert.Equal(Color.FromArgb(234, 179, 8), BarPainter.ColorAt(0.5));
        Assert.Equal(Color.FromArgb(249, 115, 22), BarPainter.ColorAt(0.7));
        // red from 90% on
        Assert.Equal(Color.FromArgb(220, 38, 38), BarPainter.ColorAt(0.9));
        Assert.Equal(Color.FromArgb(220, 38, 38), BarPainter.ColorAt(0.95));
        Assert.Equal(Color.FromArgb(220, 38, 38), BarPainter.ColorAt(1));
    }

    [Fact]
    public void Every_track_kind_renders_and_only_the_trackless_one_leaves_the_rest_clear()
    {
        BarStyle[] kinds = { BarStyle.AuroraBold, BarStyle.AuroraBoldGrey, BarStyle.AuroraBoldDotted, BarStyle.AuroraBoldGhost, BarStyle.AuroraBoldTube, BarStyle.AuroraBoldGlass };
        foreach (BarStyle style in kinds)
        {
            using var bitmap = new Bitmap(800, 40, PixelFormat.Format32bppArgb);
            BarPainter.Render(bitmap, Figures(300_000, threshold: null), style);
            // the ghost track is faint (alpha 30), so count any pixel above 15
            bool rest = false;
            for (int y = 10; y < 30 && !rest; y++)
            {
                rest = AnyInk(bitmap, 700, 790, y, minAlpha: 15);
            }

            Assert.True(rest == (style.Track != TrackKind.None), style.Name);
        }
    }

    [Fact]
    public void A_half_used_window_fills_the_left_half_of_the_line_and_a_trackless_style_leaves_the_right_half_clear()
    {
        using var bitmap = new Bitmap(800, 40, PixelFormat.Format32bppArgb);
        BarPainter.Render(bitmap, Figures(500_000, threshold: null), BarStyle.AuroraBold);

        // text at the left, then the line: the line's start is past the text, its end near the middle of the remaining width
        Assert.True(AnyInk(bitmap, 0, 120, 20), "the figures' text is drawn at the left");
        Assert.True(AnyInk(bitmap, 250, 450, 20), "the filled part of the line has ink");
        Assert.False(AnyInk(bitmap, 700, 800, 20), "past the fill nothing is drawn");
        Assert.False(AnyInk(bitmap, 0, 800, 2), "the top row stays clear: the line is thin");
    }

    [Fact]
    public void The_default_ghost_track_shows_the_rest_faintly_and_the_knob_marks_the_tip()
    {
        using var bitmap = new Bitmap(800, 40, PixelFormat.Format32bppArgb);
        BarPainter.Render(bitmap, Figures(500_000, threshold: null));

        int faint = bitmap.GetPixel(760, 20).A;
        int solid = bitmap.GetPixel(300, 20).A;
        Assert.True(faint > 15, "the rest of the line is visible, faintly");
        Assert.True(faint < 80 && solid > 200, $"faint {faint} < solid {solid}");
    }

    [Fact]
    public void An_empty_reading_draws_the_texts_alone_and_a_full_one_reaches_the_right_padding()
    {
        using var empty = new Bitmap(800, 40, PixelFormat.Format32bppArgb);
        BarPainter.Render(empty, Figures(null, threshold: null), BarStyle.AuroraBold);
        Assert.True(AnyInk(empty, 0, 120, 20));
        Assert.False(AnyInk(empty, 300, 800, 20));

        using var full = new Bitmap(800, 40, PixelFormat.Format32bppArgb);
        BarPainter.Render(full, Figures(1_000_000, threshold: null));
        Assert.True(AnyInk(full, 760, 795, 20));
    }

    [Fact]
    public void A_slot_too_narrow_for_the_line_gets_the_texts_only_and_a_tiny_one_does_not_throw()
    {
        using var narrow = new Bitmap(160, 40, PixelFormat.Format32bppArgb);
        BarPainter.Render(narrow, Figures(500_000));
        Assert.True(AnyInk(narrow, 0, 100, 20));

        using var tiny = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        BarPainter.Render(tiny, Figures(500_000));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        using var bitmap = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
        Assert.Throws<ArgumentNullException>(() => BarPainter.Render(null!, Figures(1)));
        Assert.Throws<ArgumentNullException>(() => BarPainter.Render(bitmap, null!));
    }
}
