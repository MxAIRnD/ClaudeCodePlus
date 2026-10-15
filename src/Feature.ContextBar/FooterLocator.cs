using System;
using System.Collections.Generic;
using System.Drawing;

namespace Feature.ContextBar;

/// <summary>
/// Finds where the context line goes, as plain functions over UI Automation snapshots. A running
/// session's prompt footer (the row under the prompt box; names read on app build 2.37337.1,
/// 2026-10-15) starts with the permission-mode chip ("Bypass permissions", named by the mode
/// alone) and ends with the model chip ("Model: Fable 5.1"), the effort chip and the usage chip.
/// The line takes the room between the mode chip and the model chip. The new-session screen's
/// composer has a similar row whose chips are named "Permissions: …" and "Environment: …"; no
/// session is shown there, so the line stays away from it. Names are the app's accessible
/// names; they are a foreign contract and may change with an app update.
/// </summary>
public static class FooterLocator
{
    /// <summary>Start of the accessible name of the footer's model chip; the model's name follows.</summary>
    public const string ModelChipPrefix = "Model: ";

    /// <summary>Start of the new-session composer's permissions chip name; a running session's mode chip has no prefix.</summary>
    public const string NewSessionPermissionsPrefix = "Permissions: ";

    /// <summary>Start of the new-session composer's environment chip name.</summary>
    public const string NewSessionEnvironmentPrefix = "Environment: ";

    /// <summary>The least width of a slot worth drawing in, in chip heights.</summary>
    public const int MinWidthInHeights = 3;

    /// <summary>What the app's page title ends with; the shown session's title comes before it.</summary>
    public const string PageTitleSuffix = " - Claude Code";

    /// <summary>
    /// The title of the session the app shows: the name of the first document element that ends
    /// with <see cref="PageTitleSuffix"/>, less that suffix and trimmed (the app titles its page
    /// after the session in front). Null when there is no such document, or the title is empty.
    /// Throws <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static string? ShownSessionTitle(IReadOnlyList<FooterElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        foreach (FooterElement e in elements)
        {
            if (e.Kind == FooterElementKind.Document && e.Name is not null
                && e.Name.EndsWith(PageTitleSuffix, StringComparison.Ordinal))
            {
                string title = e.Name[..^PageTitleSuffix.Length].Trim();
                if (title.Length > 0)
                {
                    return title;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The rectangle the line is drawn in, or <c>null</c> when no running session's footer is
    /// visible. Only drawn elements count (not off screen, non-empty bounds). The model chip is
    /// a button whose name starts with <see cref="ModelChipPrefix"/> (the lowest one if there
    /// are several); its row holds the buttons whose vertical centres are closer to its own than
    /// its height. On that row, a button named with <see cref="NewSessionPermissionsPrefix"/> or
    /// <see cref="NewSessionEnvironmentPrefix"/> means the new-session composer: null. Else the
    /// left anchor is the row's button ending nearest before the model chip (the mode chip). The
    /// slot runs from the anchor's right edge plus half a chip height to the model chip's left
    /// edge less the same gap, at the model chip's top and height; one narrower than
    /// <see cref="MinWidthInHeights"/> chip heights is null. Names ignore case. Throws
    /// <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static Rectangle? FindSlot(IReadOnlyList<FooterElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        Rectangle? model = null;
        foreach (FooterElement e in elements)
        {
            if (IsDrawn(e) && e.Kind == FooterElementKind.Button && StartsWith(e, ModelChipPrefix)
                && (model is null || e.Bounds.Y > model.Value.Y))
            {
                model = e.Bounds;
            }
        }

        if (model is null)
        {
            return null;
        }

        Rectangle chip = model.Value;
        Rectangle? anchor = null;
        foreach (FooterElement e in elements)
        {
            if (!IsDrawn(e) || e.Kind != FooterElementKind.Button || !OnRow(e.Bounds, chip))
            {
                continue;
            }

            if (StartsWith(e, NewSessionPermissionsPrefix) || StartsWith(e, NewSessionEnvironmentPrefix))
            {
                return null;
            }

            if (e.Bounds.Right <= chip.Left && (anchor is null || e.Bounds.Right > anchor.Value.Right))
            {
                anchor = e.Bounds;
            }
        }

        if (anchor is null)
        {
            return null;
        }

        int gap = chip.Height / 2;
        int left = anchor.Value.Right + gap;
        int width = chip.Left - gap - left;
        return width < chip.Height * MinWidthInHeights ? null : new Rectangle(left, chip.Top, width, chip.Height);
    }

    private static bool IsDrawn(FooterElement e) => !e.IsOffscreen && e.Bounds.Width > 0 && e.Bounds.Height > 0;

    private static bool StartsWith(FooterElement e, string prefix) =>
        e.Name is not null && e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static bool OnRow(Rectangle r, Rectangle chip) => Math.Abs(CenterY(r) - CenterY(chip)) < chip.Height;

    private static double CenterY(Rectangle r) => r.Top + (r.Height / 2.0);
}
