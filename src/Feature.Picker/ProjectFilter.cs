using System;
using System.Collections.Generic;
using Feature.Catalog;

namespace Feature.Picker;

/// <summary>Type-to-filter over the catalog's rows, as a plain function apart from the window.</summary>
public static class ProjectFilter
{
    private static readonly char[] Separators = { ' ', '\t' };

    /// <summary>
    /// The rows that match <paramref name="query"/>, in their original order. A null, empty or
    /// whitespace query keeps every row. Otherwise the query is split into words at spaces and
    /// tabs and a row is kept when every word occurs in its <see cref="ProjectEntry.Name"/> or its
    /// <see cref="ProjectEntry.Path"/>, ignoring case (ordinal). Never null; the same list
    /// instance is returned for a null, empty or whitespace query. The rows are not changed.
    /// Throws <see cref="ArgumentNullException"/> for a null <paramref name="entries"/>.
    /// </summary>
    public static IReadOnlyList<ProjectEntry> Apply(IReadOnlyList<ProjectEntry> entries, string? query)
    {
        ArgumentNullException.ThrowIfNull(entries);
        string[] words = Words(query);
        if (words.Length == 0)
        {
            return entries;
        }

        var kept = new List<ProjectEntry>(entries.Count);
        foreach (ProjectEntry entry in entries)
        {
            if (Matches(entry, words))
            {
                kept.Add(entry);
            }
        }

        return kept;
    }

    private static string[] Words(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Array.Empty<string>()
            : query.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool Matches(ProjectEntry entry, string[] words)
    {
        foreach (string word in words)
        {
            if (!entry.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                && !entry.Path.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
