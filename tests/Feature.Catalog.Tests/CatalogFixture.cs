using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Feature.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace Feature.Catalog.Tests;

/// <summary>
/// One synthetic data tree per test under %TEMP%\ClaudeCodePlus.Tests\Feature.Catalog.Tests\
/// &lt;test&gt;-&lt;8 hex&gt;: a session store with made-up local_*.json records, a Claude Code projects
/// folder with made-up &lt;guid&gt;.jsonl transcripts, and project folders with made-up names.
/// Never deleted (agreed behavior on this machine).
/// </summary>
internal sealed class CatalogFixture
{
    private CatalogFixture(string root)
    {
        Root = root;
        SessionsRoot = Path.Combine(root, "sessions");
        Directory.CreateDirectory(SessionsRoot);
    }

    /// <summary>The fixture tree of this test.</summary>
    public string Root { get; }

    /// <summary>The synthetic desktop session store root.</summary>
    public string SessionsRoot { get; }

    /// <summary>The synthetic Claude Code projects folder root; created by the first transcript written.</summary>
    public string ProjectsRoot => Path.Combine(Root, "projects");

    public static CatalogFixture Create([CallerMemberName] string testName = "")
    {
        string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        string root = Path.Combine(Path.GetTempPath(), "ClaudeCodePlus.Tests", "Feature.Catalog.Tests", testName + "-" + suffix);
        return new CatalogFixture(root);
    }

    /// <summary>Creates a folder inside the fixture tree and returns its absolute path.</summary>
    public string ExistingFolder(params string[] segments)
    {
        string path = Path.Combine(Root, "work", Path.Combine(segments));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>A made-up absolute path inside the fixture tree that is never created.</summary>
    public string MissingFolder(params string[] segments) =>
        Path.Combine(Root, "gone", Path.Combine(segments));

    /// <summary>Writes a session record (any JSON value) as &lt;root&gt;\&lt;account&gt;\&lt;org&gt;\&lt;fileName&gt;.</summary>
    public string WriteJson(object? value, string account = "acct-0001", string org = "org-0001", string? fileName = null) =>
        WriteRaw(JsonSerializer.Serialize(value), account, org, fileName);

    /// <summary>Writes raw text as a record file; the default name is local_&lt;guid&gt;.json.</summary>
    public string WriteRaw(string text, string account = "acct-0001", string org = "org-0001", string? fileName = null)
    {
        string dir = Path.Combine(SessionsRoot, account, org);
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, fileName ?? "local_" + Guid.NewGuid().ToString("D") + ".json");
        File.WriteAllText(file, text);
        return file;
    }

    /// <summary>Writes a typical record naming <paramref name="originCwd"/>.</summary>
    public string WriteRecord(string? originCwd, long? lastActivityAt, long? createdAt = null, string account = "acct-0001", string org = "org-0001")
    {
        var record = new Dictionary<string, object?>
        {
            ["sessionId"] = "local_" + Guid.NewGuid().ToString("N").Substring(0, 6),
            ["title"] = "made up title",
        };
        if (originCwd is not null) record["originCwd"] = originCwd;
        if (lastActivityAt is not null) record["lastActivityAt"] = lastActivityAt.Value;
        if (createdAt is not null) record["createdAt"] = createdAt.Value;
        return WriteJson(record, account, org);
    }

    /// <summary>One JSON Lines record of a transcript; a null <paramref name="cwd"/> leaves the field out.</summary>
    public static string TranscriptLine(string? cwd, string type = "user")
    {
        var line = new Dictionary<string, object?> { ["type"] = type };
        if (cwd is not null) line["cwd"] = cwd;
        line["sessionId"] = Guid.NewGuid().ToString("D");
        line["timestamp"] = "2026-10-08T10:00:00Z";
        line["message"] = new Dictionary<string, object?> { ["role"] = type, ["content"] = "made up" };
        return JsonSerializer.Serialize(line);
    }

    /// <summary>
    /// Writes &lt;projects&gt;\&lt;projectFolder&gt;\&lt;fileName&gt; (default &lt;guid&gt;.jsonl) from
    /// <paramref name="lines"/>, one per line, and sets its last write time when given.
    /// </summary>
    public string WriteTranscriptLines(string projectFolder, IEnumerable<string> lines, DateTime? lastWriteUtc = null, string? fileName = null)
    {
        string dir = Path.Combine(ProjectsRoot, projectFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, fileName ?? "x"))!);
        string file = Path.Combine(dir, fileName ?? Guid.NewGuid().ToString("D") + ".jsonl");
        File.WriteAllText(file, string.Join("\n", lines) + "\n");
        if (lastWriteUtc is not null) File.SetLastWriteTimeUtc(file, lastWriteUtc.Value);
        return file;
    }

    /// <summary>
    /// Writes a typical transcript naming <paramref name="cwd"/>: a first line without a cwd,
    /// then a user and an assistant line with it.
    /// </summary>
    public string WriteTranscript(string projectFolder, string cwd, DateTime lastWriteUtc, string? fileName = null) =>
        WriteTranscriptLines(projectFolder,
            new[] { TranscriptLine(null, "summary"), TranscriptLine(cwd, "user"), TranscriptLine(cwd, "assistant") },
            lastWriteUtc, fileName);

    /// <summary>Options with the desktop root set to this fixture's session store.</summary>
    public CatalogOptions Options() => new() { DesktopSessionsRoot = SessionsRoot };

    /// <summary>Options with both roots set to this fixture's session store and projects folder.</summary>
    public CatalogOptions BothSources() => new() { DesktopSessionsRoot = SessionsRoot, TerminalProjectsRoot = ProjectsRoot };

    /// <summary>A UTC write time <paramref name="unixMs"/> milliseconds after the epoch.</summary>
    public static DateTime Utc(long unixMs) => DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;

    /// <summary>Obtains the catalog the way a consumer does: through <see cref="CatalogServices.AddCatalog"/>.</summary>
    public static IProjectCatalog Catalog(CatalogOptions options) =>
        new ServiceCollection().AddCatalog(options).BuildServiceProvider().GetRequiredService<IProjectCatalog>();

    public static Task<IReadOnlyList<ProjectEntry>> ListAsync(CatalogOptions options) => Catalog(options).ListAsync();

    public static DateTimeOffset Ms(long unixMs) => DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
}
