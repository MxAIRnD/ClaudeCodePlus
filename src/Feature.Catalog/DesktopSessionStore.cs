using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Feature.Catalog;

/// <summary>
/// The desktop app's session store: every <c>local_*.json</c> under the root (recursively) is
/// one session record; the ones that name a folder become sightings. Read-only; a file that
/// cannot be read or parsed, or has no usable <c>originCwd</c>, is skipped.
/// </summary>
internal static class DesktopSessionStore
{
    private const string RecordPattern = "local_*.json";
    private const long MaxUnixMilliseconds = 253_402_300_799_999; // 9999-12-31, the DateTimeOffset ceiling

    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    internal static List<Sighting> Read(string root, CancellationToken cancellationToken)
    {
        var sightings = new List<Sighting>();
        if (!Directory.Exists(root))
        {
            return sightings;
        }

        List<string> files;
        try
        {
            files = new List<string>(Directory.EnumerateFiles(root, RecordPattern, Recursive));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return sightings;
        }

        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sighting? sighting = ReadRecord(file);
            if (sighting is not null)
            {
                sightings.Add(sighting);
            }
        }

        return sightings;
    }

    private static Sighting? ReadRecord(string file)
    {
        try
        {
            using FileStream stream = File.OpenRead(file);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement record = document.RootElement;
            if (record.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!record.TryGetProperty("originCwd", out JsonElement cwdElement)
                || cwdElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? cwd = cwdElement.GetString();
            if (string.IsNullOrWhiteSpace(cwd))
            {
                return null;
            }

            DateTimeOffset when = UnixMilliseconds(record, "lastActivityAt")
                ?? UnixMilliseconds(record, "createdAt")
                ?? new DateTimeOffset(File.GetLastWriteTimeUtc(file));
            return new Sighting(cwd, when, ProjectSources.DesktopSessions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? UnixMilliseconds(JsonElement record, string name)
    {
        if (record.TryGetProperty(name, out JsonElement element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt64(out long milliseconds)
            && milliseconds > 0
            && milliseconds <= MaxUnixMilliseconds)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        return null;
    }
}
