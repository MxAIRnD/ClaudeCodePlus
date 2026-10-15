using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Feature.ContextBar;

/// <summary>
/// Which session the Claude app shows, as the desktop app's session store tells it. Every
/// <c>local_*.json</c> under the root (recursively) is one record, with the session's
/// <c>title</c>, its <c>cliSessionId</c> (the engine's session id, under which the context-bar
/// mod publishes its figures), <c>lastFocusedAt</c> and <c>isArchived</c>. Only unarchived
/// records that name a <c>cliSessionId</c> count. Given the shown session's title (the app's
/// page title, read by the probe) the answer is the record with that exact title and the newest
/// <c>lastFocusedAt</c> among those; without a title, or when none has it, the record with the
/// newest <c>lastFocusedAt</c> of all. A record is parsed again only when its size or last
/// write time changed; one that cannot be opened or parsed (a record being written) is skipped
/// and tried again next time. The folder is rescanned at most once per <see cref="MinInterval"/>
/// by <see cref="Find"/>, always by <see cref="FindNow"/>; the answer itself is computed from
/// what the last scan read, every call. Read-only; safe to call from one thread at a time.
/// </summary>
public sealed class FocusedSessionFinder
{
    /// <summary>How long <see cref="Find"/> keeps answering from the last scan of the folder.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    private const string RecordPattern = "local_*.json";

    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    private readonly string? _root;
    private Dictionary<string, Memo> _memo = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _scannedAtUtc = DateTime.MinValue;

    /// <summary>
    /// Creates a finder over <paramref name="sessionsRoot"/>, normally
    /// <c>%APPDATA%\Claude\claude-code-sessions</c>. Null, or a folder that does not exist:
    /// every answer is null.
    /// </summary>
    public FocusedSessionFinder(string? sessionsRoot)
    {
        _root = sessionsRoot;
    }

    /// <summary>
    /// The shown session's engine id, from a scan at most <see cref="MinInterval"/> old;
    /// <paramref name="shownTitle"/> is the app's page title when known. Null when none is known.
    /// </summary>
    public string? Find(string? shownTitle = null)
    {
        if (DateTime.UtcNow - _scannedAtUtc >= MinInterval)
        {
            Scan();
        }

        return Pick(shownTitle);
    }

    /// <summary>The shown session's engine id from a fresh scan; null when none is known.</summary>
    public string? FindNow(string? shownTitle = null)
    {
        Scan();
        return Pick(shownTitle);
    }

    private string? Pick(string? shownTitle)
    {
        string? titled = null;
        long titledFocusedAt = long.MinValue;
        string? any = null;
        long anyFocusedAt = long.MinValue;
        foreach (Memo memo in _memo.Values)
        {
            if (memo.Id is null || memo.IsArchived)
            {
                continue;
            }

            if (memo.FocusedAt > anyFocusedAt)
            {
                any = memo.Id;
                anyFocusedAt = memo.FocusedAt;
            }

            if (shownTitle is not null && string.Equals(memo.Title, shownTitle, StringComparison.Ordinal) && memo.FocusedAt > titledFocusedAt)
            {
                titled = memo.Id;
                titledFocusedAt = memo.FocusedAt;
            }
        }

        return titled ?? any;
    }

    private void Scan()
    {
        _scannedAtUtc = DateTime.UtcNow;
        if (_root is null || !Directory.Exists(_root))
        {
            _memo = new Dictionary<string, Memo>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        List<FileInfo> files;
        try
        {
            files = new List<FileInfo>(new DirectoryInfo(_root).EnumerateFiles(RecordPattern, Recursive));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        Dictionary<string, Memo> previous = _memo;
        var next = new Dictionary<string, Memo>(files.Count, StringComparer.OrdinalIgnoreCase);
        foreach (FileInfo file in files)
        {
            long length = file.Length;
            DateTime written = file.LastWriteTimeUtc;
            if (!previous.TryGetValue(file.FullName, out Memo memo) || memo.Length != length || memo.LastWriteUtc != written)
            {
                if (!TryReadRecord(file.FullName, out Memo fresh))
                {
                    continue;
                }

                memo = fresh with { Length = length, LastWriteUtc = written };
            }

            next[file.FullName] = memo;
        }

        _memo = next;
    }

    /// <summary>
    /// False when the file could not be opened, read or parsed (worth another try next time).
    /// True otherwise, with a null <c>Id</c> when the record names no usable session.
    /// </summary>
    private static bool TryReadRecord(string file, out Memo memo)
    {
        memo = default;
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            string? id = Text(root, "cliSessionId");
            long focusedAt = root.TryGetProperty("lastFocusedAt", out JsonElement focused) && focused.ValueKind == JsonValueKind.Number
                && focused.TryGetInt64(out long ms)
                    ? ms
                    : 0;
            bool isArchived = root.TryGetProperty("isArchived", out JsonElement archived) && archived.ValueKind == JsonValueKind.True;
            memo = new Memo(0, DateTime.MinValue, string.IsNullOrEmpty(id) ? null : id, Text(root, "title"), focusedAt, isArchived);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>What one record file yielded, with the size and write time it had then.</summary>
    private readonly record struct Memo(long Length, DateTime LastWriteUtc, string? Id, string? Title, long FocusedAt, bool IsArchived);
}
