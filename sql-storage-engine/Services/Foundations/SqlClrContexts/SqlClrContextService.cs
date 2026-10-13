// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Brokers.ClrRuntimes;

namespace sql_storage_engine.Services.Foundations.SqlClrContexts;
internal sealed partial class SqlClrContextService(SqlClrRuntimeBroker broker) : ISqlClrContextService
{
    public void RegisterSqlClrContext(SqlClrContext sqlClrContext) =>
        TryCatch(operation: () =>
    {
        ValidateRegistration(sqlClrContext: sqlClrContext);

        if (!broker.Register(assemblyName: sqlClrContext.AssemblyName!, className: sqlClrContext.ClassName!, parse: sqlClrContext.Parse!, format: sqlClrContext.Format!, validate: sqlClrContext.Validate!, compare: sqlClrContext.Compare!))
        {
            throw new InvalidOperationException(message: "A CLR runtime is already registered for this assembly/class identity.");
        }
    });

    public bool UnregisterSqlClrContext(SqlClrContext sqlClrContext) =>
        TryCatch(operation: () =>
    {
        ValidateIdentity(sqlClrContext: sqlClrContext);
        return broker.Unregister(assemblyName: sqlClrContext.AssemblyName!, className: sqlClrContext.ClassName!);
    });

    public void RegisterSqlClrContextAssembly(SqlClrContext sqlClrContext) =>
        TryCatch(operation: () =>
    {
        ValidateAssembly(sqlClrContext: sqlClrContext);

        broker.RegisterAssembly(assemblyName: sqlClrContext.Assembly!.Name, image: sqlClrContext.Assembly.ImageContent.Select(selector: item => checked((byte)item))
            .ToArray(), classNames: sqlClrContext.DeclaredTypes!.Select(selector: item => item.ClrClassName!)
            .ToArray(), validationMethods: sqlClrContext.DeclaredTypes!.Select(selector: item => item.ClrValidationMethodName)
            .ToArray());
    });

    public SqlClrContext ParseSqlClrContext(SqlClrContext sqlClrContext) =>
        TryCatch(operation: () =>
    {
        ValidateParse(sqlClrContext: sqlClrContext);
        var value = broker.Parse(assemblyName: sqlClrContext.SqlType!.ClrAssemblyName!, className: sqlClrContext.SqlType.ClrClassName!, text: sqlClrContext.Text!);

        if (!broker.Validate(assemblyName: sqlClrContext.SqlType.ClrAssemblyName!, className: sqlClrContext.SqlType.ClrClassName!, value: value))
        {
            throw new FormatException(message: "Parsed CLR value failed validation.");
        }

        return sqlClrContext with
        {
            Value = value,
            BinaryResult = new BinarySqlValue
            {
                Content = new string (value.ToArray()
            .Select(selector: item => (char)item)
            .ToArray()),
                StorageFamily = SqlStorageFamily.Binary
            }
        };
    });

    public string FormatSqlClrContext(SqlClrContext sqlClrContext) =>
        TryCatch(operation: () =>
    {
        ValidateFormat(sqlClrContext: sqlClrContext);

        return broker.Format(assemblyName: sqlClrContext.SqlType!.ClrAssemblyName!, className: sqlClrContext.SqlType.ClrClassName!, value: sqlClrContext.BinaryInput is null ? sqlClrContext.Value : sqlClrContext.BinaryInput.Content.Select(selector: item => checked((byte)item))
            .ToArray());
    });

    public bool CheckSqlClrContext(SqlClrContext sqlClrContext) =>
        TryCatch(operation: () =>
    {
        ValidateType(sqlClrContext: sqlClrContext);
        return broker.Validate(assemblyName: sqlClrContext.SqlType!.ClrAssemblyName!, className: sqlClrContext.SqlType.ClrClassName!, value: sqlClrContext.Value);
    });

    public int CompareSqlClrContext(SqlClrContext sqlClrContext) =>
        TryCatch(operation: () =>
    {
        ValidateType(sqlClrContext: sqlClrContext);
        return broker.Compare(assemblyName: sqlClrContext.SqlType!.ClrAssemblyName!, className: sqlClrContext.SqlType.ClrClassName!, left: sqlClrContext.Value, right: sqlClrContext.Other);
    });

    public SqlClrContext ReadSqlClrContextValidation(SqlClrContext sqlClrContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlClrContext: sqlClrContext);

        if (sqlClrContext.SqlType?.UserTypeKind != SqlUserTypeKind.Clr)
        {
            return sqlClrContext with
            {
                Found = false,
                Valid = false
            };
        }

        var result = broker.TryValidate(assemblyName: sqlClrContext.SqlType.ClrAssemblyName!, className: sqlClrContext.SqlType.ClrClassName!, value: sqlClrContext.Value);

        return sqlClrContext with
        {
            Found = result.Found,
            Valid = result.Valid
        };
    });

    internal static SqlClrContextService CreateSqlClrContextService() =>
        new SqlClrContextService(broker: new sql_storage_engine.Brokers.ClrRuntimes.SqlClrRuntimeBroker());
}