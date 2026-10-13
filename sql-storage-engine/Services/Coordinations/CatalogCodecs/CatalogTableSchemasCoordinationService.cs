// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogCodecs;
internal sealed partial class CatalogTableSchemasCoordinationService : ICatalogTableSchemasCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogColumnAdmissionOrchestrationService catalogColumnAdmission;
    private readonly sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogTableShapesOrchestrationService catalogTableShapes;
    private readonly sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogTableTypeShapesOrchestrationService catalogTableTypeShapes;
    public CatalogTableSchemasCoordinationService(sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogColumnAdmissionOrchestrationService catalogColumnAdmission, sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogTableShapesOrchestrationService catalogTableShapes, sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogTableTypeShapesOrchestrationService catalogTableTypeShapes)
    {
        this.catalogColumnAdmission = catalogColumnAdmission;
        this.catalogTableShapes = catalogTableShapes;
        this.catalogTableTypeShapes = catalogTableTypeShapes;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 21:
                catalogCodecContext.CatalogColumn = PrepareColumn(catalogColumn: catalogCodecContext.CatalogColumn!);
                catalogCodecContext.PrepareCatalogColumnResult = catalogCodecContext.CatalogColumn;
                return catalogCodecContext;
            case 12:
                var table = catalogCodecContext.CatalogTable!;
                ArgumentNullException.ThrowIfNull(argument: table);
                ArgumentNullException.ThrowIfNull(argument: table.ColumnsSource);
                table = table with
                {
                    ColumnsSource = table.ColumnsSource.Select(selector: column => PrepareColumn(catalogColumn: column))
            .ToArray()
                };
                table = table with
                {
                    CheckConstraintsSource = PrepareChecks(checks: table.CheckConstraintsSource)
                };
                if (table.SystemVersioningSource is not null)
                {
                    table = table with
                    {
                        SystemVersioningSource = catalogTableTypeShapes.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 10, CatalogSystemVersioning = table.SystemVersioningSource })
            .PrepareCatalogSystemVersioningResult
                    };
                }

                if (table.GraphSource is not null)
                {
                    table = table with
                    {
                        GraphSource = catalogTableShapes.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 11, CatalogGraphTable = table.GraphSource })
            .PrepareCatalogGraphTableResult
                    };
                }

                catalogCodecContext.CatalogTable = table;
                return catalogTableShapes.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            case 17:
                var tableType = catalogCodecContext.CatalogTableType!;
                ArgumentNullException.ThrowIfNull(argument: tableType);
                ArgumentNullException.ThrowIfNull(argument: tableType.ColumnsSource);
                tableType = tableType with
                {
                    ColumnsSource = tableType.ColumnsSource.Select(selector: column => PrepareColumn(catalogColumn: column))
            .ToArray()
                };
                tableType = tableType with
                {
                    CheckConstraintsSource = PrepareChecks(checks: tableType.CheckConstraintsSource)
                };
                tableType = tableType with
                {
                    IndexesSource = tableType.IndexesSource?.Select(selector: index => catalogTableTypeShapes.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 16, CatalogTableTypeIndex = index })
            .PrepareCatalogTableTypeIndexResult!)
            .ToArray()
                };
                catalogCodecContext.CatalogTableType = tableType;
                return catalogTableTypeShapes.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            case 19:
            case 20:
                return catalogColumnAdmission.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            case 9:
            case 11:
                return catalogTableShapes.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            case 10:
            case 16:
                return catalogTableTypeShapes.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });

    private CatalogColumn PrepareColumn(CatalogColumn catalogColumn)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogColumn);

        if (catalogColumn.IdentitySource is not null)
        {
            catalogColumn = catalogColumn with
            {
                IdentitySource = catalogColumnAdmission.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 19, CatalogIdentity = catalogColumn.IdentitySource })
            .PrepareCatalogIdentityResult
            };
        }

        if (catalogColumn.EncryptionSource is not null)
        {
            catalogColumn = catalogColumn with
            {
                EncryptionSource = catalogColumnAdmission.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 20, CatalogColumnEncryption = catalogColumn.EncryptionSource })
            .PrepareCatalogColumnEncryptionResult
            };
        }

        return catalogColumnAdmission.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 21, CatalogColumn = catalogColumn })
            .PrepareCatalogColumnResult!;
    }

    private CatalogCheckConstraint[] PrepareChecks(IEnumerable<CatalogCheckConstraint>? checks) =>
        checks?.Select(selector: check => catalogTableShapes.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 9, CatalogCheckConstraint = check })
            .PrepareCatalogCheckConstraintResult!)
            .ToArray() ?? [];

    internal static CatalogTableSchemasCoordinationService CreateCatalogTableSchemasCoordinationService() =>
        new CatalogTableSchemasCoordinationService(catalogColumnAdmission: sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogColumnAdmissionOrchestrationService.CreateCatalogColumnAdmissionOrchestrationService(), catalogTableShapes: sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogTableShapesOrchestrationService.CreateCatalogTableShapesOrchestrationService(), catalogTableTypeShapes: sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogTableTypeShapesOrchestrationService.CreateCatalogTableTypeShapesOrchestrationService());
}