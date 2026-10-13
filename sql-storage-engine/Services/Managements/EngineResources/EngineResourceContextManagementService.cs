// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Buffers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Managements.EngineResources;
internal sealed partial class EngineResourceContextManagementService : IEngineResourceContextManagementService
{
    private readonly sql_storage_engine.Services.Coordinations.PageDatabases.PageDatabaseContextCoordinationService database;
    private readonly sql_storage_engine.Services.Coordinations.EngineLifecycles.EngineLifecycleResourceCoordinationService lifecycle;
    private readonly sql_storage_engine.Services.Coordinations.EngineDurabilities.EngineDurabilityResourceCoordinationService durability;
    private EngineResourceContextManagementService(sql_storage_engine.Services.Coordinations.PageDatabases.PageDatabaseContextCoordinationService database, sql_storage_engine.Services.Coordinations.EngineLifecycles.EngineLifecycleResourceCoordinationService lifecycle, sql_storage_engine.Services.Coordinations.EngineDurabilities.EngineDurabilityResourceCoordinationService durability)
    {
        this.database = database;
        this.lifecycle = lifecycle;
        this.durability = durability;
    }

    internal static EngineResourceContextManagementService CreateEngineResourceContextManagementService() =>
        new(database: sql_storage_engine.Services.Coordinations.PageDatabases.PageDatabaseContextCoordinationService.CreatePageDatabaseContextCoordinationService(), lifecycle: sql_storage_engine.Services.Coordinations.EngineLifecycles.EngineLifecycleResourceCoordinationService.CreateEngineLifecycleResourceCoordinationService(), durability: sql_storage_engine.Services.Coordinations.EngineDurabilities.EngineDurabilityResourceCoordinationService.CreateEngineDurabilityResourceCoordinationService());

