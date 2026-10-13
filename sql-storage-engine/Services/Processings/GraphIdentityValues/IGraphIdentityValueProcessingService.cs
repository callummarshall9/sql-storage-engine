// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Services.Processings.GraphIdentityValues;
internal interface IGraphIdentityValueProcessingService
{
    GraphIdentityValue PrepareGraphIdentityValue(GraphIdentityValue graphIdentityValue);

    GraphIdentityValue EncodeGraphIdentityValue(GraphIdentityValue graphIdentityValue);

    GraphIdentityValue DecodeGraphIdentityValue(GraphIdentityValue graphIdentityValue);

    GraphIdentityValue FormatGraphIdentityValue(GraphIdentityValue graphIdentityValue);
}