// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Pages;

namespace sql_storage_engine.Models.Pages;
internal sealed class PageDatabaseRuntime
{
    public PageFileSession? Session;
    public DatabaseHeader? Header;
    public DatabaseOpenMode OpenMode;
    public bool Disposed;
    public long? MaximumAllocatedBytes;
}