using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Transactions;

public enum TransactionMutationKind { HeapInsert, HeapUpdate, HeapDelete, IndexSplit, OverflowReplacement, CatalogChange }

/// <summary>Receives the durable-recovery requirement after in-process undo cannot complete.</summary>
public interface IRecoveryRequirement
{
    bool RecoveryRequired { get; }
    void MarkRecoveryRequired();
}

public sealed class RecoveryRequirement : IRecoveryRequirement
{
    private int _required;
    public bool RecoveryRequired => Volatile.Read(ref _required) != 0;
    public void MarkRecoveryRequired() => Interlocked.Exchange(ref _required, 1);
}

/// <summary>Records transaction undo actions and page ownership changes in mutation order.</summary>
public sealed class TransactionUndoLog(IRecoveryRequirement recoveryRequirement)
{
    private readonly List<(TransactionMutationKind Kind, Action Undo)> _undo = [];
    private readonly List<(PageId PageId, Action Reclaim)> _allocated = [];
    private readonly List<PageId> _retired = [];
    private bool _completed;

    public IReadOnlyList<PageId> AllocatedPages => _allocated.Select(item => item.PageId).ToArray();
    public IReadOnlyList<PageId> RetiredPages => _retired.ToArray();

    public void RecordUndo(TransactionMutationKind kind, Action undo)
    {
        ArgumentNullException.ThrowIfNull(undo);
        EnsureOpen();
        _undo.Add((kind, undo));
    }

    public void RecordBeforeImage(TransactionMutationKind kind, Memory<byte> destination, ReadOnlySpan<byte> beforeImage)
    {
        if (destination.Length != beforeImage.Length) throw new ArgumentException("Before-image length must match its destination.", nameof(beforeImage));
        var snapshot = beforeImage.ToArray();
        RecordUndo(kind, () => snapshot.CopyTo(destination));
    }

    public void TrackAllocatedPage(PageId pageId, Action reclaim)
    {
        ArgumentNullException.ThrowIfNull(reclaim);
        EnsureOpen();
        _allocated.Add((pageId, reclaim));
    }

    public void TrackRetiredPage(PageId pageId) { EnsureOpen(); _retired.Add(pageId); }

    public void Commit() { EnsureOpen(); _completed = true; _undo.Clear(); _allocated.Clear(); }

    public void Rollback()
    {
        EnsureOpen();
        try
        {
            for (var index = _undo.Count - 1; index >= 0; index--) _undo[index].Undo();
            for (var index = _allocated.Count - 1; index >= 0; index--) _allocated[index].Reclaim();
            _completed = true;
            _retired.Clear();
        }
        catch
        {
            recoveryRequirement.MarkRecoveryRequired();
            throw;
        }
    }

    private void EnsureOpen()
    {
        if (_completed) throw new InvalidOperationException("Transaction undo log is already complete.");
    }
}
