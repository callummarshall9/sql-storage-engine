// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Text.Json;
using sql_storage_engine.Storage;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Services.Foundations.SecurityEventFiles;
using sql_storage_engine.Security;
using sql_storage_engine.Services.Foundations.SecurityAuditRecords;
using sql_storage_engine.Services.Foundations.TransactionDirectories;

namespace sql_storage_engine.Services.Orchestrations.SecurityEvents;
internal sealed partial class SecurityEventContextOrchestrationService : ISecurityEventContextOrchestrationService
{
    private readonly TransactionDirectoryService transactionDirectories;
    private readonly SecurityEventFileService files;
    private readonly SecurityAuditRecordService auditRecords;
    public SecurityEventContextOrchestrationService(TransactionDirectoryService transactionDirectories, SecurityEventFileService files, SecurityAuditRecordService records)
    {
        this.transactionDirectories = transactionDirectories;
        this.files = files;
        this.auditRecords = records;
    }

    private const int MaximumEvents = 4096;
    private const int MaximumBytes = 4 * 1024 * 1024;
    public ValueTask AppendSecurityEventContextAsync(SecurityEventContext securityEventContext, CancellationToken token) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventContext(securityEventContext: securityEventContext, token: token);
        var owner = securityEventContext;
        var record = securityEventContext.Record!;
        auditRecords.ValidateStorageAuditRecord(storageAuditRecord: record);

        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Durable security events require Linux.");
        }

        if (record.Kind is not (StorageAuditKind.Denied or StorageAuditKind.ReadAdmission))
        {
            throw new ArgumentException("Event requires transaction-coupled audit.");
        }

        if (record.Kind == StorageAuditKind.Denied && owner.HasTransaction)
        {
            throw new InvalidOperationException("Abort the user transaction before recording denial.");
        }

        {
            if (owner.Uncertain)
            {
                throw new StorageAuditException(record.OperationId);
            }

            var records = Read(owner: owner);
            var existing = records.SingleOrDefault(predicate: a => a.OperationId == record.OperationId);

            if (existing is not null)
            {
                if (existing != record)
                {
                    throw new ArgumentException("Operation identity has conflicting audit content.");
                }

                try
                {
                    files.FlushSecurityEventFile(securityEventFile: new SecurityEventFile { Path = owner.TransactionPath + ".security-events" });
                    transactionDirectories.FlushTransactionDirectory(path: owner.TransactionPath + ".security-events");
                }
                catch
                {
                    owner.Uncertain = true;
                    throw new StorageAuditException(record.OperationId);
                }

                return ValueTask.CompletedTask;
            }

            if (records.Length >= MaximumEvents)
            {
                throw new InvalidOperationException("Security event retention quota exceeded.");
            }

            var path = owner.TransactionPath + ".security-events";
            var temporary = path + ".pending";

            var bytes = files.EncodeSecurityEventFile(securityEventFile: new SecurityEventFile { Path = temporary, DatabaseId = owner.DatabaseId, Records = records.Append(element: record)
            .ToArray() });

            token.ThrowIfCancellationRequested();

            try
            {
                owner.Observer?.Invoke(obj: "security-event-before-write");
                files.WriteSecurityEventFile(securityEventFile: new SecurityEventFile { Path = temporary, Bytes = bytes });
                token.ThrowIfCancellationRequested();
                owner.Observer?.Invoke(obj: "security-event-before-publish");
                files.PublishSecurityEventFile(securityEventFile: new SecurityEventFile { Path = temporary, Destination = path });
                owner.Observer?.Invoke(obj: "security-event-after-publish");
                transactionDirectories.FlushTransactionDirectory(path: path);
                owner.Observer?.Invoke(obj: "security-event-durable");
            }
            catch (OperationCanceledException)
            {
                files.DeleteSecurityEventFile(deletedSecurityEventFile: new SecurityEventFile { Path = temporary });
                throw;
            }
            catch
            {
                owner.Uncertain = true;
                throw new StorageAuditException(record.OperationId);
            }
        }

        return ValueTask.CompletedTask;
    });

    public ValueTask<SecurityEventContext> ReadSecurityEventContextAsync(SecurityEventContext securityEventContext, CancellationToken token) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityEventContext(securityEventContext: securityEventContext, token: token);
        var owner = securityEventContext;

        {
            return ValueTask.FromResult(result: securityEventContext with { Records = Array.AsReadOnly(array: Read(owner: owner)) });
        }
    });

    private StorageAuditRecord[] Read(SecurityEventContext owner)
    {
        var path = owner.TransactionPath + ".security-events";

        if (!files.ExistsSecurityEventFile(securityEventFile: new SecurityEventFile { Path = path }))
        {
            return[];
        }

        if (files.ReadLengthSecurityEventFile(securityEventFile: new SecurityEventFile { Path = path })is < 48 or > MaximumBytes)
        {
            throw new StorageFormatException("Invalid security event file length.");
        }

        var bytes = files.ReadBytesSecurityEventFile(securityEventFile: new SecurityEventFile { Path = path });

        try
        {
            var records = files.DecodeSecurityEventFile(securityEventFile: new SecurityEventFile { Path = path, DatabaseId = owner.DatabaseId, Bytes = bytes })
            .Records!;

            if (records.Length > MaximumEvents || records.Select(selector: a => a.OperationId)
            .Distinct()
            .Count() != records.Length)
            {
                throw new ArgumentException();
            }

            foreach (var record in records)
            {
                auditRecords.ValidateStorageAuditRecord(storageAuditRecord: record);

                if (record.Kind is not (StorageAuditKind.Denied or StorageAuditKind.ReadAdmission))
                {
                    throw new ArgumentException();
                }
            }

            return records;
        }
        catch (Exception error)when (error is JsonException or ArgumentException or NullReferenceException)
        {
            throw new StorageFormatException("Invalid security event records.");
        }
    }

    internal static SecurityEventContextOrchestrationService CreateSecurityEventContextOrchestrationService() =>
        new(transactionDirectories: sql_storage_engine.Services.Foundations.TransactionDirectories.TransactionDirectoryService.CreateTransactionDirectoryService(), files: SecurityEventFileService.CreateSecurityEventFileService(), records: SecurityAuditRecordService.CreateSecurityAuditRecordService());
}