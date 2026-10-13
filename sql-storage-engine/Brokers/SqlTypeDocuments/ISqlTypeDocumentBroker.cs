// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Brokers.SqlTypeDocuments;
internal interface ISqlTypeDocumentBroker
{
    void CheckSchemas(string[] definitions);

    void CheckXml(string value, bool isDocument, string[]? definitions);

    int[] ReadJsonObservations(string value, int maximumDepth, int maximumUniqueKeys, int maximumCollectionItems, int maximumKeyBytes, int maximumStringBytes);

    float[] ReadJsonVector(string value);

    string WriteJsonVector(float[] values);

    byte[] ReadUtf8Bytes(string value);
}