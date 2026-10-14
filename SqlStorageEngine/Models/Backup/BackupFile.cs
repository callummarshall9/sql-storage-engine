// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Backup;
public sealed record BackupFile
{
    public string Name { get => _name!; init => _name = value; }
    public string Kind { get => _kind!; init => _kind = value; }

    private string? _name;
    private string? _kind;
    public long Size { get; init; }
    public string Sha256 { get => _sha256!; init => _sha256 = value; }

    private string? _sha256;
}