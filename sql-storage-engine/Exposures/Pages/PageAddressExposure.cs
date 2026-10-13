// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Foundations.PageAddresses;

namespace sql_storage_engine.Exposures.Pages;
public static class PageAddressExposure
{
    private static readonly IPageAddressService service = new PageAddressService();
    public static long CalculatePageAddress(PageId pageId, int pageSize) =>
        service.CalculatePageAddress(pageId: pageId.Value, pageSize: pageSize);
}