// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal interface ISqlCollationCatalogTextEncodingProcessingService
{
    CatalogTextEncodingContext EncodeCatalogTextEncodingContext(CatalogTextEncodingContext catalogTextEncodingContext);

    CatalogTextEncodingContext DecodeCatalogTextEncodingContext(CatalogTextEncodingContext catalogTextEncodingContext);
}