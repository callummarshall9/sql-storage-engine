// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Services.Foundations.GraphIdentities;
internal interface IGraphIdentityService
{
    GraphIdentity EncodeGraphIdentity(GraphIdentity graphIdentity);

    GraphIdentity DecodeGraphIdentity(GraphIdentity graphIdentity);
}