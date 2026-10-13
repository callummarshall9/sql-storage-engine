// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Services.Foundations.TransactionDirectories;

namespace sql_storage_engine.Services.Processings.SavepointFiles;
/// <summary>Generates savepoint file identities and removes only files belonging to one database.</summary>
internal sealed partial class TransactionDirectorySavepointProcessingService(TransactionDirectoryService transactionDirectoryService) : ITransactionDirectorySavepointProcessingService
{
    public string GenerateSavepointPath(string databasePath) =>
        TryCatch(operation: () =>
    {
        ValidateDatabasePath(databasePath: databasePath);

        return databasePath + ".savepoint-" + transactionDirectoryService.GenerateTransactionFileIdentity()
            .ToString(format: "N");
    });

    public void DeleteAllSavepointFiles(string databasePath) =>
        TryCatch(operation: () =>
    {
        ValidateAllSavepointFilesOnDelete(databasePath: databasePath);
        var prefix = transactionDirectoryService.GetTransactionFileName(path: databasePath) + ".savepoint-";
        var siblings = transactionDirectoryService.GetSiblingTransactionFiles(path: databasePath);

        foreach (var sibling in siblings)
        {
            if (sibling.Name.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
            {
                transactionDirectoryService.DeleteTransactionFile(path: sibling.Path);
            }
        }
    });

    internal static TransactionDirectorySavepointProcessingService CreateTransactionDirectorySavepointProcessingService() =>
        new(transactionDirectoryService: sql_storage_engine.Services.Foundations.TransactionDirectories.TransactionDirectoryService.CreateTransactionDirectoryService());
}