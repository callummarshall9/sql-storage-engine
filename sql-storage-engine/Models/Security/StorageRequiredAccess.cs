// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Security;

namespace sql_storage_engine.Models.Security;
public sealed record StorageRequiredAccess
{
    public StorageObjectBinding Object { get => _object!; init => _object = value; }

    private StorageObjectBinding? _object;
    public StoragePermissionAction Action { get; init; }
}