// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogSystemVersioningProcessingService
{
    private static void ValidateCatalogSystemVersioning(CatalogSystemVersioning catalogSystemVersioning)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogSystemVersioning);
        var historyTableId = catalogSystemVersioning.HistoryTableId;
        var periodStartColumnId = catalogSystemVersioning.PeriodStartColumnId;
        var periodEndColumnId = catalogSystemVersioning.PeriodEndColumnId;

        if (periodStartColumnId == periodEndColumnId)
        {
            throw new ArgumentException(message: "The temporal period start and end columns must be different.");
        }
    }
}