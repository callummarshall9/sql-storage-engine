// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Services.Processings.GraphRowProjections;
internal interface IGraphRowProjectionProcessingService
{
    GraphRowContext ProcessGraphRowContext(GraphRowContext graphRowContext);
}