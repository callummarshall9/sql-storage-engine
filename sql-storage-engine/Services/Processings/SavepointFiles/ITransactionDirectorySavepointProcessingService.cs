// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Services.Processings.SavepointFiles;
internal interface ITransactionDirectorySavepointProcessingService
{
    string GenerateSavepointPath(string databasePath);

    void DeleteAllSavepointFiles(string databasePath);
}