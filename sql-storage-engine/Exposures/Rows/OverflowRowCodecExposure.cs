// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Exposures.Overflow;
using sql_storage_engine.Services.Managements.RowCodecs;

namespace sql_storage_engine.Exposures.Rows;
public sealed class OverflowRowCodecExposure : ICompositionExposure, IRowCodecExposure
{
    private readonly RowCodecContextManagementService service;
    public OverflowRowCodecExposure(IOverflowChainExposure overflowManager, int inlineThreshold)
    {
        service = RowCodecContextManagementService.CreateRowCodecContextManagementService(rowEncodingConfiguration: new RowEncodingConfiguration { OverflowService = overflowManager.ReadOverflowChainBinding()
            .Service, UseOverflow = true, InlineThreshold = inlineThreshold });
    }

    public RowCodecBinding ReadRowCodecBinding() =>
        new RowCodecBinding
    {
        Service = service
    };

    public int ReadInlineThreshold() =>
        service.ReadRowEncodingContext(rowEncodingContext: new RowEncodingContext())
            .InlineThreshold;

    public async ValueTask<RowEncodingResult> EncodeAsync(Row row, TableDefinition tableDefinition, CancellationToken cancellationToken = default) =>
        (await service.ProcessRowEncodingContextEncodeAsync(rowEncodingContext: new RowEncodingContext { Row = row, Table = tableDefinition }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EncodingResult!;

    public async ValueTask<Row> DecodeAsync(ReadOnlyMemory<byte> source, TableDefinition tableDefinition, CancellationToken cancellationToken = default) =>
        (await service.ProcessRowEncodingContextDecodeAsync(rowEncodingContext: new RowEncodingContext { Source = source, Table = tableDefinition }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Row!;

    public async ValueTask<RowEncodingResult> ApplyUpdateAsync(ReadOnlyMemory<byte> currentRow, RowUpdate rowUpdate, TableDefinition tableDefinition, CancellationToken cancellationToken = default) =>
        (await service.ProcessRowEncodingContextUpdateAsync(rowEncodingContext: new RowEncodingContext { Source = currentRow, Update = rowUpdate, Table = tableDefinition }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EncodingResult!;

    public RowValueStorage GetStorage(ReadOnlySpan<byte> row, TableDefinition tableDefinition, ColumnId columnId) =>
        service.ReadRowEncodingContextStorage(rowEncodingContext: new RowEncodingContext { Source = row.ToArray(), Table = tableDefinition, ColumnId = columnId })
            .ValueStorage;

    public IReadOnlyDictionary<ColumnId, OverflowReference> GetOverflowReferences(ReadOnlySpan<byte> row, TableDefinition tableDefinition) =>
        service.ReadRowEncodingContextReferences(rowEncodingContext: new RowEncodingContext { Source = row.ToArray(), Table = tableDefinition })
            .ReferenceResults!;
}