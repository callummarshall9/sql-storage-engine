using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>Algorithm used to protect a complete page from corruption.</summary>
public enum PageChecksumAlgorithm : ushort
{
    None = 0,
    Crc32 = 1
}
