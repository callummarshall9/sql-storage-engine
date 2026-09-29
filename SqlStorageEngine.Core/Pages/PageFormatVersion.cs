using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>Version of a page's binary layout.</summary>
public readonly record struct PageFormatVersion(ushort Value)
{
    public static PageFormatVersion Current => new(1);
}
