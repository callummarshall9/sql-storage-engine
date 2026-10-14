// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Services.Foundations.PageAddresses;
internal interface IPageAddressService
{
    long CalculatePageAddress(ulong pageId, int pageSize);
}