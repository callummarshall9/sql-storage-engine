// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.RowFormats;
internal interface IRowFormatContextCoordinationService
{
    RowEncodingContext ProcessRowEncodingContextEncode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextDecode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextDecodeSelected(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextHeader(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextVariables(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextWidth(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextIsVariable(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextVariableEncode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextLogicalEncode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextVariableDecode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextFixedDecode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextSchemaHash(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextSpatialEncode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextSpatialBounds(RowEncodingContext rowEncodingContext);

    RowEncodingContext ReadRowEncodingContextColumnSet(RowEncodingContext rowEncodingContext);

    RowEncodingContext WriteRowEncodingContextColumnSet(RowEncodingContext rowEncodingContext);
}