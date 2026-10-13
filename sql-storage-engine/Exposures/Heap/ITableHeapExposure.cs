// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Heap;

namespace sql_storage_engine.Exposures.Heap;
public interface ITableHeapExposure
{
    TableHeapBinding ReadTableHeapBinding();
}