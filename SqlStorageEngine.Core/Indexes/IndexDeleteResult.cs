using System.Runtime.CompilerServices;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

/// <summary>Describes an exact index deletion and pages that may be reclaimed after it is safe to do so.</summary>
public sealed record IndexDeleteResult(bool Removed, IReadOnlyList<PageId> RetiredPageIds);
