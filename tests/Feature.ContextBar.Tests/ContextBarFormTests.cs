using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Feature.ContextBar.Tests;

/// <summary>
/// The window's states over the figures, through <see cref="ContextBarForm.ReadFigures"/>: no
/// session shown, a session shown without a figures file, a file without a reading yet, a file
/// with a reading. The figures folder is under
/// %TEMP%\ClaudeCodePlus.Tests\Feature.ContextBar.Tests\&lt;test&gt;-&lt;8 hex&gt;, never deleted
/// (agreed behavior on this machine).
/// </summary>
public sealed class ContextBarFormTests
{
    private const string Shown = "local_8804b5b6-7c1d-4e2f-9a3b-0c1d2e3f4a5b";
    private const string Other = "local_03b846e4-5f76-450e-8dd9-a4dbc37d0883";

    private const string Reading = """
        {"sessionId":"0d62bd69-e8f3-4a8b-8d39-b6d195acea5f","hostSessionId":"local_8804b5b6-7c1d-4e2f-9a3b-0c1d2e3f4a5b","cwd":"C:\\Work","model":"claude-x","tokens":366000,"window":1000000,"percent":37,"threshold":920000,"at":1792054399179,"isHidden":false}
        """;

    private const string NoReadingYet = """
        {"sessionId":"0d62bd69-e8f3-4a8b-8d39-b6d195acea5f","hostSessionId":"local_8804b5b6-7c1d-4e2f-9a3b-0c1d2e3f4a5b","cwd":"C:\\Work","model":"claude-x","tokens":null,"window":1000000,"percent":null,"threshold":null,"at":1792054399179,"isHidden":false}
        """;

    private static string Root([CallerMemberName] string testName = "")
    {
        string root = Path.Combine(Path.GetTempPath(), "ClaudeCodePlus.Tests", "Feature.ContextBar.Tests", testName + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void No_session_shown_or_nowhere_to_read_reads_nothing()
    {
        string root = Root();
        File.WriteAllText(Path.Combine(root, Shown + ".json"), Reading);

        Assert.Null(ContextBarForm.ReadFigures(root, null));
        Assert.Null(ContextBarForm.ReadFigures(null, Shown));
        Assert.Null(ContextBarForm.ReadFigures(null, null));
    }

    [Fact]
    public void A_session_without_a_figures_file_reads_nothing_and_never_another_sessions()
    {
        string root = Root();
        File.WriteAllText(Path.Combine(root, Other + ".json"), Reading);

        Assert.Null(ContextBarForm.ReadFigures(root, Shown));
        Assert.Null(ContextBarForm.ReadFigures(Path.Combine(root, "gone"), Shown));
    }

    [Fact]
    public void A_file_without_a_reading_yet_reads_as_the_dashes()
    {
        string root = Root();
        File.WriteAllText(Path.Combine(root, Shown + ".json"), NoReadingYet);

        ContextFigures? figures = ContextBarForm.ReadFigures(root, Shown);

        Assert.NotNull(figures);
        Assert.Null(figures!.Tokens);
        Assert.False(figures.IsHidden);
        Assert.Equal("\u2014", figures.PercentText);
        Assert.Equal("\u2014 / 1M", figures.CountsText);
    }

    [Fact]
    public void A_published_file_reads_as_its_figures()
    {
        string root = Root();
        File.WriteAllText(Path.Combine(root, Shown + ".json"), Reading);

        ContextFigures? figures = ContextBarForm.ReadFigures(root, Shown);

        Assert.NotNull(figures);
        Assert.Equal("0d62bd69-e8f3-4a8b-8d39-b6d195acea5f", figures!.SessionId);
        Assert.Equal(366000, figures.Tokens);
        Assert.Equal("37%", figures.PercentText);
    }
}
