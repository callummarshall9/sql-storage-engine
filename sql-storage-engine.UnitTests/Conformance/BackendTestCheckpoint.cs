namespace SqlExecutionEngine.Storage.Conformance;

/// <summary>Test-only persistence boundaries; never part of the production backend ABI.</summary>
public enum BackendTestCheckpoint
{
    AdmissionWritten,
    AdmissionDurable,
    PublicationWritten,
    PublicationDurable
}
