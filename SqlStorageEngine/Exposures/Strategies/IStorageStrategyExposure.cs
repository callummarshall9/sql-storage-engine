// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Exposures.Strategies;
public interface IStorageStrategyExposure
{
    ValueTask<IStorageDatabaseExposure> CreateStorageDatabaseExposureAsync(string name, int pageSize = PageConstants.DefaultSize, CancellationToken cancellationToken = default);
    ValueTask<IStorageDatabaseExposure> OpenAsync(string name, CancellationToken cancellationToken = default);
}