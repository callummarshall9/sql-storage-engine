// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogIndexDocuments;
internal interface ICatalogIndexDocumentService
{
    CatalogIndexDocumentContext ProcessCatalogIndexDocumentContext(CatalogIndexDocumentContext catalogIndexDocumentContext);
}