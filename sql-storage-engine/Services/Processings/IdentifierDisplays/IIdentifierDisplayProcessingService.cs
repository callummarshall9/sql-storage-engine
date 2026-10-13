// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Processings.IdentifierDisplays;
internal interface IIdentifierDisplayProcessingService
{
    string FormatIdentifierDisplay(IdentifierDisplay identifierDisplay);
}