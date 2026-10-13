// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Processings.IdentifierDisplays;
internal sealed partial class IdentifierDisplayProcessingService : IIdentifierDisplayProcessingService
{
    public string FormatIdentifierDisplay(IdentifierDisplay identifierDisplay) =>
        TryCatch(operation: () =>
    {
        ValidateIdentifierDisplay(identifierDisplay: identifierDisplay);

        return FormattableString.Invariant(formattable: identifierDisplay.Kind switch
        {
            IdentifierDisplayKind.Database => $"database:{identifierDisplay.DatabaseValue:D}",
            IdentifierDisplayKind.Column => $"column:{identifierDisplay.Value}",
            IdentifierDisplayKind.Page => $"page:{identifierDisplay.Value}",
            IdentifierDisplayKind.Table => $"table:{identifierDisplay.Value}",
            IdentifierDisplayKind.Index => $"index:{identifierDisplay.Value}",
            IdentifierDisplayKind.Transaction => $"transaction:{identifierDisplay.Value}",
            IdentifierDisplayKind.LogSequence => $"lsn:{identifierDisplay.Value}",
            IdentifierDisplayKind.Slot => $"slot:{identifierDisplay.Value}",
            IdentifierDisplayKind.Generation => $"generation:{identifierDisplay.Value}",
            IdentifierDisplayKind.Row => $"row:{identifierDisplay.Value}/{identifierDisplay.SlotValue}/{identifierDisplay.GenerationValue}",
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(identifierDisplay.Kind))});
    });
}