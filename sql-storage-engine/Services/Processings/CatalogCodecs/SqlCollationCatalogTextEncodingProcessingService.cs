// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class SqlCollationCatalogTextEncodingProcessingService : ISqlCollationCatalogTextEncodingProcessingService
{
    private readonly sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService collations;
    public SqlCollationCatalogTextEncodingProcessingService(sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService collations)
    {
        this.collations = collations;
    }

    public CatalogTextEncodingContext EncodeCatalogTextEncodingContext(CatalogTextEncodingContext catalogTextEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateText(catalogTextEncodingContext: catalogTextEncodingContext);
        var size = collations.CountSqlCollationUtf8(value: catalogTextEncodingContext.Text!);
        var bytes = new byte[size];
        collations.WriteSqlCollationUtf8(destination: bytes, value: catalogTextEncodingContext.Text!);
        catalogTextEncodingContext.Bytes = bytes;
        return catalogTextEncodingContext;
    });

    public CatalogTextEncodingContext DecodeCatalogTextEncodingContext(CatalogTextEncodingContext catalogTextEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateBytes(catalogTextEncodingContext: catalogTextEncodingContext);
        catalogTextEncodingContext.Text = collations.ReadSqlCollationUtf8(value: catalogTextEncodingContext.Bytes!);
        return catalogTextEncodingContext;
    });

    internal static SqlCollationCatalogTextEncodingProcessingService CreateSqlCollationCatalogTextEncodingProcessingService() =>
        new SqlCollationCatalogTextEncodingProcessingService(collations: new sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService());
}