using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public sealed record PhysicalPageChange(PageId PageId, PageType PageType,
    ReadOnlyMemory<byte> BeforeImage, ReadOnlyMemory<byte> AfterImage);
