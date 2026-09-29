using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

internal static class BackendCapabilityCases
{
    internal static async Task<BackendDescriptor> InspectAsync(BackendRegistration provider, CancellationToken token)
    {
        await using var location = await provider.CreateLocation(token);
        await using var store = await BackendAdmission.OpenAsync(provider.Factory, new(location.Location, BackendOpenMode.CreateNew), token);
        return store.Descriptor;
    }

    internal static void Verify(BackendDescriptor descriptor)
    {
        const BackendGuarantees proved = BackendGuarantees.ConsistentSnapshots |
            BackendGuarantees.ConditionalAtomicBatches | BackendGuarantees.DurableReceipts;
        BackendOracle.Require((descriptor.Guarantees & ~proved) == 0, "An unknown guarantee has no oracle.");
        // No optional capability has an independent suite yet. Unknown advertisements cannot silently pass.
        BackendOracle.Require(descriptor.OptionalCapabilities.Count == 0, "Advertised optional capability has no proof.");
    }
}
