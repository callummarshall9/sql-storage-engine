// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Exposures.Pages;
public interface IPageDatabaseExposure
{
    PageDatabaseBinding ReadPageDatabaseBinding();
}