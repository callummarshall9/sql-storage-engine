// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Heap;
public static class HeapPageFormat
{
    public const int HeaderLength = 64;
    public const int SlotEntryLength = 16;
    public const int PreviousPageOffset = 32;
    public const int NextPageOffset = 41;
    public const int SlotCountOffset = 50;
    public const int SlotDirectoryEndOffset = 52;
    public const int RowDataStartOffset = 56;
}