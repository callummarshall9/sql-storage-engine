using System.Text.Json;

namespace SqlExecutionEngine.Storage.Conformance;

/// <summary>Only the runner constructs reports; outstanding obligations always make the gate fail.</summary>
public sealed class ConformanceReport
{
    private readonly bool complete;
    internal ConformanceReport(IEnumerable<ConformanceResult> results, IEnumerable<BackendCapabilityEvidence> capabilities,
        bool complete = true)
    {
        this.complete = complete;
        Results = Array.AsReadOnly(results.ToArray());
        Capabilities = Array.AsReadOnly(capabilities.ToArray());
    }
    public int SchemaVersion => 2;
    public string Profile => "DurableAtomicV1";
    public string Scope => complete
        ? "Registered providers only; independent two-provider portability requires separate evidence."
        : "Focused diagnostic only; not a provider conformance certification.";
    public IReadOnlyList<BackendCapabilityEvidence> Capabilities { get; }
    public IReadOnlyList<ConformanceResult> Results { get; }
    public bool Passed => complete && Results.Count > 0 && Results.All(row => row.Status == "Passed");
    public int ExitCode => Passed ? 0 : 1;
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}
