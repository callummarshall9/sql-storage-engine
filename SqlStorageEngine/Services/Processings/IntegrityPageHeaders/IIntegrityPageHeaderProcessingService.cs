// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Processings.IntegrityPageHeaders;
internal interface IIntegrityPageHeaderProcessingService
{
    IntegrityContext CheckIntegrityContext(IntegrityContext integrityContext);
}