using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Transactions;

/// <summary>Receives the durable-recovery requirement after in-process undo cannot complete.</summary>
public interface IRecoveryRequirement
{
    bool RecoveryRequired { get; }
    void MarkRecoveryRequired();
}
