// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.RowPayloads;
internal interface IRowScalarPayloadOrchestrationService
{
    RowPayloadContext ReadRowPayloadContextWidth(RowPayloadContext rowPayloadContext);

    RowPayloadContext WriteRowPayloadContextFixed(RowPayloadContext rowPayloadContext);

    RowPayloadContext ReadRowPayloadContextFixed(RowPayloadContext rowPayloadContext);

    RowPayloadContext WriteRowPayloadContextScalar(RowPayloadContext rowPayloadContext);

    RowPayloadContext ReadRowPayloadContextScalar(RowPayloadContext rowPayloadContext);

    RowPayloadContext EncryptRowPayloadContext(RowPayloadContext rowPayloadContext);

    RowPayloadContext DecryptRowPayloadContext(RowPayloadContext rowPayloadContext);
}