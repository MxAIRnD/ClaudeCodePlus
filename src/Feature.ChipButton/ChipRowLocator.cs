using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace Feature.ChipButton;

/// <summary>
/// Finds where the button goes, as plain functions over UI Automation snapshots. Two layouts of
/// the Claude app's new-session composer are known. The older one draws a chip row —
/// environment, folder, branch, the "worktree" box, the "Add another folder" button — and the
/// button is placed right after its last chip (names read on app build 2.26454.2, 2026-10-08).
/// The newer one (the "environments" start card, read on app build 2.37337.1, 2026-10-14) has
/// no such row: the composer's bottom row starts with the environment chip ("Environment: AI")
/// and the permissions chip, and the button is placed right after those. Either way at the same
/// top and height. Names are the app's accessible names; they are a foreign contract and may
/// change with an app update, which is what <see cref="Report"/> is for.
/// </summary>
public static class ChipRowLocator
{
    /// <summary>Accessible name of the chip row's last button (folder icon with a plus).</summary>
    public const string AddFolderName = "Add another folder";

    /// <summary>Accessible name of the worktree check box and of its label.</summary>
    public const string WorktreeName = "worktree";

    /// <summary>
    /// Start of the accessible name of the newer layout's environment chip; the environment's
    /// name follows ("Environment: AI"). Only the new-session composer has it: a running
    /// session shows its environment in a side panel instead.
    /// </summary>
    public const string EnvironmentChipPrefix = "Environment: ";

    /// <summary>
    /// Start of the accessible name of the composer's model chip ("Model: Fable 5.1"), on the
    /// new-session screen and in a running session alike: a drawn one means a composer is on
    /// screen (the diagnostics' test).
    /// </summary>
    public const string ModelChipPrefix = "Model: ";

    /// <summary>
    /// The page URL of a running session, as a regular expression: the app routes a session's
    /// page to <c>https://claude.ai/epitaxy/local_&lt;uuid&gt;</c> (read on app build
    /// 2.37337.1, 2026-10-15). A document with such a URL means no chip row is expected.
    /// </summary>
    public static readonly Regex SessionUrl = new(@"^https://claude\.ai/epitaxy/(local_[0-9a-f-]{36})$", RegexOptions.CultureInvariant);

    // A packaged install's page URL names the app's version: …/WindowsApps/Claude_2.37337.1.0_x64__…/
    private static readonly Regex PackagedVersion = new(@"/Claude_([0-9]+(?:\.[0-9]+)+)_", RegexOptions.CultureInvariant);

    // A session id anywhere in a text, to cut it to its first 8 hex digits in a report.
    private static readonly Regex AnyLocalId = new(@"local_([0-9a-f]{8})[0-9a-f-]{28}", RegexOptions.CultureInvariant);

    private const int MaxNameInReport = 40;
    private const int MaxUrlInReport = 120;

    /// <summary>
    /// The rectangle the button is placed after, or <c>null</c> when no new-session composer is
    /// visible. Only drawn elements count (not off screen, non-empty bounds). First choice: the
    /// <see cref="AddFolderName"/> button (the lowest one if there are several). Else the
    /// <see cref="WorktreeName"/> check box, joined with a <see cref="WorktreeName"/> text label
    /// on its row to its right (row = vertical centres closer than the box's height). Else the
    /// environment chip (a button whose name starts with <see cref="EnvironmentChipPrefix"/>,
    /// the lowest one if there are several), joined with the buttons that follow it on its row,
    /// each starting at most one chip height after the previous one ends. Names ignore case;
    /// the first two match exactly. Throws <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static Rectangle? FindAnchor(IReadOnlyList<ChipElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        Rectangle? addFolder = null;
        Rectangle? box = null;
        Rectangle? environment = null;
        foreach (ChipElement e in elements)
        {
            if (!IsDrawn(e))
            {
                continue;
            }

            if (e.Kind == ChipElementKind.Button && Is(e, AddFolderName)
                && (addFolder is null || e.Bounds.Y > addFolder.Value.Y))
            {
                addFolder = e.Bounds;
            }
            else if (e.Kind == ChipElementKind.CheckBox && Is(e, WorktreeName)
                && (box is null || e.Bounds.Y > box.Value.Y))
            {
                box = e.Bounds;
            }
            else if (e.Kind == ChipElementKind.Button && StartsWith(e, EnvironmentChipPrefix)
                && (environment is null || e.Bounds.Y > environment.Value.Y))
            {
                environment = e.Bounds;
            }
        }

        if (addFolder is not null)
        {
            return addFolder;
        }

        if (box is null)
        {
            return environment is null ? null : JoinRow(environment.Value, elements);
        }

        Rectangle anchor = box.Value;
        foreach (ChipElement e in elements)
        {
            if (IsDrawn(e) && e.Kind == ChipElementKind.Text && Is(e, WorktreeName)
                && e.Bounds.Left >= box.Value.Left
                && Math.Abs(CenterY(e.Bounds) - CenterY(box.Value)) < box.Value.Height)
            {
                anchor = Rectangle.Union(anchor, e.Bounds);
            }
        }

