// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using System.Runtime.ExceptionServices;

namespace sql_storage_engine.Dependencies.StorageRegistrations;
internal sealed class StorageRegistrationDependency : Collection<ServiceDescriptor>, IServiceCollection
{
    public StorageRegistrationDependency(IServiceCollection services) : base(list: services)
    {
    }

    protected override void InsertItem(int index, ServiceDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(argument: item);
        base.InsertItem(index: index, item: item);
    }

    public object BindDefaults(IConfiguration? configuration, Type type, object fallback, Delegate? adjustment)
    {
        var defaults = configuration?.GetSection(key: "StorageEngine").Get(type: type) ?? fallback;

        try
        {
            adjustment?.DynamicInvoke(args: [defaults]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(source: exception.InnerException)
                .Throw();
            throw;
        }

        return defaults;
    }

    public void Register(Type serviceType, Type implementationType, object[] arguments, Type objectStoreType, object? objectStore, Type? objectImplementationType, object[]? objectArguments)
    {
        object strategy;

        try
        {
            objectStore ??= objectImplementationType is null ? null : Activator.CreateInstance(type: objectImplementationType, args: objectArguments);
            var strategyArguments = objectStore is null ? arguments : new object[] { arguments[0], objectStore };
            strategy = Activator.CreateInstance(type: implementationType, args: strategyArguments)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(source: exception.InnerException)
                .Throw();
            throw;
        }

        this.RemoveAll(serviceType: serviceType);
        Add(item: ServiceDescriptor.Singleton(serviceType: serviceType, implementationInstance: strategy));
        this.RemoveAll(serviceType: objectStoreType);

        if (objectStore is not null)
        {
            Add(item: ServiceDescriptor.Singleton(serviceType: objectStoreType, implementationInstance: objectStore));
        }
    }
}