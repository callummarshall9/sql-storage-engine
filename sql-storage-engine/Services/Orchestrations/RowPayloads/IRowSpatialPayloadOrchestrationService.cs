// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.RowPayloads;
internal interface IRowSpatialPayloadOrchestrationService
{
    RowPayloadContext WriteRowPayloadContextSpatial(RowPayloadContext rowPayloadContext);

    RowPayloadContext ReadRowPayloadContextSpatial(RowPayloadContext rowPayloadContext);

    RowPayloadContext ReadRowPayloadContextBounds(RowPayloadContext rowPayloadContext);
}