        return anchor;
    }

    /// <summary>
    /// The button's bounds for an anchor and the width of its text, all in physical pixels: a
    /// gap of a quarter of the anchor's height after the anchor's right edge, the anchor's top
    /// and height, and the text width plus the anchor's height as padding. Throws
    /// <see cref="ArgumentOutOfRangeException"/> for a negative text width.
    /// </summary>
    public static Rectangle ButtonBounds(Rectangle anchor, int textWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(textWidth);
        int gap = anchor.Height / 4;
        return new Rectangle(anchor.Right + gap, anchor.Top, textWidth + anchor.Height, anchor.Height);
    }

    /// <summary>
    /// Whether a button at <paramref name="bounds"/> would cover one of the app's own buttons: a
    /// drawn button that overlaps <paramref name="bounds"/> but not <paramref name="anchor"/>
    /// (the anchor's own chips, and anything around them, are not in the way). The button is
    /// hidden then; a narrow window can leave no room before the composer's right-hand chips.
    /// Edges that only touch do not overlap. Throws <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static bool Collides(Rectangle bounds, Rectangle anchor, IReadOnlyList<ChipElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        foreach (ChipElement e in elements)
        {
            if (IsDrawn(e) && e.Kind == ChipElementKind.Button
                && e.Bounds.IntersectsWith(bounds) && !e.Bounds.IntersectsWith(anchor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The app's version as the shell page's URL names it in a packaged install
    /// (<c>…/WindowsApps/Claude_2.37337.1.0_x64__…/app.asar/…</c>): from the first document
    /// element whose value has it. Null when none has (an unpackaged install, or no document).
    /// Throws <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static string? AppVersion(IReadOnlyList<ChipElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        foreach (ChipElement e in elements)
        {
            if (e.Kind == ChipElementKind.Document && e.Value is not null)
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
    /// What is missing when the button cannot be placed although a Claude window with a composer
    /// is in front: one line for the diagnostics log, or null when there is nothing to report:
    /// Claude not in front (<paramref name="elements"/> empty), an <paramref name="anchor"/>
    /// found (<see cref="FindAnchor"/>'s answer over the same snapshot), a running session shown
    /// (a document whose value matches <see cref="SessionUrl"/>: no chip row is expected there),
    /// or no composer on screen (no drawn button named with <see cref="ModelChipPrefix"/>: a
    /// settings page, say). The line has the app version, each document's name length and URL
    /// (a session id cut to its first 8 hex digits, a URL to 120 characters) and the drawn
    /// buttons on the lowest row (names cut to 40 characters, bounds). Throws
    /// <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static string? Report(IReadOnlyList<ChipElement> elements, Rectangle? anchor)
    {
        ArgumentNullException.ThrowIfNull(elements);
        if (elements.Count == 0 || anchor is not null)
        {
            return null;
        }

        bool composer = false;
        foreach (ChipElement e in elements)
        {
            if (e.Kind == ChipElementKind.Document && e.Value is not null && SessionUrl.IsMatch(e.Value))
            {
                return null;
            }

            composer |= IsDrawn(e) && e.Kind == ChipElementKind.Button && StartsWith(e, ModelChipPrefix);
        }

        if (!composer)
        {
            return null;
        }

        return $"app {AppVersion(elements) ?? "unknown"}: a composer but no chip-row anchor; documents: {DescribeDocuments(elements)}; buttons on the lowest row: {DescribeLowestRow(elements)}";
    }

    // The chip joined with the buttons that follow it on its row: each starts at or after the
    // joined row's right edge, at most the chip's height later, with its vertical centre closer
    // to the chip's than the chip's height. Every join moves the right edge, so this ends.
    private static Rectangle JoinRow(Rectangle chip, IReadOnlyList<ChipElement> elements)
    {
        Rectangle row = chip;
        bool grown = true;
        while (grown)
        {
            grown = false;
            foreach (ChipElement e in elements)
            {
                if (IsDrawn(e) && e.Kind == ChipElementKind.Button
                    && e.Bounds.Left >= row.Right && e.Bounds.Left - row.Right <= chip.Height
                    && Math.Abs(CenterY(e.Bounds) - CenterY(chip)) < chip.Height)
                {
                    row = Rectangle.Union(row, e.Bounds);
                    grown = true;
                }
            }
        }

        return row;
    }

    // The drawn buttons on the lowest row (the row of the button that ends lowest; a row's
    // buttons have vertical centres closer to its than its height): 'name' x,y wxh, left to right.
    private static string DescribeLowestRow(IReadOnlyList<ChipElement> elements)
    {
        Rectangle? lowest = null;
        foreach (ChipElement e in elements)
        {
            if (IsDrawn(e) && e.Kind == ChipElementKind.Button && (lowest is null || e.Bounds.Bottom > lowest.Value.Bottom))
            {
                lowest = e.Bounds;
            }
        }

        if (lowest is null)
        {
            return "none";
        }

        var row = new List<ChipElement>();
        foreach (ChipElement e in elements)
        {
            if (IsDrawn(e) && e.Kind == ChipElementKind.Button && Math.Abs(CenterY(e.Bounds) - CenterY(lowest.Value)) < lowest.Value.Height)
            {
                row.Add(e);
            }
        }

        row.Sort((a, b) => a.Bounds.X.CompareTo(b.Bounds.X));
        var text = new StringBuilder();
        foreach (ChipElement e in row)
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
    private static string DescribeDocuments(IReadOnlyList<ChipElement> elements)
    {
        var text = new StringBuilder();
        foreach (ChipElement e in elements)
        {
            if (e.Kind != ChipElementKind.Document)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append("[name ").Append(e.Name?.Length ?? 0).Append(" chars, url ").Append(Cut(AnyLocalId.Replace(e.Value ?? string.Empty, "local_$1…"), MaxUrlInReport)).Append(']');
        }

        return text.Length == 0 ? "none" : text.ToString();
    }

    private static string Cut(string? text, int max) => text is null ? string.Empty : text.Length > max ? text[..max] + "…" : text;

    private static bool IsDrawn(ChipElement e) => !e.IsOffscreen && e.Bounds.Width > 0 && e.Bounds.Height > 0;

    private static bool Is(ChipElement e, string name) => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWith(ChipElement e, string prefix) => e.Name is not null && e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static double CenterY(Rectangle r) => r.Top + (r.Height / 2.0);
}
