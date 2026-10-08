using System;

namespace Feature.Catalog;

/// <summary>One record of one source naming a folder: the raw material of the merge.</summary>
/// <param name="Path">The folder path exactly as the record spells it.</param>
/// <param name="When">The record's activity time, UTC.</param>
/// <param name="Source">The source that produced the record.</param>
internal sealed record Sighting(string Path, DateTimeOffset When, ProjectSources Source);
