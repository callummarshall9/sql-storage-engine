// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Managements.CatalogCodecs;

namespace sql_storage_engine.Exposures.Catalog;
public sealed class CatalogCodecExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private CatalogCodecExposure()
    {
    }

    private static readonly ICatalogCodecManagementService service = CatalogCodecManagementService.CreateCatalogCodecManagementService();
    public const ushort FormatVersion = 13;
    public const uint Magic = 0x33544143;
    public const int HeaderLength = 32;
    public const int MaximumRecordCount = 65_535;
    public const int MaximumStringBytes = int.MaxValue;
    public static byte[] Encode(CatalogDefinition catalogDefinition) =>
        service.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 27, CatalogDefinition = catalogDefinition })
            .Payload!;

    public static CatalogDefinition Decode(ReadOnlySpan<byte> source) =>
        service.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 28, Payload = source.ToArray() })
            .CatalogDefinition!;

    public static void DecodeFuzzInput(System.ReadOnlyMemory<byte> source) =>
        service.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 28, Payload = source.ToArray() });
}