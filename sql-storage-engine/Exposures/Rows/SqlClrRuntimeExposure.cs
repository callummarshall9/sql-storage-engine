// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Brokers.ClrRuntimes;
using sql_storage_engine.Services.Foundations.SqlClrContexts;

namespace sql_storage_engine.Exposures.Rows;
public static class SqlClrRuntimeExposure
{
    private static readonly ISqlClrContextService service = new SqlClrContextService(broker: new SqlClrRuntimeBroker());
    public static void Register(string assemblyName, string className, ISqlClrTypeRuntime runtime) =>
        service.RegisterSqlClrContext(sqlClrContext: new SqlClrContext { AssemblyName = assemblyName, ClassName = className, RuntimeSource = runtime, Parse = text => runtime.Parse(text: text), Format = value => runtime.Format(serializedValue: value), Validate = value => runtime.Validate(serializedValue: value), Compare = (left, right) => runtime.Compare(left: left, right: right) });

    public static bool Unregister(string assemblyName, string className) =>
        service.UnregisterSqlClrContext(sqlClrContext: new SqlClrContext { AssemblyName = assemblyName, ClassName = className });

    public static void RegisterAssembly(CatalogAssembly catalogAssembly, IEnumerable<SqlType> declaredTypes) =>
        service.RegisterSqlClrContextAssembly(sqlClrContext: new SqlClrContext { Assembly = catalogAssembly, DeclaredTypes = declaredTypes?.ToArray() });

    public static SqlValue Parse(SqlType sqlType, string text) =>
        service.ParseSqlClrContext(sqlClrContext: new SqlClrContext { SqlType = sqlType, Text = text })
            .BinaryResult!;

    public static string Format(SqlType sqlType, BinarySqlValue binarySqlValue) =>
        service.FormatSqlClrContext(sqlClrContext: new SqlClrContext { SqlType = sqlType, BinaryInput = binarySqlValue });

    public static bool Validate(SqlType sqlType, ReadOnlyMemory<byte> value) =>
        service.CheckSqlClrContext(sqlClrContext: new SqlClrContext { SqlType = sqlType, Value = value });

    public static int Compare(SqlType sqlType, ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) =>
        service.CompareSqlClrContext(sqlClrContext: new SqlClrContext { SqlType = sqlType, Value = left, Other = right });

    internal static bool TryValidate(SqlType sqlType, ReadOnlyMemory<byte> value, out bool valid)
    {
        var result = service.ReadSqlClrContextValidation(sqlClrContext: new SqlClrContext { SqlType = sqlType, Value = value });
        valid = result.Valid;
        return result.Found;
    }
}