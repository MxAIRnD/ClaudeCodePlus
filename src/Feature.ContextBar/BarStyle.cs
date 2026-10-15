using System;
using System.Collections.Generic;
using System.Drawing;

namespace Feature.ContextBar;

/// <summary>How the unused rest of the window is shown, so the line's full length can be seen.</summary>
public enum TrackKind
{
    /// <summary>Nothing: the line ends at the tip.</summary>
    None,

    /// <summary>The rest of the line in light grey.</summary>
    Grey,

    /// <summary>The rest of the line as a dotted grey line.</summary>
    Dotted,

    /// <summary>The whole gradient faintly visible along the full length; the used part at full strength.</summary>
    Ghost,

    /// <summary>A thin rounded outline the full length, the line running inside it.</summary>
    Tube,

    /// <summary>The tube with a glassy inner tint and a highlight along the used part.</summary>
    Glass,
}

/// <summary>
/// How the context line looks: the gradient along the window (positions 0..1 with a color at
/// each; the first at 0, the last at 1), an optional neon glow under the line (a blur of the
/// line and the knob: its radius in pixels at a 40 px row, and its strength, 1 soft and 2
/// strong; 0 for none), the knob at the tip (white with a ring in the tip's color, or a dot in
/// a lighter tip color), how much darker than the tip's color the percentage is written (0
/// the color itself, 1 black: a light background needs some), how the unused rest of the
/// window is shown, and the line's thickness in pixels at a 40 px row. The named presets are
/// the schemes on offer; <see cref="Default"/> is the one drawn.
/// </summary>
/// <param name="Name">The scheme's name, for a preview sheet.</param>
/// <param name="Stops">The gradient, as (position, color) in increasing position from 0 to 1.</param>
/// <param name="GlowRadius">The blur radius of the halo, in pixels at a 40 px row; 0 for no halo.</param>
/// <param name="GlowStrength">How strong the halo is: 1 soft, 2 strong; 0 for none.</param>
/// <param name="WhiteTip">True draws a white knob with a ring at the tip; false a dot in a lighter tip color.</param>
/// <param name="TextDarken">How far the percentage's color is moved from the tip's color towards black, 0..1.</param>
/// <param name="Track">How the unused rest of the window is shown.</param>
/// <param name="LineHeight">The line's thickness in pixels at a 40 px row.</param>
/// <param name="GhostAlpha">The alpha (0..255) of the ghost track's faint gradient; read for <see cref="TrackKind.Ghost"/> only.</param>
public sealed record BarStyle(
    string Name,
    IReadOnlyList<(float Position, Color Color)> Stops,
    float GlowRadius,
    float GlowStrength,
    bool WhiteTip,
    float TextDarken,
    TrackKind Track,
    float LineHeight,
    int GhostAlpha = 55)
{
    private static readonly (float, Color)[] AuroraStops =
    {
        (0f, Hex("22d3ee")), (0.25f, Hex("22c55e")), (0.5f, Hex("a3e635")), (0.65f, Hex("facc15")), (0.8f, Hex("fb923c")), (1f, Hex("ef4444")),
    };

    // Green from 15% to 35% of the window and over by 40%; gold at 50%, orange at 70%, red from 90% to the end.
    private static readonly (float, Color)[] AuroraBoldStops =
    {
        (0f, Hex("0ea5e9")), (0.15f, Hex("16a34a")), (0.35f, Hex("16a34a")), (0.42f, Hex("84cc16")), (0.5f, Hex("eab308")), (0.7f, Hex("f97316")), (0.9f, Hex("dc2626")), (1f, Hex("dc2626")),
    };

    /// <summary>Cyan at the start through green, lime, yellow and orange to red; the original, paler scheme.</summary>
    public static BarStyle Aurora { get; } = new("Aurora", AuroraStops, 0f, 0f, true, 0.3f, TrackKind.None, 5f);

    /// <summary>Sky through green, lime, gold and orange to red: the chosen scheme, the line ending at the tip.</summary>
    public static BarStyle AuroraBold { get; } = new("Aurora Bold", AuroraBoldStops, 0f, 0f, true, 0.3f, TrackKind.None, 5f);

    /// <summary>Aurora Bold with the rest of the line in grey.</summary>
    public static BarStyle AuroraBoldGrey { get; } = AuroraBold with { Name = "Aurora Bold · grey", Track = TrackKind.Grey };

    /// <summary>Aurora Bold with the rest of the line dotted.</summary>
    public static BarStyle AuroraBoldDotted { get; } = AuroraBold with { Name = "Aurora Bold · dotted", Track = TrackKind.Dotted };

    /// <summary>Aurora Bold with the whole gradient faintly visible (alpha 30): the chosen look.</summary>
    public static BarStyle AuroraBoldGhost { get; } = AuroraBold with { Name = "Aurora Bold · ghost", Track = TrackKind.Ghost, GhostAlpha = 30 };

    /// <summary>Aurora Bold inside a thin tube.</summary>
    public static BarStyle AuroraBoldTube { get; } = AuroraBold with { Name = "Aurora Bold · tube", Track = TrackKind.Tube };

    /// <summary>Aurora Bold inside a glassy tube.</summary>
    public static BarStyle AuroraBoldGlass { get; } = AuroraBold with { Name = "Aurora Bold · glass", Track = TrackKind.Glass };

    /// <summary>Every preset on the preview sheet, in its order: the chosen look first, then the other track kinds.</summary>
    public static IReadOnlyList<BarStyle> Presets { get; } = new[] { AuroraBoldGhost, AuroraBold, AuroraBoldGrey, AuroraBoldDotted, AuroraBoldTube, AuroraBoldGlass };

    /// <summary>The scheme the line is drawn with.</summary>
    public static BarStyle Default => AuroraBoldGhost;

    private static Color Hex(string rgb) =>
        Color.FromArgb(
            Convert.ToInt32(rgb.Substring(0, 2), 16),
            Convert.ToInt32(rgb.Substring(2, 2), 16),
            Convert.ToInt32(rgb.Substring(4, 2), 16));
}
