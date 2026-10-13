// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.RowStorage;
internal interface IRowStorageContextCoordinationService
{
    RowEncodingContext CheckRowEncodingContextMetadata(RowEncodingContext rowEncodingContext);

    ValueTask<RowEncodingContext> WriteRowEncodingContextOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken);

    ValueTask<RowEncodingContext> ReadRowEncodingContextOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken);

    ValueTask FreeRowEncodingContextOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken);

    RowEncodingContext ProcessRowEncodingContextGraph(RowEncodingContext rowEncodingContext);
}