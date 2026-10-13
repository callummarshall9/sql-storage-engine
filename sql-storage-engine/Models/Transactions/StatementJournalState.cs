// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
internal sealed record StatementJournalState
{
    public string Path { get => path!; init => path = value; }

    private string? path;
    internal bool Completed { get; set; }
}