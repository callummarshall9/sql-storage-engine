// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.CompatibilityFixtures;
internal interface ICompatibilityFixtureService
{
    CompatibilityContext DecodeCompatibilityContext(CompatibilityContext compatibilityContext);
}