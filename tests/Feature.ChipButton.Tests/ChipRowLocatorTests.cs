using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Xunit;

namespace Feature.ChipButton.Tests;

/// <summary>
/// Black-box behavior of <see cref="ChipRowLocator.FindAnchor"/> and
/// <see cref="ChipRowLocator.ButtonBounds"/> over in-memory element snapshots.
/// </summary>
public sealed class ChipRowLocatorTests
{
    // The new-session chip row as UI Automation reported it (physical px), names only, no
    // session content. The ComboBox (branch) maps to Other.
    private static readonly Rectangle AddFolder = new(1313, 1340, 52, 45);
    private static readonly Rectangle Box = new(1170, 1351, 23, 23);
    private static readonly Rectangle Label = new(1198, 1345, 94, 33);

    private static ChipElement Button(string name, Rectangle bounds) => new(name, ChipElementKind.Button, bounds, false);

    private static ChipElement CheckBox(string name, Rectangle bounds) => new(name, ChipElementKind.CheckBox, bounds, false);

    private static ChipElement Text(string name, Rectangle bounds) => new(name, ChipElementKind.Text, bounds, false);

    private static ChipElement Other(string name, Rectangle bounds) => new(name, ChipElementKind.Other, bounds, false);

    private static List<ChipElement> RowWithoutAddFolder() => new()
    {
        Button("Local", new Rectangle(674, 1340, 118, 45)),
        Button("SampleProject", new Rectangle(802, 1340, 232, 45)),
        Other("main", new Rectangle(1044, 1340, 116, 45)),
        CheckBox("worktree", Box),
        Text("worktree", Label),
        Text("Work in an isolated copy of the repository", new Rectangle(1168, 1362, 444, 33)),
    };

    private static List<ChipElement> NewSessionRow()
    {
        List<ChipElement> row = RowWithoutAddFolder();
        row.Add(Button("Add another folder", AddFolder));
        return row;
    }

    // --- contract constants ---

    [Fact]
    public void Names_are_the_apps_accessible_names()
    {
        Assert.Equal("Add another folder", ChipRowLocator.AddFolderName);
        Assert.Equal("worktree", ChipRowLocator.WorktreeName);
    }

    // --- FindAnchor: argument and empty ---

