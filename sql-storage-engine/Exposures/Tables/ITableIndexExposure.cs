// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Exposures.Tables;
public interface ITableIndexExposure
{
    TableIndexBinding ReadTableIndexBinding();
}