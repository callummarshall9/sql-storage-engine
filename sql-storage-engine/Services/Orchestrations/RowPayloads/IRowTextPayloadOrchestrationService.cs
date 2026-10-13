// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.RowPayloads;
internal interface IRowTextPayloadOrchestrationService
{
    RowPayloadContext WriteRowPayloadContextText(RowPayloadContext rowPayloadContext);

    RowPayloadContext ReadRowPayloadContextText(RowPayloadContext rowPayloadContext);

    RowPayloadContext WriteRowPayloadContextUtf8(RowPayloadContext rowPayloadContext);

    RowPayloadContext ReadRowPayloadContextUtf8(RowPayloadContext rowPayloadContext);

    RowPayloadContext ReadRowPayloadContextColumnSet(RowPayloadContext rowPayloadContext);

    RowPayloadContext WriteRowPayloadContextColumnSet(RowPayloadContext rowPayloadContext);
}