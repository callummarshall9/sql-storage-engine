// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Brokers.CatalogIndexDocuments;
internal interface ICatalogIndexDocumentBroker
{
    (int Kind, string Raw, string? Text)[] ReadPathValues(string json, string path);

    (string Path, int Kind, string Raw, string? Text)[] ReadDescendants(string json, string path, string normalizedPath);

    string NormalizePath(string path);

    byte[] ReadUtf8Bytes(string value);

    string ReadUtf8String(byte[] value);
}