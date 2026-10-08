using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Feature.Catalog;

/// <summary>
/// The Claude Code projects folder: one subfolder per project, holding one <c>*.jsonl</c>
/// transcript per session at its top level (deeper files are tool results, not sessions). The
/// folder name encodes the path lossily, so the path comes from the first record with a
/// non-empty <c>cwd</c> in any transcript (smallest first); each transcript becomes one sighting
/// dated by its last write. A project folder with no transcript or no readable <c>cwd</c> is
/// skipped. Read-only.
/// </summary>
internal static class TerminalProjectsFolder
{
    private const string TranscriptPattern = "*.jsonl";
    private const int LinesToInspect = 50;

    internal static List<Sighting> Read(string root, CancellationToken cancellationToken)
    {
        var sightings = new List<Sighting>();
        if (!Directory.Exists(root))
        {
            return sightings;
        }

        List<string> projectFolders;
        try
        {
            projectFolders = new List<string>(Directory.EnumerateDirectories(root));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return sightings;
        }

        foreach (string folder in projectFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<FileInfo> transcripts = Transcripts(folder);
            if (transcripts.Count == 0)
            {
                continue;
            }

            string? cwd = CwdOf(transcripts);
            if (cwd is null)
            {
                continue;
            }

            foreach (FileInfo transcript in transcripts)
            {
                sightings.Add(new Sighting(
                    cwd,
                    new DateTimeOffset(transcript.LastWriteTimeUtc),
                    ProjectSources.TerminalProjects));
            }
        }

        return sightings;
    }

    private static List<FileInfo> Transcripts(string folder)
    {
        var transcripts = new List<FileInfo>();
        try
        {
            foreach (string file in Directory.EnumerateFiles(folder, TranscriptPattern, SearchOption.TopDirectoryOnly))
            {
                transcripts.Add(new FileInfo(file));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            transcripts.Clear();
        }

        return transcripts;
    }

    private static string? CwdOf(List<FileInfo> transcripts)
    {
        // The cwd sits in the first records of every transcript, so the smallest file settles it cheaply.
        transcripts.Sort(static (a, b) => a.Length.CompareTo(b.Length));
        foreach (FileInfo transcript in transcripts)
        {
            string? cwd = CwdOf(transcript.FullName);
            if (cwd is not null)
            {
                return cwd;
            }
        }

        return null;
    }

    private static string? CwdOf(string file)
    {
        try
        {
            using var reader = new StreamReader(file);
            for (int i = 0; i < LinesToInspect; i++)
            {
                string? line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (!line.Contains("\"cwd\"", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    using JsonDocument record = JsonDocument.Parse(line);
                    if (record.RootElement.ValueKind == JsonValueKind.Object
                        && record.RootElement.TryGetProperty("cwd", out JsonElement element)
                        && element.ValueKind == JsonValueKind.String)
                    {
                        string? cwd = element.GetString();
                        if (!string.IsNullOrWhiteSpace(cwd))
                        {
                            return cwd;
                        }
                    }
                }
                catch (JsonException)
                {
                    // A cut or odd line: try the next one.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable transcript: the next one may do.
        }

        return null;
    }
}
