// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Orchestrations.StorageMemory;
using sql_storage_engine.Services.Orchestrations.ObjectCommits;
using sql_storage_engine.Services.Orchestrations.DatabaseHeaders;

namespace sql_storage_engine.Services.Coordinations.ObjectDatabases;
internal interface IObjectDatabaseContextCoordinationService
{
    ValueTask<StorageStrategyContext> ProcessStorageStrategyContextAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken);
}