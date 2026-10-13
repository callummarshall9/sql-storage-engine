// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Dependencies.SqlTypeDocuments;

namespace sql_storage_engine.Brokers.SqlTypeDocuments;
internal sealed class SqlTypeDocumentBroker : ISqlTypeDocumentBroker
{
    public void CheckSchemas(string[] definitions)
    {
        using var dependency = new SqlTypeDocumentDependency();
        dependency.CheckSchemas(definitions: definitions);
    }

    public void CheckXml(string value, bool isDocument, string[]? definitions)
    {
        using var dependency = new SqlTypeDocumentDependency();
        dependency.CheckXml(value: value, isDocument: isDocument, definitions: definitions);
    }

    public int[] ReadJsonObservations(string value, int maximumDepth, int maximumUniqueKeys, int maximumCollectionItems, int maximumKeyBytes, int maximumStringBytes)
    {
        using var dependency = new SqlTypeDocumentDependency();
        return dependency.ReadJsonObservations(value: value, maximumDepth: maximumDepth, maximumUniqueKeys: maximumUniqueKeys, maximumCollectionItems: maximumCollectionItems, maximumKeyBytes: maximumKeyBytes, maximumStringBytes: maximumStringBytes);
    }

    public float[] ReadJsonVector(string value)
    {
        using var dependency = new SqlTypeDocumentDependency();
        return dependency.ReadJsonVector(value: value);
    }

    public string WriteJsonVector(float[] values)
    {
        using var dependency = new SqlTypeDocumentDependency();
        return dependency.WriteJsonVector(values: values);
    }

    public byte[] ReadUtf8Bytes(string value)
    {
        using var dependency = new SqlTypeDocumentDependency();
        return dependency.ReadUtf8Bytes(value: value);
    }
}