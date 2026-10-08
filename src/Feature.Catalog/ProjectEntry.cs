using System;

namespace Feature.Catalog;

/// <summary>One project folder known to Claude on this machine, as the catalog returns it.</summary>
/// <param name="Path">
/// The absolute folder path as Claude recorded it, without a trailing separator. Records that
/// differ only in letter case are one entry; the spelling of the most recent record wins.
/// </param>
/// <param name="Name">The last path segment (for a drive root such as <c>C:\</c>, the path itself).</param>
/// <param name="LastActivity">
/// The newest activity across all sources: a desktop record's <c>lastActivityAt</c> (or its
/// <c>createdAt</c> when that is missing, else the record file's last write time), a
/// transcript's last write time. UTC.
/// </param>
/// <param name="Sources">Which sources knew the folder.</param>
/// <param name="SessionCount">
/// The number of session records that name the folder, summed over the sources (desktop
/// records plus transcripts). At least 1.
/// </param>
/// <param name="FolderExists">Whether the folder existed on disk when the catalog scanned.</param>
public sealed record ProjectEntry(
    string Path,
    string Name,
    DateTimeOffset LastActivity,
    ProjectSources Sources,
    int SessionCount,
    bool FolderExists);
