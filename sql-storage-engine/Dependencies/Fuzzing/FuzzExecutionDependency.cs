// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.Fuzzing;
internal sealed class FuzzExecutionDependency : IDisposable
{
    private readonly Random random;
    private readonly Action<string, byte[], string, string> save;
    private readonly List<Task> workers = new List<Task>();
    public FuzzExecutionDependency(int seed, Action<string, byte[], string, string> save)
    {
        random = new Random(Seed: seed);
        this.save = save;
    }

    public int Next(int maximum) =>
        random.Next(maxValue: maximum);

    public void Save(string target, byte[] bytes, string type, string message) =>
        save(arg1: target, arg2: bytes, arg3: type, arg4: message);

    public async ValueTask<(bool TimedOut, Exception? Failure)> ExecuteAsync(Action<ReadOnlyMemory<byte>> decode, byte[] bytes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);
        var worker = Task.Run(action: () => decode(obj: bytes), cancellationToken: cancellationToken);
        workers.Add(item: worker);
        var delay = Task.Delay(delay: timeout, cancellationToken: deadline.Token);

        var completed = await Task.WhenAny(task1: worker, task2: delay)
            .ConfigureAwait(continueOnCapturedContext: false);

        cancellationToken.ThrowIfCancellationRequested();
        deadline.Cancel();

        if (completed != worker)
            return (true, null);

        workers.Remove(item: worker);

        try
        {
            await worker.ConfigureAwait(continueOnCapturedContext: false);
            return (false, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
        finally
        {
            worker.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var worker in workers)
        {
            if (worker.IsCompleted)
                worker.Dispose();
            else
                _ = worker.ContinueWith(continuationAction: task =>
                {
                    _ = task.Exception;
                    task.Dispose();
                }, cancellationToken: CancellationToken.None, continuationOptions: TaskContinuationOptions.ExecuteSynchronously, scheduler: TaskScheduler.Default);
        }

        workers.Clear();
    }
}