// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.Fuzzing;

namespace sql_storage_engine.Brokers.Fuzzing;
internal sealed class FuzzExecutionBroker : IFuzzExecutionBroker
{
    public IDisposable Open(int seed, Action<string, byte[], string, string> save) =>
        new FuzzExecutionDependency(seed: seed, save: save);

    public int Next(IDisposable run, int maximum) =>
        ((FuzzExecutionDependency)run).Next(maximum: maximum);

    public void Save(IDisposable run, string target, byte[] bytes, string type, string message) =>
        ((FuzzExecutionDependency)run).Save(target: target, bytes: bytes, type: type, message: message);

    public ValueTask<(bool TimedOut, Exception? Failure)> ExecuteAsync(IDisposable run, Action<ReadOnlyMemory<byte>> decode, byte[] bytes, TimeSpan timeout, CancellationToken cancellationToken) =>
        ((FuzzExecutionDependency)run).ExecuteAsync(decode: decode, bytes: bytes, timeout: timeout, cancellationToken: cancellationToken);
}