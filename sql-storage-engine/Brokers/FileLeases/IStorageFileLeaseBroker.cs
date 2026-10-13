// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Brokers.FileLeases;
internal interface IStorageFileLeaseBroker
{
    void Open(string path, bool creating);

    void Close();
}