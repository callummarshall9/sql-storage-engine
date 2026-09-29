using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Overflow;

public readonly record struct OverflowPageHeader(PageId? NextPageId, uint UsedLength);
