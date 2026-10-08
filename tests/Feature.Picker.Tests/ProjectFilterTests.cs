using System;
using System.Collections.Generic;
using System.Linq;
using Feature.Catalog;
using Xunit;

namespace Feature.Picker.Tests;

/// <summary>Black-box behavior of <see cref="ProjectFilter.Apply"/> over made-up rows.</summary>
public sealed class ProjectFilterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static ProjectEntry Row(string path, int hoursAgo)
    {
        string name = path.Substring(path.LastIndexOf('\\') + 1);
        return new ProjectEntry(path, name, Now.AddHours(-hoursAgo), ProjectSources.DesktopSessions, 1, true);
    }

    // Deliberately not in time order: the filter must keep the input order, whatever it is.
    private static List<ProjectEntry> Rows() => new()
    {
        Row(@"C:\Work\Alpha", 5),
        Row(@"D:\Clients\Contoso\web", 1),
        Row(@"C:\Work\beta-tools", 9),
        Row(@"C:\Projects\ContosoSite", 2),
        Row(@"E:\Sandbox\gamma", 3),
    };

    private static string[] Paths(IEnumerable<ProjectEntry> rows) => rows.Select(r => r.Path).ToArray();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \t \t ")]
    public void Blank_query_returns_the_same_list_instance(string? query)
    {
        List<ProjectEntry> rows = Rows();

        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(rows, query);

        Assert.Same(rows, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public void Blank_query_keeps_every_row_in_the_original_order(string? query)
    {
        List<ProjectEntry> rows = Rows();
        string[] before = Paths(rows);

        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(rows, query);

        Assert.Equal(before, Paths(result));
    }

    [Fact]
    public void Blank_query_over_an_empty_list_returns_that_empty_list()
    {
        var rows = new List<ProjectEntry>();

        Assert.Same(rows, ProjectFilter.Apply(rows, ""));
    }

    [Fact]
    public void Word_query_over_an_empty_list_gives_an_empty_list()
    {
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(new List<ProjectEntry>(), "alpha");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Single_word_matches_the_name()
    {
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), "gamma");

        Assert.Equal(new[] { @"E:\Sandbox\gamma" }, Paths(result));
    }

    [Fact]
    public void Single_word_matches_the_path_when_the_name_does_not_contain_it()
    {
        // "Clients" occurs only in the path of ...\Contoso\web, never in a name.
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), "Clients");

        Assert.Equal(new[] { @"D:\Clients\Contoso\web" }, Paths(result));
    }

    [Fact]
    public void Single_word_matches_a_substring_inside_the_name()
    {
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), "eta-to");

        Assert.Equal(new[] { @"C:\Work\beta-tools" }, Paths(result));
    }

    [Theory]
    [InlineData("ALPHA")]
    [InlineData("alpha")]
    [InlineData("aLpHa")]
    public void Matching_the_name_ignores_case(string query)
    {
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), query);

        Assert.Equal(new[] { @"C:\Work\Alpha" }, Paths(result));
    }

    [Theory]
    [InlineData("clients")]
    [InlineData("CLIENTS")]
    [InlineData(@"d:\clients")]
    public void Matching_the_path_ignores_case(string query)
    {
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), query);

        Assert.Equal(new[] { @"D:\Clients\Contoso\web" }, Paths(result));
    }

    [Fact]
    public void Word_found_in_several_rows_keeps_them_in_the_original_order()
    {
        // "contoso" is in the path of row 2 and the name of row 4; "work" in rows 1 and 3.
        Assert.Equal(new[] { @"D:\Clients\Contoso\web", @"C:\Projects\ContosoSite" },
            Paths(ProjectFilter.Apply(Rows(), "contoso")));
        Assert.Equal(new[] { @"C:\Work\Alpha", @"C:\Work\beta-tools" },
            Paths(ProjectFilter.Apply(Rows(), "work")));
    }

    [Fact]
    public void Several_words_are_ANDed()
    {
        // "contoso" alone keeps two rows; adding "web" keeps only the row that has both.
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), "contoso web");

        Assert.Equal(new[] { @"D:\Clients\Contoso\web" }, Paths(result));
    }

    [Fact]
    public void Several_words_may_match_one_in_the_name_and_another_only_in_the_path()
    {
        // "site" is only in the name ContosoSite, "projects" only in its parent folder.
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), "site projects");

        Assert.Equal(new[] { @"C:\Projects\ContosoSite" }, Paths(result));
    }

    [Fact]
    public void Word_order_does_not_matter()
    {
        Assert.Equal(Paths(ProjectFilter.Apply(Rows(), "contoso web")),
            Paths(ProjectFilter.Apply(Rows(), "web contoso")));
    }

    [Theory]
    [InlineData("contoso\tweb")]
    [InlineData("contoso \t web")]
    [InlineData("  contoso   web  ")]
    [InlineData("\tcontoso\t\tweb\t")]
    public void Words_may_be_split_by_spaces_or_tabs_in_any_run(string query)
    {
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), query);

        Assert.Equal(new[] { @"D:\Clients\Contoso\web" }, Paths(result));
    }

    [Fact]
    public void Surrounding_spaces_do_not_change_a_single_word_match()
    {
        Assert.Equal(Paths(ProjectFilter.Apply(Rows(), "gamma")),
            Paths(ProjectFilter.Apply(Rows(), "   gamma \t")));
    }

    [Fact]
    public void Word_that_occurs_in_no_row_gives_an_empty_list()
    {
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), "zzz-nowhere");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void One_unmatched_word_among_matched_ones_gives_an_empty_list()
    {
        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(Rows(), "contoso web zzz-nowhere");

        Assert.Empty(result);
    }

    [Fact]
    public void A_word_spanning_a_path_separator_matches_as_a_path_substring()
    {
        Assert.Equal(new[] { @"C:\Work\Alpha" }, Paths(ProjectFilter.Apply(Rows(), @"Work\Alpha")));
    }

    [Fact]
    public void Filtered_result_is_not_the_input_instance()
    {
        List<ProjectEntry> rows = Rows();

        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(rows, "work");

        Assert.NotSame(rows, result);
    }

    [Fact]
    public void Result_contains_the_input_row_instances()
    {
        List<ProjectEntry> rows = Rows();

        IReadOnlyList<ProjectEntry> result = ProjectFilter.Apply(rows, "gamma");

        Assert.Same(rows[4], Assert.Single(result));
    }

    [Fact]
    public void Null_entries_throws_ArgumentNullException()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ProjectFilter.Apply(null!, "alpha"));

        Assert.Equal("entries", ex.ParamName);
    }

    [Fact]
    public void Null_entries_throws_even_for_a_blank_query()
    {
        Assert.Throws<ArgumentNullException>(() => ProjectFilter.Apply(null!, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("contoso")]
    [InlineData("contoso web")]
    [InlineData("zzz-nowhere")]
    public void Input_list_and_rows_are_not_changed(string? query)
    {
        List<ProjectEntry> rows = Rows();
        ProjectEntry[] instancesBefore = rows.ToArray();
        ProjectEntry[] valuesBefore = Rows().ToArray();

        _ = ProjectFilter.Apply(rows, query);

        Assert.Equal(instancesBefore.Length, rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            Assert.Same(instancesBefore[i], rows[i]);
            Assert.Equal(valuesBefore[i], rows[i]);
        }
    }
}
