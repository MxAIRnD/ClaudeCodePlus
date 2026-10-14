using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Xunit;

namespace Feature.ChipButton.Tests;

/// <summary>
/// Black-box behavior of <see cref="ChipRowLocator.FindAnchor"/>,
/// <see cref="ChipRowLocator.ButtonBounds"/> and <see cref="ChipRowLocator.Collides"/> over
/// in-memory element snapshots.
/// </summary>
public sealed class ChipRowLocatorTests
{
    // The new-session chip row as UI Automation reported it (physical px), names only, no
    // session content. The ComboBox (branch) maps to Other.
    private static readonly Rectangle AddFolder = new(1313, 1340, 52, 45);
    private static readonly Rectangle Box = new(1170, 1351, 23, 23);
    private static readonly Rectangle Label = new(1198, 1345, 94, 33);

    // The newer layout's composer bottom row (app build 2.37337.1), same capture rules. The
    // environment and permissions chips joined are the anchor; the button goes right after.
    private static readonly Rectangle EnvironmentChip = new(1012, 1493, 75, 40);
    private static readonly Rectangle PermissionsChip = new(1092, 1493, 219, 40);
    private static readonly Rectangle EnvironmentRow = new(1012, 1493, 299, 40);
    private static readonly Rectangle NewButton = new(1321, 1493, 140, 40);

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

    // The new-session screen of the newer layout: the composer's attach button on the row above,
    // the start card's buttons, and the composer's bottom row.
    private static List<ChipElement> NewSessionComposer(Rectangle model) => new()
    {
        Button("Add", new Rectangle(1009, 1417, 46, 47)),
        Button("SampleProject", new Rectangle(1368, 605, 511, 54)),
        Button("Choose environment", new Rectangle(1368, 727, 511, 54)),
        Button("Environment: AI", EnvironmentChip),
        Button("Permissions: Bypass permissions", PermissionsChip),
        Button("Model: Fable 5.1", model),
        Button("Effort: Extra", new Rectangle(2114, 1493, 74, 40)),
        Button("Usage: Weekly", new Rectangle(2196, 1493, 40, 40)),
    };

    private static List<ChipElement> NewSessionComposer() => NewSessionComposer(new Rectangle(2005, 1493, 103, 40));

    private static List<ChipElement> WithEnvironmentChip(params ChipElement[] others)
    {
        List<ChipElement> elements = new() { Button("Environment: AI", EnvironmentChip) };
        elements.AddRange(others);
        return elements;
    }

    // --- contract constants ---

    [Fact]
    public void Names_are_the_apps_accessible_names()
    {
        Assert.Equal("Add another folder", ChipRowLocator.AddFolderName);
        Assert.Equal("worktree", ChipRowLocator.WorktreeName);
    }

    [Fact]
    public void Environment_chip_prefix_is_the_start_of_the_apps_accessible_name()
    {
        Assert.Equal("Environment: ", ChipRowLocator.EnvironmentChipPrefix);
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

    // --- FindAnchor: environment chip ---

    [Fact]
    public void FindAnchor_new_session_composer_is_the_environment_and_permissions_chips()
    {
        Assert.Equal(new Rectangle(1012, 1493, 299, 40), ChipRowLocator.FindAnchor(NewSessionComposer()));
    }

    [Fact]
    public void FindAnchor_new_session_composer_does_not_depend_on_order()
    {
        List<ChipElement> elements = NewSessionComposer();
        elements.Reverse();

        Assert.Equal(EnvironmentRow, ChipRowLocator.FindAnchor(elements));
    }

    [Fact]
    public void FindAnchor_running_session_with_the_environment_panel_is_null()
    {
        // A running session names its side-panel heading "Environment" (no colon) and has no
        // environment chip in the composer.
        List<ChipElement> elements = new()
        {
            Button("Environment", new Rectangle(1622, 185, 430, 44)),
            Button("Model: Opus 5.5", new Rectangle(1304, 1493, 108, 40)),
            Button("Effort: Extra", new Rectangle(1418, 1493, 74, 40)),
            Button("Usage: Weekly", new Rectangle(1500, 1493, 40, 40)),
        };

        Assert.Null(ChipRowLocator.FindAnchor(elements));
    }

    [Fact]
    public void FindAnchor_environment_chip_alone_is_its_bounds()
    {
        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(WithEnvironmentChip()));
    }

