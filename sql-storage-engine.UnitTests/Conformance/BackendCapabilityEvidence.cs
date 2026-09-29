using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

/// <summary>Advertised capabilities, not certification; corresponding result rows decide whether they were proved.</summary>
public sealed record BackendCapabilityEvidence(string Provider, BackendDescriptor Advertised);
