// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogCodecContext
{
    public int Operation { get; set; }
    public SecurityStatePayload? SecurityStatePayload { get; set; }
    public byte[]? EncodeSecurityStatePayloadResult { get; set; }
    public SecurityStatePayload? DecodeSecurityStatePayloadResult { get; set; }
    public SqlXmlSchemaCollection? SqlXmlSchemaCollection { get; set; }
    public SqlXmlSchemaCollection? PrepareSqlXmlSchemaCollectionResult { get; set; }
    public IReadOnlyList<string>? ReadSqlXmlSchemaCollectionDefinitionsResult { get; set; }
    public CatalogAssembly? CatalogAssembly { get; set; }
    public byte[]? Image { get; set; }
    public CatalogAssembly? PrepareCatalogAssemblyResult { get; set; }
    public byte[]? ReadCatalogAssemblyImageResult { get; set; }
    public CatalogFullTextIndexOptions? CatalogFullTextIndexOptions { get; set; }
    public IReadOnlyList<string>? EncodeCatalogFullTextIndexOptionsResult { get; set; }
    public IReadOnlyList<string>? Values { get; set; }
    public CatalogFullTextIndexOptions? DecodeCatalogFullTextIndexOptionsResult { get; set; }
    public CatalogCheckConstraint? CatalogCheckConstraint { get; set; }
    public CatalogCheckConstraint? PrepareCatalogCheckConstraintResult { get; set; }
    public CatalogSystemVersioning? CatalogSystemVersioning { get; set; }
    public CatalogSystemVersioning? PrepareCatalogSystemVersioningResult { get; set; }
    public CatalogGraphTable? CatalogGraphTable { get; set; }
    public CatalogGraphTable? PrepareCatalogGraphTableResult { get; set; }
    public CatalogTable? CatalogTable { get; set; }
    public CatalogTable? PrepareCatalogTableResult { get; set; }
    public CatalogIndexedColumn? CatalogIndexedColumn { get; set; }
    public CatalogIndexedColumn? PrepareCatalogIndexedColumnResult { get; set; }
    public SqlTypeContext? SqlTypeContext { get; set; }
    public SqlTypeContext? ProcessSqlTypeContextRestore73Result { get; set; }
    public SqlTypeContext? CreateSqlTypeContextCatalogScalarTypeResult { get; set; }
    public CatalogTableTypeIndex? CatalogTableTypeIndex { get; set; }
    public CatalogTableTypeIndex? PrepareCatalogTableTypeIndexResult { get; set; }
    public CatalogTableType? CatalogTableType { get; set; }
    public CatalogTableType? PrepareCatalogTableTypeResult { get; set; }
    public CatalogDefinition? CatalogDefinition { get; set; }
    public CatalogDefinition? PrepareCatalogDefinitionResult { get; set; }
    public CatalogIdentity? CatalogIdentity { get; set; }
    public CatalogIdentity? PrepareCatalogIdentityResult { get; set; }
    public CatalogColumnEncryption? CatalogColumnEncryption { get; set; }
    public CatalogColumnEncryption? PrepareCatalogColumnEncryptionResult { get; set; }
    public CatalogColumn? CatalogColumn { get; set; }
    public CatalogColumn? PrepareCatalogColumnResult { get; set; }
    public CatalogSpecializedIndexOptions? CatalogSpecializedIndexOptions { get; set; }
    public CatalogSpecializedIndexOptions? PrepareCatalogSpecializedIndexOptionsResult { get; set; }
    public CatalogFullTextIndexOptions? PrepareCatalogFullTextIndexOptionsResult { get; set; }
    public CatalogIndex? CatalogIndex { get; set; }
    public CatalogIndex? PrepareCatalogIndexResult { get; set; }
    public CatalogTextEncodingContext? CatalogTextEncodingContext { get; set; }
    public CatalogTextEncodingContext? EncodeCatalogTextEncodingContextResult { get; set; }
    public CatalogTextEncodingContext? DecodeCatalogTextEncodingContextResult { get; set; }
    public byte[]? Payload { get; set; }
}