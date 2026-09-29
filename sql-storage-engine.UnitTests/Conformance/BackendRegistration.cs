using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

/// <summary>A host supplies a factory and a fresh disposable location for every independent case.</summary>
public sealed record BackendRegistration(string Name, IBackendFactory Factory,
    Func<CancellationToken, ValueTask<BackendTestLocation>> CreateLocation, BackendTestDriver? Driver = null);
