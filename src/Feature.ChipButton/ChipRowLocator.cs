using System;
using System.Collections.Generic;
using System.Drawing;

namespace Feature.ChipButton;

/// <summary>
/// Finds where the button goes, as plain functions over UI Automation snapshots. The Claude
/// app's new-session composer draws a chip row — environment, folder, branch, the "worktree"
/// box, the "Add another folder" button — and the button is placed right after its last chip,
/// at the same top and height. Names are the app's accessible names, read on app build
/// 2.26454.2 (2026-10-08); they are a foreign contract and may change with an app update.
/// </summary>
public static class ChipRowLocator
{
    /// <summary>Accessible name of the chip row's last button (folder icon with a plus).</summary>
    public const string AddFolderName = "Add another folder";

    /// <summary>Accessible name of the worktree check box and of its label.</summary>
    public const string WorktreeName = "worktree";

    /// <summary>
    /// The rectangle the button is placed after, or <c>null</c> when no chip row is visible.
    /// Only drawn elements count (not off screen, non-empty bounds). First choice: the
    /// <see cref="AddFolderName"/> button (the lowest one if there are several). Else the
    /// <see cref="WorktreeName"/> check box, joined with a <see cref="WorktreeName"/> text label
    /// on its row to its right (row = vertical centres closer than the box's height). Names
    /// match exactly, ignoring case. Throws <see cref="ArgumentNullException"/> for null.
    /// </summary>
    public static Rectangle? FindAnchor(IReadOnlyList<ChipElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        Rectangle? addFolder = null;
        Rectangle? box = null;
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
        }

        if (addFolder is not null)
        {
            return addFolder;
        }

        if (box is null)
        {
            return null;
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

    private static bool IsDrawn(ChipElement e) => !e.IsOffscreen && e.Bounds.Width > 0 && e.Bounds.Height > 0;

    private static bool Is(ChipElement e, string name) => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase);

    private static double CenterY(Rectangle r) => r.Top + (r.Height / 2.0);
}
