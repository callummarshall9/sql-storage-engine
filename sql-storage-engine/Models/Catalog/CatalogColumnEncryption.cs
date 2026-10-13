// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogColumnEncryption
{
    public string KeyName { get => _keyName!; init => _keyName = value; }

    private string? _keyName;
    public CatalogEncryptionType EncryptionType { get; init; }
    public string Algorithm { get => _algorithm!; init => _algorithm = value; }

    private string? _algorithm;
}