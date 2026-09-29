using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

/// <summary>Runs identical independent checks for every registration. Missing evidence fails closed.</summary>
public static class BackendConformanceRunner
{
    public static async Task<ConformanceReport> RunAsync(IEnumerable<BackendRegistration> registrations,
        CancellationToken cancellationToken = default)
        => await RunCoreAsync(registrations, null, cancellationToken);

    // Test-only diagnostics: never use a single check as a provider certification report.
    internal static Task<ConformanceReport> RunCheckAsync(BackendRegistration registration, string check,
        CancellationToken cancellationToken = default)
    {
        if (check is not ("atomic-snapshot-range-replay" or "orderly-reopen-receipts" or
            "pre-admission-cancellation-invalid-handles-disposal" or "exact-and-over-resource-quotas" or
            "concurrent-publication-and-disposal" or "snapshot-expiry" or "real-process-exit-recovery" or
            "injected-io-outcomes" or "optional-capability-proof"))
            throw new ArgumentException("Unknown conformance check.", nameof(check));
        return RunCoreAsync([registration], check, cancellationToken);
    }

    private static async Task<ConformanceReport> RunCoreAsync(IEnumerable<BackendRegistration> registrations,
        string? selectedCheck, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var providers = new List<BackendRegistration>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var provider in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentException.ThrowIfNullOrWhiteSpace(provider.Name);
            if (provider.Name.Length > 64 || provider.Name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-')))
                throw new ArgumentException("Provider names must be bounded artifact labels, not paths or diagnostic text.", nameof(registrations));
            ArgumentNullException.ThrowIfNull(provider.Factory);
            ArgumentNullException.ThrowIfNull(provider.CreateLocation);
            if (providers.Count == 64 || !names.Add(provider.Name))
                throw new ArgumentException("At most 64 uniquely named providers may be registered.", nameof(registrations));
            providers.Add(provider);
        }
        var results = new List<ConformanceResult>();
        var capabilities = new List<BackendCapabilityEvidence>();
        if (providers.Count == 0)
            results.Add(new("registry", "real-provider-registration", "Blocked", "No real backend is registered."));
        foreach (var provider in providers)
        {
            await RunCase("atomic-snapshot-range-replay", BackendContractCases.AtomicSnapshotAsync);
            await RunCase("orderly-reopen-receipts", BackendContractCases.ReopenAsync);
            await RunCase("pre-admission-cancellation-invalid-handles-disposal", BackendContractCases.LifetimeAsync);
            await RunDriven("exact-and-over-resource-quotas", BackendQuotaCases.RunAsync);
            await RunDriven("concurrent-publication-and-disposal", BackendPublicationCases.RunAsync);
            await RunDriven("snapshot-expiry", BackendExpiryCases.RunAsync);
            await RunDriven("real-process-exit-recovery", BackendRecoveryCases.ProcessExitAsync);
            await RunDriven("injected-io-outcomes", BackendRecoveryCases.IoFailureAsync);
            await RunDriven("optional-capability-proof", async (registration, token) =>
            {
                var descriptor = await BackendCapabilityCases.InspectAsync(registration, token);
                capabilities.Add(new(registration.Name, descriptor));
                BackendCapabilityCases.Verify(descriptor);
            });

            async Task RunDriven(string name, Func<BackendRegistration, CancellationToken, Task> run)
            {
                if (selectedCheck is not null && selectedCheck != name) return;
                cancellationToken.ThrowIfCancellationRequested();
                if (provider.Driver is null)
                {
                    results.Add(new(provider.Name, name, "Blocked", "Required host test driver is absent; no guarantee is certified."));
                    return;
                }
                try
                {
                    await run(provider, cancellationToken);
                    results.Add(new(provider.Name, name, "Passed", "Independent check passed for this registration only."));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) { results.Add(new(provider.Name, name, "Failed", exception.GetType().Name)); }
            }

            async Task RunCase(string name, Func<IBackendFactory, ByteString, CancellationToken, Task> run)
            {
                if (selectedCheck is not null && selectedCheck != name) return;
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await using (var location = await provider.CreateLocation(cancellationToken))
                        await run(provider.Factory, location.Location, cancellationToken);
                    results.Add(new(provider.Name, name, "Passed", "Independent check passed; see outstanding obligations."));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    // Do not export provider messages, locations, credentials or stack traces into artifacts.
                    results.Add(new(provider.Name, name, "Failed", exception.GetType().Name));
                }
            }
        }
        return new(results, capabilities, complete: selectedCheck is null);
    }
}
