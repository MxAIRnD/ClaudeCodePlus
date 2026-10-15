using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace Feature.ContextBar;

/// <summary>
/// Finds where the context line goes and which session it belongs to, as plain functions over
/// UI Automation snapshots. A running session's prompt footer (the row under the prompt box;
/// names read on app build 2.37337.1, 2026-10-15) starts with the permission-mode chip ("Bypass
/// permissions", named by the mode alone) and ends with the model chip ("Model: Fable 5.1"),
/// the effort chip and the usage chip. The line takes the room between the mode chip and the
/// model chip. The new-session screen's composer has a similar row whose chips are named
/// "Permissions: …" and "Environment: …"; no session is shown there, so the line stays away
/// from it. The session shown is read from the page document's URL,
/// <c>https://claude.ai/epitaxy/local_&lt;uuid&gt;</c> (the desktop session store's record id;
/// read on the same build). Names and the URL route are the app's; they are a foreign contract
/// and may change with an app update.
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

    /// <summary>
    /// The page URL of a shown session, as a regular expression: the app routes a session's page
    /// to <c>https://claude.ai/epitaxy/local_&lt;uuid&gt;</c>, and the one group is that
    /// <c>local_</c> id (36 hex digits and hyphens after the prefix): the desktop session store's
    /// record id, which the context-bar mod publishes the session's figures under.
    /// </summary>
    public static readonly Regex SessionUrl = new(@"^https://claude\.ai/epitaxy/(local_[0-9a-f-]{36})$", RegexOptions.CultureInvariant);

    // A packaged install's page URL names the app's version: …/WindowsApps/Claude_2.37337.1.0_x64__…/
    private static readonly Regex PackagedVersion = new(@"/Claude_([0-9]+(?:\.[0-9]+)+)_", RegexOptions.CultureInvariant);

    // A session id anywhere in a text, to cut it to its first 8 hex digits in a report.
    private static readonly Regex AnyLocalId = new(@"local_([0-9a-f]{8})[0-9a-f-]{28}", RegexOptions.CultureInvariant);

    private const int MaxNameInReport = 40;
    private const int MaxUrlInReport = 120;

    /// <summary>
    /// The id of the session the app shows: the <c>local_&lt;uuid&gt;</c> of the first document
    /// element whose value is a session page URL (<see cref="SessionUrl"/>). Null when no
    /// document has one: the new-session screen, a settings page, or no page document at all.
    /// Never a guess from anything else. Throws <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static string? ShownSessionId(IReadOnlyList<FooterElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        foreach (FooterElement e in elements)
        {
            if (e.Kind == FooterElementKind.Document && e.Value is not null)
            {
                Match match = SessionUrl.Match(e.Value);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The app's version as the shell page's URL names it in a packaged install
    /// (<c>…/WindowsApps/Claude_2.37337.1.0_x64__…/app.asar/…</c>): from the first document
    /// element whose value has it. Null when none has (an unpackaged install, or no document).
    /// Throws <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static string? AppVersion(IReadOnlyList<FooterElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        foreach (FooterElement e in elements)
        {
            if (e.Kind == FooterElementKind.Document && e.Value is not null)
            {
                Match match = PackagedVersion.Match(e.Value);
                if (match.Success)
                {
                    return match.Groups[1].Value;
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

        Rectangle? model = LowestModelChip(elements);
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

    /// <summary>
    /// What is missing when the line cannot be drawn although a Claude window is in front: one
    /// line for the diagnostics log, or null when nothing is missing or Claude is not in front
    /// (<paramref name="elements"/> empty). <paramref name="slot"/> and <paramref name="sessionId"/>
    /// are <see cref="FindSlot"/>'s and <see cref="ShownSessionId"/>'s answers over the same
    /// snapshot, <paramref name="hasFigures"/> whether that session's figures file was read. The
    /// three reports: a session is shown but there is no slot (with the drawn buttons on the
    /// model chip's row, or on the lowest row when there is no model chip); a slot but no session
    /// page URL (with each document's name length and URL); a slot and a session but no figures
    /// file (the mod is not publishing for that session). Each starts with the app version; a
    /// session id is cut to its first 8 hex digits, a name to 40 characters, a URL to 120. Throws
    /// <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static string? Report(IReadOnlyList<FooterElement> elements, Rectangle? slot, string? sessionId, bool hasFigures)
    {
        ArgumentNullException.ThrowIfNull(elements);
        if (elements.Count == 0)
        {
            return null;
        }

        string app = "app " + (AppVersion(elements) ?? "unknown");
        if (slot is null)
        {
            return sessionId is null
                ? null
                : $"{app}: a session is shown but no footer slot was found; buttons on the model chip's row: {DescribeRow(elements)}";
        }

        if (sessionId is null)
        {
            return $"{app}: a footer slot but no session page URL; documents: {DescribeDocuments(elements)}";
        }

        return hasFigures
            ? null
            : $"{app}: no figures file for the shown session {Shorten(sessionId)} (the context-bar mod is not publishing for it)";
    }

    private static Rectangle? LowestModelChip(IReadOnlyList<FooterElement> elements)
    {
        Rectangle? model = null;
        foreach (FooterElement e in elements)
        {
            if (IsDrawn(e) && e.Kind == FooterElementKind.Button && StartsWith(e, ModelChipPrefix)
                && (model is null || e.Bounds.Y > model.Value.Y))
            {
                model = e.Bounds;
            }
        }

        return model;
    }

    // The drawn buttons on the model chip's row, or on the lowest row of all when there is no
    // model chip: 'name' x,y wxh, left to right; "none" without a drawn button.
    private static string DescribeRow(IReadOnlyList<FooterElement> elements)
    {
        Rectangle? reference = LowestModelChip(elements);
        if (reference is null)
        {
            foreach (FooterElement e in elements)
            {
                if (IsDrawn(e) && e.Kind == FooterElementKind.Button && (reference is null || e.Bounds.Bottom > reference.Value.Bottom))
                {
                    reference = e.Bounds;
                }
            }
        }

        if (reference is null)
        {
            return "none";
        }

        var row = new List<FooterElement>();
        foreach (FooterElement e in elements)
        {
            if (IsDrawn(e) && e.Kind == FooterElementKind.Button && OnRow(e.Bounds, reference.Value))
            {
                row.Add(e);
            }
        }

        row.Sort((a, b) => a.Bounds.X.CompareTo(b.Bounds.X));
        var text = new StringBuilder();
        foreach (FooterElement e in row)
        {
            if (text.Length > 0)
            {
                text.Append(" | ");
            }

            text.Append('\'').Append(Cut(e.Name, MaxNameInReport)).Append("' ")
                .Append(e.Bounds.X).Append(',').Append(e.Bounds.Y).Append(' ').Append(e.Bounds.Width).Append('x').Append(e.Bounds.Height);
        }

        return text.ToString();
    }

    // Every document: its name's length and its URL, ids cut: [name 44 chars, url …].
    private static string DescribeDocuments(IReadOnlyList<FooterElement> elements)
    {
        var text = new StringBuilder();
        foreach (FooterElement e in elements)
        {
            if (e.Kind != FooterElementKind.Document)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append("[name ").Append(e.Name?.Length ?? 0).Append(" chars, url ").Append(Shorten(e.Value ?? string.Empty)).Append(']');
        }

        return text.Length == 0 ? "none" : text.ToString();
    }

    private static string Shorten(string text) => Cut(AnyLocalId.Replace(text, "local_$1…"), MaxUrlInReport);

    private static string Cut(string? text, int max) => text is null ? string.Empty : text.Length > max ? text[..max] + "…" : text;

    private static bool IsDrawn(FooterElement e) => !e.IsOffscreen && e.Bounds.Width > 0 && e.Bounds.Height > 0;

    private static bool StartsWith(FooterElement e, string prefix) =>
        e.Name is not null && e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static bool OnRow(Rectangle r, Rectangle chip) => Math.Abs(CenterY(r) - CenterY(chip)) < chip.Height;

    private static double CenterY(Rectangle r) => r.Top + (r.Height / 2.0);
}
