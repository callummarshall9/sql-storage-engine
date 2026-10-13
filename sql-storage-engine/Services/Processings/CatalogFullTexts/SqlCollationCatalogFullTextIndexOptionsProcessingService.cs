// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Foundations.SqlCollations;

namespace sql_storage_engine.Services.Processings.CatalogFullTexts;
internal sealed partial class SqlCollationCatalogFullTextIndexOptionsProcessingService(SqlCollationService collations) : ISqlCollationCatalogFullTextIndexOptionsProcessingService
{
    public CatalogFullTextIndexOptions PrepareCatalogFullTextIndexOptions(CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogFullTextIndexOptions(catalogFullTextIndexOptions: catalogFullTextIndexOptions);
        return catalogFullTextIndexOptions;
    });

    public IReadOnlyList<string> EncodeCatalogFullTextIndexOptions(CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        TryCatch<IReadOnlyList<string>>(operation: () =>
    {
        ValidateCatalogFullTextIndexOptions(catalogFullTextIndexOptions: catalogFullTextIndexOptions);
        return[catalogFullTextIndexOptions.Collation, catalogFullTextIndexOptions.Language, catalogFullTextIndexOptions.Tokenizer, catalogFullTextIndexOptions.TokenizerVersion.ToString(provider: CultureInfo.InvariantCulture), catalogFullTextIndexOptions.Stoplist, catalogFullTextIndexOptions.StoplistVersion.ToString(provider: CultureInfo.InvariantCulture), ((byte)catalogFullTextIndexOptions.Consistency).ToString(provider: CultureInfo.InvariantCulture), catalogFullTextIndexOptions.MaximumTokensPerDocument.ToString(provider: CultureInfo.InvariantCulture), catalogFullTextIndexOptions.MaximumTokenLength.ToString(provider: CultureInfo.InvariantCulture)];
    });

    public CatalogFullTextIndexOptions DecodeCatalogFullTextIndexOptions(IReadOnlyList<string> values) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogFullTextFields(values: values);

        try
        {
            var result = new CatalogFullTextIndexOptions
            {
                Collation = values[0],
                Language = values[1],
                Tokenizer = values[2],
                TokenizerVersion = ushort.Parse(s: values[3], style: NumberStyles.None, provider: CultureInfo.InvariantCulture),
                Stoplist = values[4],
                StoplistVersion = ushort.Parse(s: values[5], style: NumberStyles.None, provider: CultureInfo.InvariantCulture),
                Consistency = (CatalogFullTextConsistency)byte.Parse(s: values[6], style: NumberStyles.None, provider: CultureInfo.InvariantCulture),
                MaximumTokensPerDocument = int.Parse(s: values[7], style: NumberStyles.None, provider: CultureInfo.InvariantCulture),
                MaximumTokenLength = int.Parse(s: values[8], style: NumberStyles.None, provider: CultureInfo.InvariantCulture)
            };

            ValidateCatalogFullTextIndexOptions(catalogFullTextIndexOptions: result);
            return result;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Full-text index metadata contains an invalid numeric field.", nameof(values), exception);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException("Full-text index metadata contains an out-of-range numeric field.", nameof(values), exception);
        }
    });

    internal static SqlCollationCatalogFullTextIndexOptionsProcessingService CreateSqlCollationCatalogFullTextIndexOptionsProcessingService() =>
        new SqlCollationCatalogFullTextIndexOptionsProcessingService(collations: new sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService());
}