// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class StatementJournalPublicationException : IOException
{
    public StatementJournalPublicationException() : base(message: "A statement undo journal already exists.")
    {
    }
}