    [Fact]
    public void FindAnchor_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ChipRowLocator.FindAnchor(null!));
    }

    [Fact]
    public void FindAnchor_empty_list_is_null()
    {
        Assert.Null(ChipRowLocator.FindAnchor(Array.Empty<ChipElement>()));
    }

    [Fact]
    public void FindAnchor_running_session_without_the_row_is_null()
    {
        // A running session shows the folder pills but neither "Add another folder" nor "worktree".
        List<ChipElement> elements = new()
        {
            Button("Local", new Rectangle(674, 1340, 118, 45)),
            Button("SampleProject", new Rectangle(802, 1340, 232, 45)),
            Other("main", new Rectangle(1044, 1340, 116, 45)),
            Text("Some made-up reply text", new Rectangle(700, 600, 900, 33)),
        };

        Assert.Null(ChipRowLocator.FindAnchor(elements));
    }

    // --- FindAnchor: "Add another folder" ---

    [Fact]
    public void FindAnchor_new_session_row_is_the_add_folder_button()
    {
        Assert.Equal(new Rectangle(1313, 1340, 52, 45), ChipRowLocator.FindAnchor(NewSessionRow()));
    }

    [Fact]
    public void FindAnchor_add_folder_alone_is_its_bounds()
    {
        Assert.Equal(AddFolder, ChipRowLocator.FindAnchor(new[] { Button("Add another folder", AddFolder) }));
    }

    [Fact]
    public void FindAnchor_add_folder_wins_over_the_worktree_box_in_any_order()
    {
        List<ChipElement> row = NewSessionRow();
        row.Reverse();

        Assert.Equal(AddFolder, ChipRowLocator.FindAnchor(row));
    }

    [Fact]
    public void FindAnchor_several_add_folder_buttons_gives_the_lowest()
    {
        Rectangle upper = new(1313, 400, 52, 45);
        Rectangle lowest = new(900, 1700, 52, 45);
        Rectangle middle = new(1313, 1340, 52, 45);

        Rectangle? anchor = ChipRowLocator.FindAnchor(new[]
        {
            Button("Add another folder", upper),
            Button("Add another folder", lowest),
            Button("Add another folder", middle),
        });

        Assert.Equal(lowest, anchor);
    }

    [Fact]
    public void FindAnchor_lowest_add_folder_does_not_depend_on_order()
    {
        Rectangle upper = new(1313, 400, 52, 45);
        Rectangle lowest = new(1313, 1340, 52, 45);

        Assert.Equal(lowest, ChipRowLocator.FindAnchor(new[] { Button("Add another folder", lowest), Button("Add another folder", upper) }));
        Assert.Equal(lowest, ChipRowLocator.FindAnchor(new[] { Button("Add another folder", upper), Button("Add another folder", lowest) }));
    }

    [Theory]
    [InlineData("add another folder")]
    [InlineData("ADD ANOTHER FOLDER")]
    [InlineData("Add Another Folder")]
    public void FindAnchor_add_folder_name_ignores_case(string name)
    {
        Assert.Equal(AddFolder, ChipRowLocator.FindAnchor(new[] { Button(name, AddFolder) }));
    }

    [Theory]
    [InlineData("Add another folder ")]
    [InlineData(" Add another folder")]
    [InlineData("Add another folders")]
    [InlineData("Add another")]
    [InlineData("Add folder")]
    [InlineData("")]
    public void FindAnchor_add_folder_name_must_match_exactly(string name)
    {
        Assert.Null(ChipRowLocator.FindAnchor(new[] { Button(name, AddFolder) }));
    }

    [Theory]
    [InlineData(ChipElementKind.Text)]
    [InlineData(ChipElementKind.CheckBox)]
    [InlineData(ChipElementKind.Other)]
    public void FindAnchor_add_folder_name_on_a_non_button_is_ignored(ChipElementKind kind)
    {
        Assert.Null(ChipRowLocator.FindAnchor(new[] { new ChipElement("Add another folder", kind, AddFolder, false) }));
    }

    [Fact]
    public void FindAnchor_offscreen_add_folder_is_ignored()
    {
        Assert.Null(ChipRowLocator.FindAnchor(new[] { new ChipElement("Add another folder", ChipElementKind.Button, AddFolder, true) }));
    }

    [Fact]
    public void FindAnchor_offscreen_lower_add_folder_does_not_win()
    {
        Rectangle hiddenLower = new(1313, 1900, 52, 45);

        Rectangle? anchor = ChipRowLocator.FindAnchor(new[]
        {
            Button("Add another folder", AddFolder),
            new ChipElement("Add another folder", ChipElementKind.Button, hiddenLower, true),
        });

        Assert.Equal(AddFolder, anchor);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1313, 1340, 0, 45)]
    [InlineData(1313, 1340, 52, 0)]
    public void FindAnchor_add_folder_with_empty_bounds_is_ignored(int x, int y, int width, int height)
    {
        Assert.Null(ChipRowLocator.FindAnchor(new[] { Button("Add another folder", new Rectangle(x, y, width, height)) }));
    }

    [Fact]
    public void FindAnchor_undrawn_add_folder_falls_back_to_the_worktree_box()
    {
        List<ChipElement> row = RowWithoutAddFolder();
        row.Add(new ChipElement("Add another folder", ChipElementKind.Button, AddFolder, true));
        row.Add(Button("Add another folder", Rectangle.Empty));

        Assert.Equal(Rectangle.Union(Box, Label), ChipRowLocator.FindAnchor(row));
    }

    // --- FindAnchor: worktree fallback ---

    [Fact]
    public void FindAnchor_without_add_folder_joins_the_box_and_its_label()
    {
        // Box 1170..1193 x 1351..1374 joined with label 1198..1292 x 1345..1378.
        Assert.Equal(new Rectangle(1170, 1345, 122, 33), ChipRowLocator.FindAnchor(RowWithoutAddFolder()));
    }

    [Fact]
    public void FindAnchor_join_does_not_depend_on_order()
    {
        List<ChipElement> row = RowWithoutAddFolder();
        row.Reverse();

        Assert.Equal(new Rectangle(1170, 1345, 122, 33), ChipRowLocator.FindAnchor(row));
    }

    [Fact]
    public void FindAnchor_box_without_a_label_is_the_box()
    {
        Assert.Equal(Box, ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box) }));
    }

    [Theory]
    [InlineData("WORKTREE", "Worktree")]
    [InlineData("Worktree", "WorkTree")]
    public void FindAnchor_worktree_names_ignore_case(string boxName, string labelName)
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(new[] { CheckBox(boxName, Box), Text(labelName, Label) });

        Assert.Equal(Rectangle.Union(Box, Label), anchor);
    }

    [Theory]
    [InlineData("worktree ")]
    [InlineData("worktrees")]
    [InlineData("work tree")]
    [InlineData("Work in an isolated copy of the repository")]
    public void FindAnchor_box_name_must_match_exactly(string name)
    {
        Assert.Null(ChipRowLocator.FindAnchor(new[] { CheckBox(name, Box), Text("worktree", Label) }));
    }

    [Theory]
    [InlineData("worktree ")]
    [InlineData("worktrees")]
    [InlineData("Work in an isolated copy of the repository")]
    public void FindAnchor_label_name_must_match_exactly(string name)
    {
        Assert.Equal(Box, ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text(name, Label) }));
    }

    [Theory]
    [InlineData(ChipElementKind.Button)]
    [InlineData(ChipElementKind.Text)]
    [InlineData(ChipElementKind.Other)]
    public void FindAnchor_worktree_name_on_a_non_checkbox_is_not_a_box(ChipElementKind kind)
    {
        Assert.Null(ChipRowLocator.FindAnchor(new[] { new ChipElement("worktree", kind, Box, false) }));
    }

    [Theory]
    [InlineData(ChipElementKind.Button)]
    [InlineData(ChipElementKind.Other)]
    public void FindAnchor_worktree_name_on_a_non_text_is_not_a_label(ChipElementKind kind)
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(new[]
        {
            CheckBox("worktree", Box),
            new ChipElement("worktree", kind, Label, false),
        });

        Assert.Equal(Box, anchor);
    }

    [Fact]
    public void FindAnchor_label_alone_is_null()
    {
        Assert.Null(ChipRowLocator.FindAnchor(new[] { Text("worktree", Label) }));
    }

    [Fact]
    public void FindAnchor_label_on_another_row_is_not_joined()
    {
        Rectangle below = new(1198, 1500, 94, 33);
        Rectangle above = new(1198, 1200, 94, 33);

        Assert.Equal(Box, ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text("worktree", below) }));
        Assert.Equal(Box, ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text("worktree", above) }));
    }

    [Fact]
    public void FindAnchor_label_centre_one_box_height_away_is_another_row()
    {
        // Box centre 1362.5, height 23: a label centred at 1385.5 (top 1369) is exactly 23 away.
        Rectangle atLimitBelow = new(1198, 1369, 94, 33);
        Rectangle atLimitAbove = new(1198, 1323, 94, 33);

        Assert.Equal(Box, ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text("worktree", atLimitBelow) }));
        Assert.Equal(Box, ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text("worktree", atLimitAbove) }));
    }

    [Fact]
    public void FindAnchor_label_centre_closer_than_the_box_height_is_the_same_row()
    {
        // 22 px away from the box centre, both directions.
        Rectangle justBelow = new(1198, 1368, 94, 33);
        Rectangle justAbove = new(1198, 1324, 94, 33);

        Assert.Equal(Rectangle.Union(Box, justBelow), ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text("worktree", justBelow) }));
        Assert.Equal(Rectangle.Union(Box, justAbove), ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text("worktree", justAbove) }));
    }

    [Fact]
    public void FindAnchor_label_left_of_the_box_is_not_joined()
    {
        Rectangle leftLabel = new(1060, 1345, 94, 33);

        Assert.Equal(Box, ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text("worktree", leftLabel) }));
    }

    [Fact]
    public void FindAnchor_offscreen_label_is_not_joined()
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(new[]
        {
            CheckBox("worktree", Box),
            new ChipElement("worktree", ChipElementKind.Text, Label, true),
        });

        Assert.Equal(Box, anchor);
    }

    [Fact]
    public void FindAnchor_label_with_empty_bounds_is_not_joined()
    {
        Assert.Equal(Box, ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text("worktree", Rectangle.Empty) }));
        Assert.Equal(Box, ChipRowLocator.FindAnchor(new[] { CheckBox("worktree", Box), Text("worktree", new Rectangle(1198, 1345, 0, 33)) }));
    }

    [Fact]
    public void FindAnchor_offscreen_box_is_ignored()
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(new[]
        {
            new ChipElement("worktree", ChipElementKind.CheckBox, Box, true),
            Text("worktree", Label),
        });

        Assert.Null(anchor);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1170, 1351, 0, 23)]
    [InlineData(1170, 1351, 23, 0)]
    public void FindAnchor_box_with_empty_bounds_is_ignored(int x, int y, int width, int height)
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(new[]
        {
            CheckBox("worktree", new Rectangle(x, y, width, height)),
            Text("worktree", Label),
        });

        Assert.Null(anchor);
    }

    [Fact]
    public void FindAnchor_other_elements_never_join_the_box()
    {
        List<ChipElement> row = RowWithoutAddFolder().Where(e => e.Kind != ChipElementKind.Text || e.Name != "worktree").ToList();

        Assert.Equal(Box, ChipRowLocator.FindAnchor(row));
    }

    [Fact]
    public void FindAnchor_does_not_change_the_input()
    {
        List<ChipElement> row = NewSessionRow();
        ChipElement[] before = row.ToArray();

        ChipRowLocator.FindAnchor(row);

        Assert.Equal(before, row);
    }

    // --- ButtonBounds ---

    [Fact]
    public void ButtonBounds_for_the_real_anchor()
    {
        Assert.Equal(new Rectangle(1376, 1340, 145, 45), ChipRowLocator.ButtonBounds(AddFolder, 100));
    }

    [Theory]
    [InlineData(0, 0, 10, 40, 50, 20, 0, 90, 40)]
    [InlineData(100, 200, 30, 20, 0, 135, 200, 20, 20)]
    [InlineData(-50, -10, 20, 8, 7, -28, -10, 15, 8)]
    [InlineData(1170, 1345, 122, 33, 80, 1300, 1345, 113, 33)]
    public void ButtonBounds_follows_the_gap_top_height_and_padding(
        int x, int y, int width, int height, int textWidth,
        int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        Rectangle anchor = new(x, y, width, height);

        Assert.Equal(new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight), ChipRowLocator.ButtonBounds(anchor, textWidth));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(2000)]
    public void ButtonBounds_matches_the_formula(int textWidth)
    {
        Rectangle bounds = ChipRowLocator.ButtonBounds(AddFolder, textWidth);

        Assert.Equal(AddFolder.Right + (AddFolder.Height / 4), bounds.X);
        Assert.Equal(AddFolder.Top, bounds.Y);
        Assert.Equal(textWidth + AddFolder.Height, bounds.Width);
        Assert.Equal(AddFolder.Height, bounds.Height);
    }

    [Fact]
    public void ButtonBounds_zero_text_width_is_the_padding_only()
    {
        Assert.Equal(new Rectangle(1376, 1340, 45, 45), ChipRowLocator.ButtonBounds(AddFolder, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void ButtonBounds_negative_text_width_throws_ArgumentOutOfRangeException(int textWidth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChipRowLocator.ButtonBounds(AddFolder, textWidth));
    }
}