    [Theory]
    [InlineData("environment: ai")]
    [InlineData("ENVIRONMENT: AI")]
    [InlineData("eNvIrOnMeNt: AI")]
    public void FindAnchor_environment_prefix_ignores_case(string name)
    {
        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(new[] { Button(name, EnvironmentChip) }));
    }

    [Fact]
    public void FindAnchor_environment_prefix_with_an_empty_environment_name_matches()
    {
        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(new[] { Button("Environment: ", EnvironmentChip) }));
    }

    [Theory]
    [InlineData("Environment:AI")]
    [InlineData("Environment:")]
    [InlineData("Environment")]
    [InlineData("Environment AI")]
    [InlineData(" Environment: AI")]
    [InlineData("Env: AI")]
    [InlineData("Choose environment")]
    [InlineData("")]
    public void FindAnchor_environment_name_must_start_with_the_full_prefix(string name)
    {
        Assert.Null(ChipRowLocator.FindAnchor(new[] { Button(name, EnvironmentChip) }));
    }

    [Theory]
    [InlineData(ChipElementKind.Text)]
    [InlineData(ChipElementKind.CheckBox)]
    [InlineData(ChipElementKind.Other)]
    public void FindAnchor_environment_name_on_a_non_button_is_ignored(ChipElementKind kind)
    {
        Assert.Null(ChipRowLocator.FindAnchor(new[] { new ChipElement("Environment: AI", kind, EnvironmentChip, false) }));
    }

    [Fact]
    public void FindAnchor_offscreen_environment_chip_is_ignored()
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(new[]
        {
            new ChipElement("Environment: AI", ChipElementKind.Button, EnvironmentChip, true),
            Button("Permissions: Bypass permissions", PermissionsChip),
        });

        Assert.Null(anchor);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1012, 1493, 0, 40)]
    [InlineData(1012, 1493, 75, 0)]
    public void FindAnchor_environment_chip_with_empty_bounds_is_ignored(int x, int y, int width, int height)
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(new[]
        {
            Button("Environment: AI", new Rectangle(x, y, width, height)),
            Button("Permissions: Bypass permissions", PermissionsChip),
        });

        Assert.Null(anchor);
    }

    [Fact]
    public void FindAnchor_several_environment_chips_gives_the_lowest_in_any_order()
    {
        Rectangle upper = new(1012, 400, 75, 40);
        Rectangle middle = new(1012, 900, 75, 40);
        ChipElement[] chips =
        {
            Button("Environment: A", upper),
            Button("Environment: B", EnvironmentChip),
            Button("Environment: C", middle),
        };

        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(chips));
        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(new[] { chips[2], chips[1], chips[0] }));
        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(new[] { chips[1], chips[0], chips[2] }));
    }

    [Fact]
    public void FindAnchor_offscreen_lower_environment_chip_does_not_win()
    {
        Rectangle hiddenLower = new(1012, 1900, 75, 40);

        Rectangle? anchor = ChipRowLocator.FindAnchor(new[]
        {
            Button("Environment: AI", EnvironmentChip),
            new ChipElement("Environment: AI", ChipElementKind.Button, hiddenLower, true),
        });

        Assert.Equal(EnvironmentChip, anchor);
    }

    [Fact]
    public void FindAnchor_does_not_change_the_input_in_the_newer_layout()
    {
        List<ChipElement> elements = NewSessionComposer();
        ChipElement[] before = elements.ToArray();

        ChipRowLocator.FindAnchor(elements);

        Assert.Equal(before, elements);
    }

    // --- FindAnchor: environment row join ---

    [Fact]
    public void FindAnchor_neighbour_one_chip_height_after_the_row_is_joined()
    {
        // Chip ends at 1087, height 40: a neighbour starting at 1127 is exactly 40 later.
        Rectangle neighbour = new(1127, 1493, 50, 40);

        Assert.Equal(new Rectangle(1012, 1493, 165, 40), ChipRowLocator.FindAnchor(WithEnvironmentChip(Button("Next", neighbour))));
    }

    [Fact]
    public void FindAnchor_neighbour_one_pixel_further_is_not_joined()
    {
        Rectangle neighbour = new(1128, 1493, 50, 40);

        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(WithEnvironmentChip(Button("Next", neighbour))));
    }

    [Fact]
    public void FindAnchor_neighbour_touching_the_row_is_joined()
    {
        Rectangle neighbour = new(1087, 1493, 50, 40);

        Assert.Equal(new Rectangle(1012, 1493, 125, 40), ChipRowLocator.FindAnchor(WithEnvironmentChip(Button("Next", neighbour))));
    }

    [Fact]
    public void FindAnchor_chain_of_chips_each_within_the_limit_joins_all()
    {
        // Each starts 40 after the previous one ends; the last is 130 after the chip itself.
        Rectangle second = new(1127, 1493, 50, 40);
        Rectangle third = new(1217, 1493, 50, 40);

        Rectangle? anchor = ChipRowLocator.FindAnchor(WithEnvironmentChip(Button("Second", second), Button("Third", third)));

        Assert.Equal(new Rectangle(1012, 1493, 255, 40), anchor);
    }

    [Fact]
    public void FindAnchor_environment_join_does_not_depend_on_order()
    {
        ChipElement chip = Button("Environment: AI", EnvironmentChip);
        ChipElement second = Button("Second", new Rectangle(1127, 1493, 50, 40));
        ChipElement third = Button("Third", new Rectangle(1217, 1493, 50, 40));
        Rectangle expected = new(1012, 1493, 255, 40);

        Assert.Equal(expected, ChipRowLocator.FindAnchor(new[] { third, second, chip }));
        Assert.Equal(expected, ChipRowLocator.FindAnchor(new[] { second, chip, third }));
        Assert.Equal(expected, ChipRowLocator.FindAnchor(new[] { third, chip, second }));
    }

    [Theory]
    [InlineData(1533)]
    [InlineData(1453)]
    [InlineData(1600)]
    [InlineData(1400)]
    public void FindAnchor_neighbour_on_another_row_is_not_joined(int top)
    {
        // Chip centre 1513, height 40: a neighbour centred at 1553 (top 1533) is exactly 40 away.
        Rectangle neighbour = new(1092, top, 219, 40);

        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(WithEnvironmentChip(Button("Next", neighbour))));
    }

    [Theory]
    [InlineData(1532)]
    [InlineData(1454)]
    public void FindAnchor_neighbour_centre_closer_than_the_chip_height_is_the_same_row(int top)
    {
        // 39 px away from the chip centre, both directions.
        Rectangle neighbour = new(1092, top, 219, 40);

        Assert.Equal(Rectangle.Union(EnvironmentChip, neighbour), ChipRowLocator.FindAnchor(WithEnvironmentChip(Button("Next", neighbour))));
    }

    [Fact]
    public void FindAnchor_button_left_of_the_chip_is_not_joined()
    {
        Rectangle leftButton = new(950, 1493, 50, 40);

        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(WithEnvironmentChip(Button("Before", leftButton))));
    }

    [Theory]
    [InlineData(1080)]
    [InlineData(1012)]
    [InlineData(1050)]
    public void FindAnchor_button_starting_inside_the_chip_is_not_joined(int left)
    {
        Rectangle overlapping = new(left, 1493, 100, 40);

        Assert.Equal(EnvironmentChip, ChipRowLocator.FindAnchor(WithEnvironmentChip(Button("Overlapping", overlapping))));
    }

    [Fact]
    public void FindAnchor_button_starting_inside_the_joined_row_is_not_joined()
    {
        // Starts inside the permissions chip (1092..1311) once that one is joined.
        Rectangle overlapping = new(1300, 1493, 50, 40);

        Rectangle? anchor = ChipRowLocator.FindAnchor(WithEnvironmentChip(
            Button("Permissions: Bypass permissions", PermissionsChip),
            Button("Overlapping", overlapping)));

        Assert.Equal(EnvironmentRow, anchor);
    }

    [Fact]
    public void FindAnchor_offscreen_neighbour_is_not_joined()
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(WithEnvironmentChip(
            new ChipElement("Permissions: Bypass permissions", ChipElementKind.Button, PermissionsChip, true)));

        Assert.Equal(EnvironmentChip, anchor);
    }

    [Theory]
    [InlineData(1092, 1493, 0, 40)]
    [InlineData(1092, 1493, 219, 0)]
    public void FindAnchor_neighbour_with_empty_bounds_is_not_joined(int x, int y, int width, int height)
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(WithEnvironmentChip(
            Button("Permissions: Bypass permissions", new Rectangle(x, y, width, height))));

        Assert.Equal(EnvironmentChip, anchor);
    }

    [Theory]
    [InlineData(ChipElementKind.Text)]
    [InlineData(ChipElementKind.CheckBox)]
    [InlineData(ChipElementKind.Other)]
    public void FindAnchor_non_button_neighbour_is_not_joined(ChipElementKind kind)
    {
        Rectangle? anchor = ChipRowLocator.FindAnchor(WithEnvironmentChip(
            new ChipElement("Permissions: Bypass permissions", kind, PermissionsChip, false)));

        Assert.Equal(EnvironmentChip, anchor);
    }

    // --- FindAnchor: precedence over the environment chip ---

    [Fact]
    public void FindAnchor_add_folder_wins_over_the_environment_row()
    {
        List<ChipElement> elements = NewSessionComposer();
        elements.Add(Button("Add another folder", AddFolder));

        Assert.Equal(AddFolder, ChipRowLocator.FindAnchor(elements));
    }

    [Fact]
    public void FindAnchor_worktree_box_wins_over_the_environment_row_in_any_order()
    {
        List<ChipElement> elements = NewSessionComposer();
        elements.Add(CheckBox("worktree", Box));
        elements.Add(Text("worktree", Label));

        Assert.Equal(Rectangle.Union(Box, Label), ChipRowLocator.FindAnchor(elements));
        elements.Reverse();
        Assert.Equal(Rectangle.Union(Box, Label), ChipRowLocator.FindAnchor(elements));
    }

    [Fact]
    public void FindAnchor_undrawn_add_folder_falls_back_to_the_environment_row()
    {
        List<ChipElement> elements = NewSessionComposer();
        elements.Add(new ChipElement("Add another folder", ChipElementKind.Button, AddFolder, true));
        elements.Add(Button("Add another folder", Rectangle.Empty));

        Assert.Equal(EnvironmentRow, ChipRowLocator.FindAnchor(elements));
    }

    [Fact]
    public void FindAnchor_undrawn_worktree_box_falls_back_to_the_environment_row()
    {
        List<ChipElement> elements = NewSessionComposer();
        elements.Add(new ChipElement("worktree", ChipElementKind.CheckBox, Box, true));
        elements.Add(CheckBox("worktree", Rectangle.Empty));
        elements.Add(Text("worktree", Label));

        Assert.Equal(EnvironmentRow, ChipRowLocator.FindAnchor(elements));
    }

    // --- ButtonBounds ---

    [Fact]
    public void ButtonBounds_for_the_real_anchor()
    {
        Assert.Equal(new Rectangle(1376, 1340, 145, 45), ChipRowLocator.ButtonBounds(AddFolder, 100));
    }

    [Fact]
    public void ButtonBounds_for_the_environment_row()
    {
        Assert.Equal(new Rectangle(1321, 1493, 140, 40), ChipRowLocator.ButtonBounds(EnvironmentRow, 100));
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

    // --- Collides ---

    [Fact]
    public void Collides_null_throws_ArgumentNullException()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ChipRowLocator.Collides(NewButton, EnvironmentRow, null!));

        Assert.Equal("elements", ex.ParamName);
    }

    [Fact]
    public void Collides_empty_list_is_false()
    {
        Assert.False(ChipRowLocator.Collides(NewButton, EnvironmentRow, Array.Empty<ChipElement>()));
    }

    [Fact]
    public void Collides_new_session_composer_is_false()
    {
        Assert.False(ChipRowLocator.Collides(NewButton, EnvironmentRow, NewSessionComposer()));
    }

    [Fact]
    public void Collides_placement_found_in_the_new_session_composer_is_free()
    {
        List<ChipElement> elements = NewSessionComposer();
        Rectangle anchor = ChipRowLocator.FindAnchor(elements)!.Value;

        Assert.False(ChipRowLocator.Collides(ChipRowLocator.ButtonBounds(anchor, 100), anchor, elements));
    }

    [Fact]
    public void Collides_narrow_window_with_the_model_chip_under_the_button_is_true()
    {
        List<ChipElement> elements = NewSessionComposer(new Rectangle(1400, 1493, 103, 40));

        Assert.True(ChipRowLocator.Collides(NewButton, EnvironmentRow, elements));
    }

    [Fact]
    public void Collides_button_touching_the_right_edge_is_false()
    {
        // NewButton ends at 1461.
        Rectangle touching = new(1461, 1493, 50, 40);

        Assert.False(ChipRowLocator.Collides(NewButton, EnvironmentRow, new[] { Button("Model: Fable 5.1", touching) }));
    }

    [Fact]
    public void Collides_button_one_pixel_inside_the_right_edge_is_true()
    {
        Rectangle overlapping = new(1460, 1493, 50, 40);

        Assert.True(ChipRowLocator.Collides(NewButton, EnvironmentRow, new[] { Button("Model: Fable 5.1", overlapping) }));
    }

    [Fact]
    public void Collides_container_overlapping_the_anchor_too_is_false()
    {
        // A button around the whole bottom row covers the anchor as well.
        Rectangle container = new(996, 1486, 1256, 44);

        Assert.False(ChipRowLocator.Collides(NewButton, EnvironmentRow, new[] { Button("Composer", container) }));
    }

    [Fact]
    public void Collides_anchor_chips_are_never_in_the_way()
    {
        // Bounds that reach back over the permissions chip.
        Rectangle overPermissions = new(1300, 1493, 140, 40);
        ChipElement[] elements =
        {
            Button("Environment: AI", EnvironmentChip),
            Button("Permissions: Bypass permissions", PermissionsChip),
        };

        Assert.False(ChipRowLocator.Collides(overPermissions, EnvironmentRow, elements));
    }

    [Fact]
    public void Collides_offscreen_button_is_false()
    {
        ChipElement hidden = new("Model: Fable 5.1", ChipElementKind.Button, new Rectangle(1400, 1493, 103, 40), true);

        Assert.False(ChipRowLocator.Collides(NewButton, EnvironmentRow, new[] { hidden }));
    }

    [Theory]
    [InlineData(1330, 1493, 0, 40)]
    [InlineData(1330, 1500, 50, 0)]
    public void Collides_button_with_empty_bounds_is_false(int x, int y, int width, int height)
    {
        Rectangle empty = new(x, y, width, height);

        Assert.False(ChipRowLocator.Collides(NewButton, EnvironmentRow, new[] { Button("Model: Fable 5.1", empty) }));
    }

    [Theory]
    [InlineData(ChipElementKind.Text)]
    [InlineData(ChipElementKind.CheckBox)]
    [InlineData(ChipElementKind.Other)]
    public void Collides_non_button_is_false(ChipElementKind kind)
    {
        ChipElement element = new("Model: Fable 5.1", kind, new Rectangle(1400, 1493, 103, 40), false);

        Assert.False(ChipRowLocator.Collides(NewButton, EnvironmentRow, new[] { element }));
    }
}
