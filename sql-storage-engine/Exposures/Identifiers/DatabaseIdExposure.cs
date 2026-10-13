// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Foundations.DatabaseIds;

namespace sql_storage_engine.Exposures.Identifiers;
public static class DatabaseIdExposure
{
    private static readonly IDatabaseIdService service = new DatabaseIdService();
    public static DatabaseId CreateDatabaseId() =>
        service.CreateDatabaseId();
}