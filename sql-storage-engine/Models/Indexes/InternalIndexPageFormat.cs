// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Indexes;
public static class InternalIndexPageFormat
{
    public const int HeaderLength = 64;
    public const int SlotLength = 16;
    public const int ParentOffset = 32;
    public const int SeparatorCountOffset = 41;
    public const int ChildCountOffset = 43;
    public const int SlotDirectoryEndOffset = 45;
    public const int KeyDataStartOffset = 49;
    public const int FirstChildOffset = 56;
}