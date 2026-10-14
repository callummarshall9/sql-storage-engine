// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.Models.Configurations;
internal sealed record StorageRegistrationConfiguration
{
    public IServiceCollection? Services { get; set; }
    public StorageStrategyOptions? Defaults { get; set; }
    public IConfiguration? Configuration { get; set; }
    public Action<StorageEngineConfiguration>? Adjustment { get; set; }
}