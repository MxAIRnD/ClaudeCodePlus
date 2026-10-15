using System;
using System.Globalization;

namespace Feature.ContextBar;

/// <summary>
/// The context figures one session published: what the context-bar mod writes to
/// <c>&lt;config dir&gt;\context-bar\&lt;session id&gt;.json</c> after every change (the input
/// side of the last response against the model's window, as the engine's status line has them).
/// </summary>
/// <param name="SessionId">The engine's session id (the desktop store's <c>cliSessionId</c>).</param>
/// <param name="Tokens">Input tokens of the last response; null before the first response.</param>
/// <param name="Window">The model's context window, in tokens; positive.</param>
/// <param name="Threshold">The token count at which auto-compaction runs; null when unknown or off.</param>
/// <param name="AtMs">When the figures were read, Unix milliseconds; 0 when the file has none.</param>
/// <param name="IsHidden">Whether the person hid the bar (<c>/ctxbar off</c>).</param>
public sealed record ContextFigures(string SessionId, long? Tokens, long Window, long? Threshold, long AtMs, bool IsHidden)
{
    /// <summary><see cref="Tokens"/> over <see cref="Window"/>, clamped to 0..1; 0 without a reading.</summary>
    public double Fraction => Tokens is long t && Window > 0 ? Math.Clamp((double)t / Window, 0, 1) : 0;

    /// <summary><see cref="Threshold"/> over <see cref="Window"/> when strictly between 0 and 1; else null.</summary>
    public double? ThresholdFraction =>
        Threshold is long th && Window > 0 && th > 0 && th < Window ? (double)th / Window : null;

    /// <summary>The percentage as drawn, rounded: <c>37%</c>; an em dash without a reading.</summary>
    public string PercentText =>
        Tokens is null ? "—" : ((int)Math.Round(Fraction * 100, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>The counts as drawn: <c>366K / 1M</c> (<see cref="FiguresFile.FormatTokens"/>); the first an em dash without a reading.</summary>
    public string CountsText =>
        (Tokens is long t ? FiguresFile.FormatTokens(t) : "—") + " / " + FiguresFile.FormatTokens(Window);
}
