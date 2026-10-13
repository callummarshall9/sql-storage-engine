// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Exposures.Retention;
public interface IWalRetentionExposure
{
    sql_storage_engine.Models.Retention.WalRetentionServiceBinding ReadWalRetentionServiceBinding();
}