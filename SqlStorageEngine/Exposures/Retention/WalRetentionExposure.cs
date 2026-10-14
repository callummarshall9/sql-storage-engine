// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Retention;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Foundations.WalRetentions;

namespace sql_storage_engine.Exposures.Retention;
public sealed class WalRetentionExposure : IDisposable, IWalRetentionExposure
{
    private readonly WalRetentionLogSequenceNumberService service;
    private readonly ulong? registrationId;
    public WalRetentionExposure()
    {
        service = new WalRetentionLogSequenceNumberService();
    }

    private WalRetentionExposure(WalRetentionLogSequenceNumberService service, WalRetentionRegistrationConfiguration configuration)
    {
        this.service = service;
        registrationId = configuration.RegistrationId;
    }

    public int ReadActiveCount() =>
        service.ReadActiveWalRetentionCount(registrationId: registrationId);

    public LogSequenceNumber? ReadMinimumRetainedLsn() =>
        service.ReadMinimumRetainedLogSequenceNumber(registrationId: registrationId);

    public IDisposable Register(LogSequenceNumber logSequenceNumber) =>
        new WalRetentionExposure(service: service, configuration: new WalRetentionRegistrationConfiguration { RegistrationId = service.RegisterWalRetention(logSequenceNumber: logSequenceNumber.Value, registrationId: registrationId) });

    public void Dispose() =>
        service.ReleaseWalRetention(registrationId: registrationId);

    public WalRetentionServiceBinding ReadWalRetentionServiceBinding() =>
        new WalRetentionServiceBinding
    {
        Source = service,
        RegistrationId = registrationId
    };
}