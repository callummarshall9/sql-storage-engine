// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogBuffers;
internal interface ICatalogBufferService
{
    ValueTask<CatalogBufferContext> ProcessCatalogBufferContextAsync(CatalogBufferContext catalogBufferContext, CancellationToken cancellationToken);
}