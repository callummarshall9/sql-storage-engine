using SqlExecutionEngine.Storage.Abstractions;
using SqlStorageEngine.Core;

internal static class PageCrashProbe
{
    internal static async Task RunAsync(string[] args)
    {
        var factory = new PageBackendFactory(_ => args[0])
        {
            DirectoryFlushed = _ =>
            {
                if (args[2] != "directory-flushed") return;
                Console.Write("CHECKPOINT\n");
                Console.Out.Flush();
                Task.Delay(Timeout.InfiniteTimeSpan).GetAwaiter().GetResult();
            },
            Checkpoint = async stage =>
            {
                if (stage != args[2]) return;
                Console.Write("CHECKPOINT\n");
                Console.Out.Flush();
                await Task.Delay(Timeout.InfiniteTimeSpan);
            }
        };
        await using var store = await factory.OpenAsync(new(new([1]), BackendOpenMode.CreateNew));
        await using var snapshot = await store.OpenSnapshotAsync();
        await store.CommitAsync(new(new(Guid.Parse(args[3])), store.Id, snapshot.Generation,
            [new(new(Guid.Parse(args[4])), new([1]), new([42]))]));
        throw new InvalidOperationException("Requested checkpoint was not reached.");
    }
}
