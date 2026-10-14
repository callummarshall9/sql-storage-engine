// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Transactions;
internal sealed class LockRequest
{
    private TaskCompletionSource? _completion;
    public TransactionId TransactionId { get; init; }
    public LockMode Mode { get; init; }
    public bool IsConversion { get; init; }
    public long Sequence { get; init; }
    public TaskCompletionSource Completion { get => _completion!; init => _completion = value; }
    public CancellationTokenRegistration CancellationRegistration { get; set; }
}