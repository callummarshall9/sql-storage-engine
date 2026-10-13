// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Dependencies.CatalogIndexDocuments;

namespace sql_storage_engine.Brokers.CatalogIndexDocuments;
internal sealed class CatalogIndexDocumentBroker : ICatalogIndexDocumentBroker
{
    private readonly CatalogIndexDocumentDependency dependency = new();
    public (int Kind, string Raw, string? Text)[] ReadPathValues(string json, string path) =>
        dependency.ReadPathValues(json: json, path: path);

    public (string Path, int Kind, string Raw, string? Text)[] ReadDescendants(string json, string path, string normalizedPath) =>
        dependency.ReadDescendants(json: json, path: path, normalizedPath: normalizedPath);

    public string NormalizePath(string path) =>
        dependency.NormalizePath(path: path);

    public byte[] ReadUtf8Bytes(string value) =>
        dependency.ReadUtf8Bytes(value: value);

    public string ReadUtf8String(byte[] value) =>
        dependency.ReadUtf8String(value: value);
}