    public EngineResourceContext AcquireEngineResourceContextFile(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.AcquireEngineResourceContextFile(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext CloseEngineResourceContextFile(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.CloseEngineResourceContextFile(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReadEngineResourceContextAccess(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.ReadEngineResourceContextAccess(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> EnterEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await lifecycle.EnterEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext EnterImmediateEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.EnterImmediateEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ActivateEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.ActivateEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReleaseEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.ReleaseEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> DrainEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await lifecycle.DrainEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext RegisterScopeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.RegisterScopeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> EnterSecurityEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await lifecycle.EnterSecurityEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext ReleaseSecurityEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.ReleaseSecurityEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> FlushBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await lifecycle.FlushBufferEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> DiscardBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await lifecycle.DiscardBufferEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> CloseBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await lifecycle.CloseBufferEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> CreateJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.CreateJournalEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> CommitJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.CommitJournalEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> CloseJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.CloseJournalEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext RemoveJournalEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return durability.RemoveJournalEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> ReadJournalSnapshotEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.ReadJournalSnapshotEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> ReadIdentityEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.ReadIdentityEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext ReadFileEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return durability.ReadFileEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext RemoveFileEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return durability.RemoveFileEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> RecoverJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.RecoverJournalEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext RemoveSavepointsEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return durability.RemoveSavepointsEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> WriteMetadataEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.WriteMetadataEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> ResolveReceiptEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.ResolveReceiptEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> WriteReceiptEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.WriteReceiptEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext ReleaseReceiptEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return durability.ReleaseReceiptEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReadSnapshotIdentityEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return durability.ReadSnapshotIdentityEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> RecoverEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await durability.RecoverEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> FlushDatabaseEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await database.FlushPageDatabaseContextAsync(pageDatabaseContext: engineResourceContext.Database!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return ReadCoreEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> ReloadHeaderEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await database.ReloadPageDatabaseContextHeaderAsync(pageDatabaseContext: engineResourceContext.Database!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return ReadCoreEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReadEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return ReadCoreEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext RestoreLengthEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        database.RestorePageDatabaseContextLength(pageDatabaseContext: engineResourceContext.Database!, length: engineResourceContext.Length);
        return engineResourceContext;
    });

    public EngineResourceContext SetMaximumLengthEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        database.SetPageDatabaseContextMaximumLength(pageDatabaseContext: engineResourceContext.Database!, maximumAllocatedBytes: engineResourceContext.MaximumLength);
        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> PublishCatalogRootEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await database.PublishPageDatabaseContextCatalogAsync(pageDatabaseContext: engineResourceContext.Database!, pageId: engineResourceContext.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return ReadCoreEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> CreateEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await OpenCoreEngineResourceContextAsync(engineResourceContext: engineResourceContext, creating: true, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> OpenEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await OpenCoreEngineResourceContextAsync(engineResourceContext: engineResourceContext, creating: false, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> CloseEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        try
        {
            await lifecycle.CloseBufferEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            try
            {
                if (engineResourceContext.Database is not null)
                {
                    await database.ClosePageDatabaseContextAsync(pageDatabaseContext: engineResourceContext.Database)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
            }
            finally
            {
                lifecycle.CloseEngineResourceContextFile(engineResourceContext: engineResourceContext);
            }
        }

        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> RestoreJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);
        var restored = engineResourceContext.Database!;

        var snapshot = await durability.ReadJournalSnapshotEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = engineResourceContext.Journal!.Path }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (snapshot.SnapshotState != 1)
        {
            throw new StorageFormatException(message: "Only an active statement journal can be restored.");
        }

        if (snapshot.Bytes!.LongLength % restored.PageSize != 0)
        {
            throw new StorageFormatException(message: "Statement journal is not page aligned.");
        }

        for (var offset = 0; offset < snapshot.Bytes.Length; offset += restored.PageSize)
        {
            await database.WritePageDatabaseContextAsync(pageDatabaseContext: restored, pageId: checked((ulong)(offset / restored.PageSize)), source: snapshot.Bytes.AsMemory(start: offset, length: restored.PageSize), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        database.RestorePageDatabaseContextLength(pageDatabaseContext: restored, length: snapshot.Bytes.LongLength);

        await database.FlushPageDatabaseContextAsync(pageDatabaseContext: restored, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await database.ReloadPageDatabaseContextHeaderAsync(pageDatabaseContext: restored, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        engineResourceContext.Journal.Completed = !engineResourceContext.Retain;

        if (!engineResourceContext.Retain)
        {
            durability.RemoveFileEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = engineResourceContext.Journal.Path });
        }

        return ReadCoreEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    private EngineResourceContext ReadCoreEngineResourceContext(EngineResourceContext engineResourceContext)
    {
        var current = lifecycle.ReadEngineResourceContextAccess(engineResourceContext: engineResourceContext);

        return current with
        {
            Header = current.Database?.Runtime?.Header,
            DatabaseBinding = current.Database is null ? null : new PageDatabaseBinding
            {
                Service = database,
                Context = current.Database
            }
        };
    }

    private async ValueTask<EngineResourceContext> OpenCoreEngineResourceContextAsync(EngineResourceContext engineResourceContext, bool creating, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: engineResourceContext.Path);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: engineResourceContext.DefaultCollation);

        if (engineResourceContext.BufferCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(engineResourceContext.BufferCapacity));
        }

        lifecycle.AcquireEngineResourceContextFile(engineResourceContext: engineResourceContext with { Creating = creating });

        try
        {
            if (!creating)
            {
                await durability.RecoverEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            var source = new PageDatabaseContext
            {
                Path = engineResourceContext.Path,
                PageSize = engineResourceContext.PageSize,
                DefaultCollation = engineResourceContext.DefaultCollation,
                OpenMode = DatabaseOpenMode.Writer
            };

            var opened = creating ? await database.CreatePageDatabaseContextAsync(pageDatabaseContext: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : await database.OpenPageDatabaseContextAsync(pageDatabaseContext: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                var configuration = new BufferPoolConfiguration
                {
                    ProviderSource = new PageDatabaseBinding
                    {
                        Service = database,
                        Context = opened
                    },
                    Capacity = engineResourceContext.BufferCapacity,
                    LeaveOpen = true,
                    PageSize = () => opened.PageSize,
                    Read = (id, destination, token) => database.ReadPageDatabaseContextAsync(pageDatabaseContext: opened, pageId: id, destination: destination, cancellationToken: token),
                    Write = (id, source, token) => database.WritePageDatabaseContextAsync(pageDatabaseContext: opened, pageId: id, source: source, cancellationToken: token),
                    Flush = token => database.FlushPageDatabaseContextAsync(pageDatabaseContext: opened, cancellationToken: token),
                    Close = () => database.ClosePageDatabaseContextAsync(pageDatabaseContext: opened)
                };

                var context = lifecycle.OpenBufferEngineResourceContext(engineResourceContext: engineResourceContext with { Path = opened.Path, Database = opened, BufferConfiguration = configuration });
                return PrepareEngineResourceContextProviders(engineResourceContext: context);
            }
            catch
            {
                await database.ClosePageDatabaseContextAsync(pageDatabaseContext: opened)
            .ConfigureAwait(continueOnCapturedContext: false);

                throw;
            }
        }
        catch
        {
            lifecycle.CloseEngineResourceContextFile(engineResourceContext: engineResourceContext);
            throw;
        }
    }

    private EngineResourceContext PrepareEngineResourceContextProviders(EngineResourceContext engineResourceContext)
    {
        engineResourceContext = ReadCoreEngineResourceContext(engineResourceContext: engineResourceContext);
        var opened = engineResourceContext.Database!;
        var provider = lifecycle.ReadProviderEngineResourceContext(engineResourceContext: engineResourceContext);
        var buffers = provider.BufferSource!;
        var pin = provider.PinSource!;

        var allocate = new Func<ushort, CancellationToken, ValueTask<ulong>>(async (kind, token) => (await database.AllocatePageDatabaseContextAsync(pageDatabaseContext: opened, pageType: (PageType)kind, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).ResultPageId);

        var free = new Func<ulong, CancellationToken, ValueTask>((id, token) => database.FreePageDatabaseContextAsync(pageDatabaseContext: opened, pageId: id, cancellationToken: token));
        var discard = provider.DiscardSource!;

        var storage = new sql_storage_engine.Models.Catalog.CatalogStorageConfiguration
        {
            Pages = new sql_storage_engine.Models.Catalog.CatalogPageResourceConfiguration
            {
                PageSize = () => opened.PageSize,
                Allocate = allocate,
                Free = free,
                Read = (id, destination, token) => database.ReadPageDatabaseContextAsync(pageDatabaseContext: opened, pageId: id, destination: destination, cancellationToken: token),
                Write = (id, source, token) => database.WritePageDatabaseContextAsync(pageDatabaseContext: opened, pageId: id, source: source, cancellationToken: token)
            },
            Buffer = new sql_storage_engine.Models.Catalog.CatalogBufferConfiguration
            {
                Flush = provider.BufferFlushSource,
                Discard = discard
            },
            Heap = new sql_storage_engine.Models.Heap.HeapPageResourceConfiguration
            {
                BufferSource = buffers,
                AllocatorSource = engineResourceContext.DatabaseBinding,
                PageSize = () => opened.PageSize,
                Pin = pin,
                Allocate = token => allocate(arg1: (ushort)PageType.Heap, arg2: token),
                Free = free,
                Discard = discard
            },
            Tree = new sql_storage_engine.Models.Indexes.IndexTreeResourceConfiguration
            {
                BufferSource = buffers,
                AllocatorSource = engineResourceContext.DatabaseBinding,
                PageSize = () => opened.PageSize,
                Pin = pin,
                Allocate = allocate
            }
        };

        var overflowConfiguration = new sql_storage_engine.Models.Overflow.OverflowPageResourceConfiguration
        {
            BufferSource = buffers,
            AllocatorSource = engineResourceContext.DatabaseBinding,
            PageSize = () => opened.PageSize,
            Pin = pin,
            Allocate = token => allocate(arg1: (ushort)PageType.Overflow, arg2: token),
            Free = free,
            Discard = discard
        };

        return durability.OpenOverflowEngineResourceContext(engineResourceContext: engineResourceContext with { CatalogStorage = storage, OverflowConfiguration = overflowConfiguration });
    }

    public EngineResourceContext GenerateSavepointPathEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return durability.GenerateSavepointPathEngineResourceContext(engineResourceContext: engineResourceContext);
    });
}