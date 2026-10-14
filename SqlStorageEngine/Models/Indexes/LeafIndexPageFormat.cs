// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Indexes;
public static class LeafIndexPageFormat
{
    public const int HeaderLength = 72;
    public const int SlotLength = 24;
    public const int ParentOffset = 32;
    public const int PreviousOffset = 41;
    public const int NextOffset = 50;
    public const int EntryCountOffset = 59;
    public const int SlotDirectoryEndOffset = 61;
    public const int KeyDataStartOffset = 65;
}