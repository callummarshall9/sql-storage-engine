// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using sql_storage_engine.Models.Access;
using sql_storage_engine.Brokers.Access;

namespace sql_storage_engine.Services.Foundations.StorageAccesses;
internal sealed partial class StorageAccessService : IStorageAccessService
{
    private readonly StorageGateBroker broker = new();
    private readonly StorageGateState gate = new()
    {
        Sync = new object (),
        Owner = new object ()
    };
    public StorageAccess? ReadCurrentStorageAccess() =>
        TryCatch<StorageAccess?>(operation: () =>
    {
        var scope = (StorageAccessScopeState? )broker.ReadContext();
        return scope is null ? null : CreateStorageAccess(scope: scope);
    });

    public ValueTask<StorageAccess> EnterStorageAccessAsync(StorageAccess? storageAccess, CancellationToken token, bool reentrant) =>
        TryCatch<StorageAccess>(operation: async () =>
    {
        ValidateStorageAccessEntry(storageAccess: storageAccess, token: token, reentrant: reentrant);
        var parent = reentrant ? (StorageAccessScopeState? )broker.ReadContext() : null;

        if (parent is not null)
        {
            EnterChild(scope: parent);
            return CreateStorageAccessLease(parent: parent, ownsSemaphore: false);
        }

        var lease = CreateStorageAccessLease(parent: null, ownsSemaphore: true);
        ReserveWait();

        try
        {
            await broker.WaitAsync(token: token)
            .ConfigureAwait(continueOnCapturedContext: false);

            lock (gate.Sync!)
            {
                gate.Pending--;
                gate.Leases++;
            }

            return lease;
        }
        catch
        {
            lock (gate.Sync!)
            {
                gate.Pending--;
            }

            throw;
        }
    });

    public StorageAccess EnterImmediateStorageAccess(StorageAccess? storageAccess) =>
        TryCatch(operation: () =>
    {
        ValidateStorageAccessEntry(storageAccess: storageAccess, token: CancellationToken.None, reentrant: true);
        var parent = (StorageAccessScopeState? )broker.ReadContext();

        if (parent is not null)
        {
            EnterChild(scope: parent);
            return CreateStorageAccessLease(parent: parent, ownsSemaphore: false);
        }

        lock (gate.Sync!)
        {
            ValidateOpenUnderLock();

            if (!broker.TryWait())
            {
                throw new InvalidOperationException(message: "Database access is busy; retry through an asynchronous transaction scope.");
            }

            gate.Leases++;
        }

        return CreateStorageAccessLease(parent: null, ownsSemaphore: true);
    });

    private void ReserveWait()
    {
        lock (gate.Sync!)
        {
            ValidateOpenUnderLock();
            gate.Pending = checked(gate.Pending + 1);
        }
    }

    private StorageAccess CreateStorageAccessLease(StorageAccessScopeState? parent, bool ownsSemaphore) =>
        new StorageAccess
    {
        State = new StorageAccessState
        {
            Owner = gate.Owner,
            Parent = parent,
            OwnsSemaphore = ownsSemaphore
        }
    };

    private StorageAccess CreateStorageAccess(StorageAccessScopeState scope) =>
        new StorageAccess
    {
        State = new StorageAccessState
        {
            Owner = gate.Owner,
            Scope = scope
        }
    };

    public StorageAccess ActivateStorageAccess(StorageAccess storageAccess) =>
        TryCatch(operation: () =>
    {
        var state = ValidateStorageAccess(storageAccess: storageAccess);

        if (state.Activation || state.Scope is not null)
        {
            throw new ArgumentException(message: "Activation requires a storage access lease.", paramName: nameof(storageAccess));
        }

        lock (state)
        {
            if (state.Disposed)
            {
                throw new ObjectDisposedException(objectName: "StorageAccessLease");
            }

            var previous = (StorageAccessScopeState? )broker.ReadContext();

            var scope = new StorageAccessScopeState
            {
                Sync = new object (),
                Owner = gate.Owner,
                Active = true,
                Resources = new List<IAsyncDisposable>()
            };

            broker.SetContext(value: scope);

            return new StorageAccess
            {
                State = new StorageAccessState
                {
                    Owner = gate.Owner,
                    Activation = true,
                    Scope = scope,
                    Previous = previous
                }
            };
        }
    });

