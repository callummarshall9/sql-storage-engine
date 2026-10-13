// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Services.Foundations.SqlTypeDocuments;
internal interface ISqlTypeDocumentService
{
    void CheckSqlTypeXmlSchemas(string[] definitions);

    void CheckSqlTypeXmlText(string value, bool isDocument, string[]? definitions, string constraint, string columnName);

    int[] ReadSqlTypeJsonObservations(string value, string columnName);

    float[] ReadSqlTypeJsonVector(string value);

    string WriteSqlTypeJsonVector(float[] values);

    byte[] ReadSqlTypeUtf8Bytes(string value);
}