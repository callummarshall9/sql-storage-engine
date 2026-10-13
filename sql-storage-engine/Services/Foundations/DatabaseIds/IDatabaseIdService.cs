// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Foundations.DatabaseIds;
internal interface IDatabaseIdService
{
    DatabaseId CreateDatabaseId();
}