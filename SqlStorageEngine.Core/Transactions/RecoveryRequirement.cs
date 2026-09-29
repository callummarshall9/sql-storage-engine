using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Transactions;

public sealed class RecoveryRequirement : IRecoveryRequirement
{
    private int _required;
    public bool RecoveryRequired => Volatile.Read(ref _required) != 0;
    public void MarkRecoveryRequired() => Interlocked.Exchange(ref _required, 1);
}
