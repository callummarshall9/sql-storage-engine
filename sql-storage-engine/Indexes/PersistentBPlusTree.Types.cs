// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Indexes;
public enum IndexInsertResult
{
    Inserted,
    SplitRequired
}

public interface IIndexRootReference
{
    PageId ReadRootPageId();

    ValueTask UpdateRootAsync(PageId rootPageId, CancellationToken cancellationToken = default);
}