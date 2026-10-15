using System;
using System.IO;
using Xunit;

namespace Feature.ContextBar.Tests;

/// <summary>Black-box behavior of <see cref="FiguresFile"/> and <see cref="ContextFigures"/>' derived texts.</summary>
public sealed class FiguresFileTests
{
    private const string Full = """
        {"sessionId":"abc","cwd":"C:\\Work","model":"claude-x","tokens":366000,"window":1000000,"percent":37,"threshold":920000,"at":1792054399179,"isHidden":false}
        """;

    [Fact]
    public void A_full_file_parses_to_its_figures()
    {
        ContextFigures? f = FiguresFile.Parse(Full);

        Assert.NotNull(f);
        Assert.Equal("abc", f!.SessionId);
        Assert.Equal(366000, f.Tokens);
        Assert.Equal(1000000, f.Window);
        Assert.Equal(920000, f.Threshold);
        Assert.Equal(1792054399179, f.AtMs);
        Assert.False(f.IsHidden);
        Assert.Equal(0.366, f.Fraction, 6);
        Assert.Equal(0.92, f.ThresholdFraction!.Value, 6);
        Assert.Equal("37%", f.PercentText);
        Assert.Equal("366K / 1M", f.CountsText);
    }

    [Fact]
    public void Nulls_and_missing_optionals_read_as_no_reading()
    {
        ContextFigures? f = FiguresFile.Parse("""{"sessionId":"abc","window":200000,"tokens":null,"threshold":null,"isHidden":true}""");

        Assert.NotNull(f);
        Assert.Null(f!.Tokens);
        Assert.Null(f.Threshold);
        Assert.Equal(0, f.AtMs);
        Assert.True(f.IsHidden);
        Assert.Equal(0, f.Fraction);
        Assert.Null(f.ThresholdFraction);
        Assert.Equal("\u2014", f.PercentText);
        Assert.Equal("\u2014 / 200K", f.CountsText);
    }

    [Theory]
    [InlineData("""{"window":200000}""")]
    [InlineData("""{"sessionId":"","window":200000}""")]
    [InlineData("""{"sessionId":"abc"}""")]
    [InlineData("""{"sessionId":"abc","window":0}""")]
    [InlineData("""{"sessionId":"abc","window":"big"}""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("""{"sessionId":"abc","window":200000""")]
    [InlineData("")]
    public void Anything_else_is_null(string json)
    {
        Assert.Null(FiguresFile.Parse(json));
    }

    [Fact]
    public void A_threshold_outside_the_window_has_no_fraction_and_the_fill_is_clamped()
    {
        ContextFigures? over = FiguresFile.Parse("""{"sessionId":"abc","window":1000,"tokens":1500,"threshold":1000}""");

        Assert.Equal(1, over!.Fraction);
        Assert.Null(over.ThresholdFraction);
        Assert.Equal("100%", over.PercentText);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(840, "840")]
    [InlineData(8400, "8.4K")]
    [InlineData(8000, "8K")]
    [InlineData(84200, "84K")]
    [InlineData(84500, "85K")]
    [InlineData(200000, "200K")]
    [InlineData(1000000, "1M")]
    [InlineData(1250000, "1.25M")]
    [InlineData(-5, "0")]
    public void Token_counts_read_as_plain_K_and_M(long n, string expected)
    {
        Assert.Equal(expected, FiguresFile.FormatTokens(n));
    }

    [Fact]
    public void A_missing_file_reads_as_null_and_an_existing_one_as_its_figures()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ClaudeCodePlus.Tests", "Feature.ContextBar.Tests", "figures-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir); // never deleted (agreed behavior on this machine)
        string file = Path.Combine(dir, "abc.json");

        Assert.Null(FiguresFile.TryRead(file));
        File.WriteAllText(file, Full);
        Assert.Equal("abc", FiguresFile.TryRead(file)!.SessionId);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => FiguresFile.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => FiguresFile.TryRead(null!));
    }
}
