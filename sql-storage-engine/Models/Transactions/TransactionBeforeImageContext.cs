// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Models.Transactions;
internal ref struct TransactionBeforeImageContext
{
    public TransactionMutationKind kind;
    public Memory<byte> destination;
    public ReadOnlySpan<byte> beforeImage;
}