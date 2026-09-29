using System.Runtime.CompilerServices;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

public readonly record struct IndexRange(
    IndexKey? LowerBound,
    IndexKey? UpperBound,
    bool IncludeLowerBound = true,
    bool IncludeUpperBound = true,
    ScanDirection Direction = ScanDirection.Ascending);
