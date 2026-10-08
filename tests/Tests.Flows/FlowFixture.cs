using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Feature.Catalog;
using Feature.DeepLink;
using Feature.Picker;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Flows;

/// <summary>
/// A synthetic Claude data tree for one test, under
/// %TEMP%\ClaudeCodePlus.Tests\Tests.Flows\&lt;test&gt;-&lt;8 hex&gt;: a desktop session store
/// (<c>store\&lt;account&gt;\&lt;org&gt;\local_*.json</c>, made-up records) and the project
/// folders those records name (<c>projects\…</c>). Never deleted by the tests (no-delete rule).
/// </summary>
internal sealed class FlowFixture
{
    /// <summary>An arbitrary base time (2026-10-08 UTC) in Unix milliseconds.</summary>
    public const long BaseMs = 1791446823792;

    private FlowFixture(string root)
    {
        Root = root;
        StoreRoot = Path.Combine(root, "store");
        ProjectsRoot = Path.Combine(root, "projects");
        Directory.CreateDirectory(StoreRoot);
        Directory.CreateDirectory(ProjectsRoot);
    }

    public string Root { get; }

    public string StoreRoot { get; }

    public string ProjectsRoot { get; }

    public static FlowFixture Create([CallerMemberName] string testName = "")
    {
        string hex = Guid.NewGuid().ToString("N")[..8];
        string root = Path.Combine(Path.GetTempPath(), "ClaudeCodePlus.Tests", "Tests.Flows", $"{testName}-{hex}");
        return new FlowFixture(root);
    }

    /// <summary>Creates a project folder inside the fixture and returns its absolute path.</summary>
    public string Folder(string relative)
    {
        string path = Path.Combine(ProjectsRoot, relative);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>A path inside the fixture that is never created (a "missing" folder).</summary>
    public string MissingFolder(string relative) => Path.Combine(ProjectsRoot, relative);

    /// <summary>
    /// Writes one made-up desktop session record naming <paramref name="originCwd"/>, active
    /// at <paramref name="lastActivityAt"/> (Unix ms).
    /// </summary>
    public string Record(string originCwd, long lastActivityAt, string account = "acct-1", string org = "org-1")
    {
        string dir = Path.Combine(StoreRoot, account, org);
        Directory.CreateDirectory(dir);
        string id = "local_" + Guid.NewGuid().ToString();
        var record = new
        {
            sessionId = id,
            originCwd,
            lastActivityAt,
            createdAt = lastActivityAt - 6_000_000,
            title = "made up",
        };
        string file = Path.Combine(dir, id + ".json");
        File.WriteAllText(file, JsonSerializer.Serialize(record));
        return file;
    }

    public CatalogOptions Options() => new()
    {
        DesktopSessionsRoot = StoreRoot,
        TerminalProjectsRoot = null,
    };

    /// <summary>The composition the real root uses: catalog, opener, picker in one collection.</summary>
    public static IServiceCollection Compose(CatalogOptions options) =>
        new ServiceCollection().AddCatalog(options).AddDeepLink().AddPicker();

    public ServiceProvider Provider() => Compose(Options()).BuildServiceProvider();
}
