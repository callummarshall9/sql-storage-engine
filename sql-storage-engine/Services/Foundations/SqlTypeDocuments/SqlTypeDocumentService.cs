// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Brokers.SqlTypeDocuments;

namespace sql_storage_engine.Services.Foundations.SqlTypeDocuments;
internal sealed partial class SqlTypeDocumentService : ISqlTypeDocumentService
{
    private readonly ISqlTypeDocumentBroker broker = new SqlTypeDocumentBroker();
    public void CheckSqlTypeXmlSchemas(string[] definitions) =>
        TryCatch(operation: () =>
    {
        ValidateSchemas(definitions: definitions);
        broker.CheckSchemas(definitions: definitions);
    }, requirement: "XML schema collection is invalid.", parameterName: nameof(definitions));

    public void CheckSqlTypeXmlText(string value, bool isDocument, string[]? definitions, string constraint, string columnName) =>
        TryCatch(operation: () =>
    {
        ValidateXml(value: value, isDocument: isDocument, definitions: definitions, constraint: constraint, columnName: columnName);
        broker.CheckXml(value: value, isDocument: isDocument, definitions: definitions);
    }, requirement: $"Column '{columnName}' requires {constraint}.", parameterName: nameof(value));

    public int[] ReadSqlTypeJsonObservations(string value, string columnName) =>
        TryCatch(operation: () =>
    {
        ValidateJson(value: value, columnName: columnName);
        return broker.ReadJsonObservations(value: value, maximumDepth: 128, maximumUniqueKeys: 32 * 1024, maximumCollectionItems: ushort.MaxValue, maximumKeyBytes: 7998, maximumStringBytes: 536870911);
    }, requirement: $"Column '{columnName}' requires valid JSON.", parameterName: nameof(value));

    public float[] ReadSqlTypeJsonVector(string value) =>
        TryCatch(operation: () =>
    {
        ValidateReadDocument(value: value);
        return broker.ReadJsonVector(value: value);
    }, requirement: "SQL value document is invalid.", parameterName: "value");

    public string WriteSqlTypeJsonVector(float[] values) =>
        TryCatch(operation: () =>
    {
        ValidateWriteJsonVector(values: values);
        return broker.WriteJsonVector(values: values);
    }, requirement: "SQL value document is invalid.", parameterName: "values");

    public byte[] ReadSqlTypeUtf8Bytes(string value) =>
        TryCatch(operation: () =>
    {
        ValidateReadDocument(value: value);
        return broker.ReadUtf8Bytes(value: value);
    }, requirement: "SQL value document is invalid.", parameterName: "value");
}