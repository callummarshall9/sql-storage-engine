// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Services.Foundations.PageAddresses;
internal sealed partial class PageAddressService : IPageAddressService
{
    public long CalculatePageAddress(ulong pageId, int pageSize) =>
        TryCatch(operation: () =>
    {
        ValidatePageAddress(pageId: pageId, pageSize: pageSize);
        return checked((long)checked(pageId * (ulong)pageSize));
    });
}