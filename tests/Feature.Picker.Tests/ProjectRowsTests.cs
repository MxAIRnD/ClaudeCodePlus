using System;
using System.Globalization;
using Feature.Catalog;
using Xunit;

namespace Feature.Picker.Tests;

/// <summary>Black-box behavior of <see cref="ProjectRows.Age"/> and <see cref="ProjectRows.SourcesLabel"/>.</summary>
public sealed class ProjectRowsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static string AgeOf(TimeSpan elapsed) => ProjectRows.Age(Now - elapsed, Now);

    private static string LocalDate(DateTimeOffset when) =>
        when.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // --- just now ---

    [Fact]
    public void Age_zero_is_just_now()
    {
        Assert.Equal("just now", ProjectRows.Age(Now, Now));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(59)]
    public void Age_under_one_minute_is_just_now(int seconds)
    {
        Assert.Equal("just now", AgeOf(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Age_one_tick_under_one_minute_is_just_now()
    {
        Assert.Equal("just now", AgeOf(TimeSpan.FromMinutes(1) - TimeSpan.FromTicks(1)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3600)]
    [InlineData(86400 * 400)]
    public void Age_of_a_future_time_is_just_now(int secondsAhead)
    {
        Assert.Equal("just now", ProjectRows.Age(Now.AddSeconds(secondsAhead), Now));
    }

    // --- N min ---

    [Fact]
    public void Age_of_exactly_sixty_seconds_is_1_min()
    {
        Assert.Equal("1 min", AgeOf(TimeSpan.FromSeconds(60)));
    }

    [Theory]
    [InlineData(119, "1 min")]
    [InlineData(120, "2 min")]
    [InlineData(45 * 60 + 30, "45 min")]
    [InlineData(59 * 60 + 59, "59 min")]
    public void Age_under_one_hour_is_minutes_rounded_down(int seconds, string expected)
    {
        Assert.Equal(expected, AgeOf(TimeSpan.FromSeconds(seconds)));
    }

    // --- N h ---

    [Theory]
    [InlineData(60, "1 h")]
    [InlineData(119, "1 h")]
    [InlineData(120, "2 h")]
    [InlineData(23 * 60 + 59, "23 h")]
    [InlineData(24 * 60, "24 h")]
    [InlineData(47 * 60, "47 h")]
    [InlineData(47 * 60 + 59, "47 h")]
    public void Age_under_two_days_is_hours_rounded_down(int minutes, string expected)
    {
        Assert.Equal(expected, AgeOf(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Age_one_tick_under_two_days_is_47_h()
    {
        Assert.Equal("47 h", AgeOf(TimeSpan.FromDays(2) - TimeSpan.FromTicks(1)));
    }

    // --- N d ---

    [Fact]
    public void Age_of_exactly_two_days_is_2_d()
    {
        Assert.Equal("2 d", AgeOf(TimeSpan.FromDays(2)));
    }

    [Theory]
    [InlineData(2, 23, "2 d")]
    [InlineData(3, 0, "3 d")]
    [InlineData(15, 12, "15 d")]
    [InlineData(29, 0, "29 d")]
    [InlineData(29, 23, "29 d")]
    public void Age_under_thirty_days_is_days_rounded_down(int days, int hours, string expected)
    {
        Assert.Equal(expected, AgeOf(TimeSpan.FromDays(days) + TimeSpan.FromHours(hours)));
    }

    [Fact]
    public void Age_one_tick_under_thirty_days_is_29_d()
    {
        Assert.Equal("29 d", AgeOf(TimeSpan.FromDays(30) - TimeSpan.FromTicks(1)));
    }

    // --- local date ---

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(365)]
    [InlineData(3650)]
    public void Age_of_thirty_days_or_more_is_the_local_date(int days)
    {
        DateTimeOffset when = Now - TimeSpan.FromDays(days);

        Assert.Equal(LocalDate(when), ProjectRows.Age(when, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(23 * 60 + 59)]
    public void Age_date_is_the_local_date_near_utc_midnight(int minutesAfterUtcMidnight)
    {
        // Near UTC midnight the UTC date and the local date differ in most time zones.
        DateTimeOffset when = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero).AddMinutes(minutesAfterUtcMidnight);

        Assert.Equal(LocalDate(when), ProjectRows.Age(when, Now));
    }

    [Fact]
    public void Age_date_depends_on_the_instant_not_on_the_offset_it_is_written_in()
    {
        DateTimeOffset utc = new(2026, 1, 15, 22, 30, 0, TimeSpan.Zero);
        DateTimeOffset east = utc.ToOffset(TimeSpan.FromHours(5));
        DateTimeOffset west = utc.ToOffset(TimeSpan.FromHours(-8));

        string expected = LocalDate(utc);
        Assert.Equal(expected, ProjectRows.Age(utc, Now));
        Assert.Equal(expected, ProjectRows.Age(east, Now));
        Assert.Equal(expected, ProjectRows.Age(west, Now));
    }

    [Fact]
    public void Age_date_has_the_yyyy_MM_dd_shape()
    {
        string label = ProjectRows.Age(Now - TimeSpan.FromDays(100), Now);

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", label);
    }

    [Fact]
    public void Age_measures_the_instant_difference_regardless_of_offsets()
    {
        // 90 seconds apart as instants, written in different offsets.
        DateTimeOffset when = new DateTimeOffset(2026, 10, 8, 11, 58, 30, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(3));
        DateTimeOffset now = Now.ToOffset(TimeSpan.FromHours(-7));

        Assert.Equal("1 min", ProjectRows.Age(when, now));
    }

    // --- SourcesLabel ---

    [Theory]
    [InlineData(ProjectSources.DesktopSessions, "D")]
    [InlineData(ProjectSources.TerminalProjects, "T")]
    [InlineData(ProjectSources.DesktopSessions | ProjectSources.TerminalProjects, "D+T")]
    [InlineData(ProjectSources.None, "")]
    public void SourcesLabel_names_the_sources(ProjectSources sources, string expected)
    {
        Assert.Equal(expected, ProjectRows.SourcesLabel(sources));
    }

    [Fact]
    public void SourcesLabel_for_none_is_empty_not_null()
    {
        Assert.NotNull(ProjectRows.SourcesLabel(ProjectSources.None));
    }
}
