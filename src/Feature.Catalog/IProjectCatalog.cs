using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Feature.Catalog;

/// <summary>The ranked list of Claude project folders on this machine.</summary>
public interface IProjectCatalog
{
    /// <summary>
    /// Scans the configured sources and returns every project folder: most recently used
    /// first (ties broken by path, ascending, ordinal, ignoring case), one entry per folder, noise removed
    /// per <see cref="CatalogOptions"/>. Reads only; every call scans afresh (no cache). Folders
    /// merge when their paths match ignoring letter case and one trailing separator; otherwise a
    /// path is taken as recorded (no separator or short-name normalization). The noise markers
    /// match ignoring case. A source whose root is null or missing
    /// contributes nothing; a file that cannot be read or parsed is skipped; a record without
    /// a usable folder path is skipped. An empty list means no folder was found — with a
    /// configured source present on disk that usually means the format changed. Throws
    /// <see cref="System.OperationCanceledException"/> when <paramref name="cancellationToken"/>
    /// is cancelled; nothing else is thrown for data problems.
    /// </summary>
    Task<IReadOnlyList<ProjectEntry>> ListAsync(CancellationToken cancellationToken = default);
}
