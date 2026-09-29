namespace sql_storage_engine.UnitTests;

internal sealed class PagePublicationFault : IAsyncDisposable
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
