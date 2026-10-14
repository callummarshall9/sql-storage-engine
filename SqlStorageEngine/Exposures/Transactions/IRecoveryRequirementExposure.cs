// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Exposures.Transactions;
/// <summary>Receives the recovery requirement after in-process undo cannot complete.</summary>
public interface IRecoveryRequirementExposure
{
    bool IsRecoveryRequired();

    void MarkRecoveryRequired();
}