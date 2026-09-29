namespace SqlExecutionEngine.Storage.Conformance;

/// <summary>Passed describes this check only, never certification of an entire backend.</summary>
public sealed record ConformanceResult(string Provider, string Check, string Status, string Detail);
