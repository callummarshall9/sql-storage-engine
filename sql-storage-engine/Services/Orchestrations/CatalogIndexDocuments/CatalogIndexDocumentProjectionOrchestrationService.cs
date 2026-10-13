// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogIndexDocuments;
internal sealed partial class CatalogIndexDocumentProjectionOrchestrationService : ICatalogIndexDocumentProjectionOrchestrationService
{
    private readonly sql_storage_engine.Services.Foundations.CatalogIndexDocuments.CatalogIndexDocumentService documents;
    private readonly sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService collations;
    private readonly sql_storage_engine.Services.Foundations.ColumnEncryption.CatalogColumnEncryptionService encryption;
    public CatalogIndexDocumentProjectionOrchestrationService(sql_storage_engine.Services.Foundations.CatalogIndexDocuments.CatalogIndexDocumentService documents, sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService collations, sql_storage_engine.Services.Foundations.ColumnEncryption.CatalogColumnEncryptionService encryption)
    {
        this.documents = documents;
        this.collations = collations;
        this.encryption = encryption;
    }

    public CatalogIndexProjectionContext ProcessCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndexProjectionContext(catalogIndexProjectionContext: catalogIndexProjectionContext);

        switch (catalogIndexProjectionContext.Operation)
        {
            case 16:
                catalogIndexProjectionContext.NumberResult = collations.CountSqlCollationBytes(sqlCollation: catalogIndexProjectionContext.Collation!, value: catalogIndexProjectionContext.Text!);
                break;
            case 1:
                catalogIndexProjectionContext.Document = documents.ProcessCatalogIndexDocumentContext(catalogIndexDocumentContext: catalogIndexProjectionContext.Document!);
                break;
            case 2:
                catalogIndexProjectionContext.Collation = collations.ParseSqlCollation(name: catalogIndexProjectionContext.Text!);
                break;
            case 3:
                catalogIndexProjectionContext.Collation = collations.ReadDefaultSqlCollation();
                break;
            case 4:
                catalogIndexProjectionContext.Bytes = collations.CalculateSqlCollationSortKey(sqlCollation: catalogIndexProjectionContext.Collation!, value: catalogIndexProjectionContext.Text!, unicode: catalogIndexProjectionContext.Unicode);
                break;
            case 5:
                catalogIndexProjectionContext.Bytes = encryption.EncryptCatalogColumnEncryptionPayload(catalogColumnEncryption: catalogIndexProjectionContext.Encryption!, payload: catalogIndexProjectionContext.Bytes!);
                break;
        }

        return catalogIndexProjectionContext;
    });

    internal static CatalogIndexDocumentProjectionOrchestrationService CreateCatalogIndexDocumentProjectionOrchestrationService() =>
        new CatalogIndexDocumentProjectionOrchestrationService(documents: sql_storage_engine.Services.Foundations.CatalogIndexDocuments.CatalogIndexDocumentService.CreateCatalogIndexDocumentService(), collations: new sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService(), encryption: new sql_storage_engine.Services.Foundations.ColumnEncryption.CatalogColumnEncryptionService());
}