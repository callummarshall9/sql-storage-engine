// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine;
/// <summary>Read/write access valid only inside one awaited transaction callback.</summary>
public interface IStorageTransactionContext : IStorageStatement
{
    Security.IStorageSecurityContext ReadSecurity();

    IStorageCatalog ReadCatalog();

    ValueTask<IStorageIndex> OpenIndexAsync(IndexId indexId, CancellationToken cancellationToken = default);
}