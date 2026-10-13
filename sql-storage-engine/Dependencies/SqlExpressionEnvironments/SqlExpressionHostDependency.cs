// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;

namespace sql_storage_engine.Dependencies.SqlExpressionEnvironments;
internal sealed class SqlExpressionHostDependency : IDisposable
{
    private readonly ReaderWriterLockSlim runtimeSync = new();
    private Func<string, object, object>? runtime;
    public void Register(Func<string, object, object>? runtime)
    {
        runtimeSync.EnterWriteLock();

        try
        {
            this.runtime = runtime;
        }
        finally
        {
            runtimeSync.ExitWriteLock();
        }
    }

    public object? Evaluate(string source, object columns)
    {
        Func<string, object, object>? callback;
        runtimeSync.EnterReadLock();

        try
        {
            callback = runtime;
        }
        finally
        {
            runtimeSync.ExitReadLock();
        }

        return callback?.Invoke(arg1: source, arg2: columns);
    }

    public void Dispose() =>
        runtimeSync.Dispose();
}