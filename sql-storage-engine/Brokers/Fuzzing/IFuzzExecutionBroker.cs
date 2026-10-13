// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.Fuzzing;
internal interface IFuzzExecutionBroker
{
    IDisposable Open(int seed, Action<string, byte[], string, string> save);

    int Next(IDisposable run, int maximum);

    void Save(IDisposable run, string target, byte[] bytes, string type, string message);

    ValueTask<(bool TimedOut, Exception? Failure)> ExecuteAsync(IDisposable run, Action<ReadOnlyMemory<byte>> decode, byte[] bytes, TimeSpan timeout, CancellationToken cancellationToken);
}