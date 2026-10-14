// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.IdentifierDisplays;

namespace sql_storage_engine.Exposures.Identifiers;
/// <summary>Renders identifiers using stable, culture-independent diagnostic notation.</summary>
public static class IdentifierDisplayExposure
{
    private static readonly IIdentifierDisplayProcessingService service = new IdentifierDisplayProcessingService();
    public static string FormatDatabaseId(DatabaseId databaseId) =>
        service.FormatIdentifierDisplay(identifierDisplay: new IdentifierDisplay { Kind = IdentifierDisplayKind.Database, DatabaseValue = databaseId.Value });

    public static string FormatPageId(PageId pageId) =>
        service.FormatIdentifierDisplay(identifierDisplay: new IdentifierDisplay { Kind = IdentifierDisplayKind.Page, Value = pageId.Value });

    public static string FormatTableId(TableId tableId) =>
        service.FormatIdentifierDisplay(identifierDisplay: new IdentifierDisplay { Kind = IdentifierDisplayKind.Table, Value = tableId.Value });

    public static string FormatIndexId(IndexId indexId) =>
        service.FormatIdentifierDisplay(identifierDisplay: new IdentifierDisplay { Kind = IdentifierDisplayKind.Index, Value = indexId.Value });

    public static string FormatTransactionId(TransactionId transactionId) =>
        service.FormatIdentifierDisplay(identifierDisplay: new IdentifierDisplay { Kind = IdentifierDisplayKind.Transaction, Value = transactionId.Value });

    public static string FormatLogSequenceNumber(LogSequenceNumber logSequenceNumber) =>
        service.FormatIdentifierDisplay(identifierDisplay: new IdentifierDisplay { Kind = IdentifierDisplayKind.LogSequence, Value = logSequenceNumber.Value });

    public static string FormatSlotId(SlotId slotId) =>
        service.FormatIdentifierDisplay(identifierDisplay: new IdentifierDisplay { Kind = IdentifierDisplayKind.Slot, Value = slotId.Value });

    public static string FormatSlotGeneration(SlotGeneration slotGeneration) =>
        service.FormatIdentifierDisplay(identifierDisplay: new IdentifierDisplay { Kind = IdentifierDisplayKind.Generation, Value = slotGeneration.Value });

    public static string FormatRowId(RowId rowId) =>
        service.FormatIdentifierDisplay(identifierDisplay: new IdentifierDisplay { Kind = IdentifierDisplayKind.Row, Value = rowId.PageId.Value, SlotValue = rowId.SlotId.Value, GenerationValue = rowId.Generation.Value });

}