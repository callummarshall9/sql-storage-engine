using SqlExecutionEngine.Storage.Abstractions;

namespace SqlStorageEngine.Core;

/// <summary>Host-resolved neutral-store locations. Existing ordinary SQL files are never implicitly converted.</summary>
public sealed class PageBackendFactory : IBackendFactory
{
    private readonly Func<ByteString, string> resolveLocation;
    private readonly PageBackendOptions options;

    public PageBackendFactory(Func<ByteString, string> resolveLocation, PageBackendOptions? options = null)
    {
        this.resolveLocation = resolveLocation ?? throw new ArgumentNullException(nameof(resolveLocation));
        this.options = options ?? new();
    }

    internal Func<string, ValueTask>? Checkpoint { get; init; }
    internal Action<string>? DirectoryFlushed { get; init; }

    public async ValueTask<IBackendStore> OpenAsync(BackendOpenRequest request, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Durable page publication requires Linux directory synchronization.");
        ArgumentNullException.ThrowIfNull(request);
        BackendAdmission.Require(options.Descriptor, request);
        cancellationToken.ThrowIfCancellationRequested();
        var path = resolveLocation(request.Location);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try { return await PageBackendStore.OpenAsync(path, request.Mode, options, Checkpoint, cancellationToken, DirectoryFlushed).ConfigureAwait(false); }
        catch (sql_storage_engine.Storage.StorageException) { throw new IOException("Page backend open or recovery failed."); }
        catch (IOException) { throw new IOException("Page backend open or recovery failed."); }
        catch (UnauthorizedAccessException) { throw new UnauthorizedAccessException("Page backend location access denied."); }
    }
}