    public void RegisterStorageAccessResource(StorageAccess storageAccess, IAsyncDisposable resource) =>
        TryCatch(operation: () =>
    {
        var state = ValidateStorageAccessRegistration(storageAccess: storageAccess, resource: resource);
        var scope = state.Scope!;

        lock (scope.Sync!)
        {
            if (!scope.Active)
            {
                throw new InvalidOperationException(message: "The statement scope has completed.");
            }

            scope.Resources!.Add(item: resource);
        }
    });

    public Task DrainStorageAccessAsync(StorageAccess storageAccess) =>
        TryCatch(operation: () =>
    {
        var state = ValidateStorageAccess(storageAccess: storageAccess);
        ArgumentNullException.ThrowIfNull(argument: state.Scope);
        return StartDrain(scope: state.Scope);
    });

    private Task StartDrain(StorageAccessScopeState scope)
    {
        lock (scope.Sync!)
        {
            if (scope.Drain is not null)
            {
                return scope.Drain;
            }

            scope.Active = false;
            var resources = scope.Resources!.ToArray();
            scope.Resources.Clear();
            var child = scope.Child is null ? Task.CompletedTask : broker.ReadCompletion(completion: scope.Child);
            scope.Drain = DrainCoreAsync(resources: resources, child: child);
            return scope.Drain;
        }
    }

    private async Task DrainCoreAsync(IAsyncDisposable[] resources, Task child)
    {
        List<Exception>? failures = null;

        foreach (var resource in resources)
        {
            try
            {
                await broker.DisposeResourceAsync(resource: resource)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (Exception exception)
            {
                (failures ??= new List<Exception>()).Add(item: exception);
            }
        }

        try
        {
            await child.ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception exception)
        {
            (failures ??= new List<Exception>()).Add(item: exception);
        }

        if (failures is not null)
        {
            throw new sql_storage_engine.Models.Exceptions.StorageScopeDrainException(failures: failures);
        }
    }

    private void EnterChild(StorageAccessScopeState scope)
    {
        lock (scope.Sync!)
        {
            if (!scope.Active)
            {
                throw new InvalidOperationException(message: "Storage access scope has ended.");
            }

            if (scope.Child is not null)
            {
                throw new InvalidOperationException(message: "Concurrent storage operations inside a callback are not supported.");
            }

            scope.Child = broker.CreateCompletion();
        }
    }

    private void ExitChild(StorageAccessScopeState scope)
    {
        lock (scope.Sync!)
        {
            if (scope.Child is not null)
            {
                broker.Complete(completion: scope.Child);
            }

            scope.Child = null;
        }
    }

    public void CloseStorageAccess(StorageAccess? storageAccess) =>
        TryCatch(operation: () =>
    {
        ValidateStorageAccessClose(storageAccess: storageAccess);
        CloseCoreStorageAccess(storageAccess: storageAccess);
    });

    public IDisposable ReadStorageAccessLifetime(StorageAccess storageAccess) =>
        TryCatch(operation: () =>
    {
        ValidateStorageAccessClose(storageAccess: storageAccess);
        return broker.CreateLifetime(release: () => ReleaseStorageAccess(storageAccess: storageAccess));
    });

    private void ReleaseStorageAccess(StorageAccess storageAccess) =>
        TryCatch(operation: () =>
    {
        ValidateStorageAccessClose(storageAccess: storageAccess);
        CloseCoreStorageAccess(storageAccess: storageAccess);
    });

    private void CloseCoreStorageAccess(StorageAccess? storageAccess)
    {
        if (storageAccess is null)
        {
            lock (gate.Sync!)
            {
                if (gate.Closed)
                {
                    return;
                }

                if (gate.Leases != 0 || gate.Pending != 0)
                {
                    throw new InvalidOperationException(message: "Cannot close an active storage access gate.");
                }

                gate.Closed = true;
                broker.Close();
            }

            return;
        }

        var state = storageAccess.State!;

        lock (state)
        {
            if (state.Disposed)
            {
                return;
            }

            state.Disposed = true;

            if (state.Activation)
            {
                _ = StartDrain(scope: state.Scope!);
                broker.SetContext(value: state.Previous);
            }
            else if (state.OwnsSemaphore)
            {
                lock (gate.Sync!)
                {
                    broker.Release();
                    gate.Leases--;
                }
            }
            else if (state.Parent is not null)
            {
                ExitChild(scope: state.Parent);
            }
        }
    }
}