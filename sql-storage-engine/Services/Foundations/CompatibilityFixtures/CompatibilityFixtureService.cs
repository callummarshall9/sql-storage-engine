// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Brokers.SecuritySerialization;

namespace sql_storage_engine.Services.Foundations.CompatibilityFixtures;
internal sealed partial class CompatibilityFixtureService(ISecuritySerializationBroker serialization) : ICompatibilityFixtureService
{
    public CompatibilityContext DecodeCompatibilityContext(CompatibilityContext compatibilityContext) =>
        TryCatch(operation: () =>
    {
        ValidateCompatibilityContext(compatibilityContext: compatibilityContext);
        var fixture = serialization.Deserialize<CompatibilityFixture>(bytes: compatibilityContext.Bytes!) ?? throw new StorageFormatException(message: "Compatibility fixture is empty.");

        if (fixture.FormatVersion != DatabaseHeaderFormat.CurrentVersion)
        {
            throw new UnsupportedDatabaseVersionException(version: fixture.FormatVersion);
        }

        if (!Enum.IsDefined(value: (PageSize)fixture.PageSize) || fixture.Timeline == 0 || fixture.ExpectedRows is null || fixture.WalRecords is null)
        {
            throw new StorageFormatException(message: "Compatibility fixture metadata is invalid.");
        }

        return compatibilityContext with
        {
            Fixture = fixture
        };
    });
}