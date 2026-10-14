using System;
using System.Collections.Generic;
using System.Drawing;

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
/// change with an app update.
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

    private static bool IsDrawn(ChipElement e) => !e.IsOffscreen && e.Bounds.Width > 0 && e.Bounds.Height > 0;

    private static bool Is(ChipElement e, string name) => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWith(ChipElement e, string prefix) => e.Name is not null && e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static double CenterY(Rectangle r) => r.Top + (r.Height / 2.0);
}
