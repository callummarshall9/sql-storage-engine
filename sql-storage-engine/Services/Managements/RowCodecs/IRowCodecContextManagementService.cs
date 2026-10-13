// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.RowCodecs;
internal interface IRowCodecContextManagementService
{
    RowEncodingContext ReadRowEncodingContext(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextEncode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextDecode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextSelected(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextLogicalEncode(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextUpdate(RowEncodingContext rowEncodingContext);

    RowEncodingContext ReadRowEncodingContextStorage(RowEncodingContext rowEncodingContext);

    RowEncodingContext ReadRowEncodingContextReferences(RowEncodingContext rowEncodingContext);

    ValueTask<RowEncodingContext> ProcessRowEncodingContextEncodeAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken);

    ValueTask<RowEncodingContext> ProcessRowEncodingContextDecodeAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken);

    ValueTask<RowEncodingContext> ProcessRowEncodingContextUpdateAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken);

    RowEncodingContext ProcessRowEncodingContextAdmission(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextConversion(RowEncodingContext rowEncodingContext);

    RowEncodingContext ReadRowEncodingContextProjectionType(RowEncodingContext rowEncodingContext);

    ValueTask FreeRowEncodingContextOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken);

    RowEncodingContext ReadRowEncodingContextColumnSet(RowEncodingContext rowEncodingContext);

    RowEncodingContext WriteRowEncodingContextColumnSet(RowEncodingContext rowEncodingContext);

    RowEncodingContext ProcessRowEncodingContextGraph(RowEncodingContext rowEncodingContext);
}