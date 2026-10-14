// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Foundations.DatabaseIds;
internal sealed partial class DatabaseIdService : IDatabaseIdService
{
    public DatabaseId CreateDatabaseId() =>
        TryCatch(operation: () =>
    {
        var databaseId = new DatabaseId
        {
            Value = Guid.NewGuid()
        };

        ValidateDatabaseIdOnCreate(databaseId: databaseId);
        return databaseId;
    });
}