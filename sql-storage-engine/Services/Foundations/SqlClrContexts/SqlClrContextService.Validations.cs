// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Foundations.SqlClrContexts;
internal sealed partial class SqlClrContextService
{
    private static void ValidateContext(SqlClrContext sqlClrContext) =>
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext);

    private static void ValidateIdentity(SqlClrContext sqlClrContext)
    {
        ValidateContext(sqlClrContext: sqlClrContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlClrContext.AssemblyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlClrContext.ClassName);
    }

    private static void ValidateRegistration(SqlClrContext sqlClrContext)
    {
        ValidateIdentity(sqlClrContext: sqlClrContext);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.RuntimeSource);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.Parse);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.Format);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.Validate);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.Compare);
    }

    private static void ValidateType(SqlClrContext sqlClrContext)
    {
        ValidateContext(sqlClrContext: sqlClrContext);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.SqlType);

        if (sqlClrContext.SqlType.UserTypeKind != SqlUserTypeKind.Clr)
        {
            throw new ArgumentException(message: "The type is not a CLR UDT.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlClrContext.SqlType.ClrAssemblyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlClrContext.SqlType.ClrClassName);
    }

    private static void ValidateParse(SqlClrContext sqlClrContext)
    {
        ValidateType(sqlClrContext: sqlClrContext);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.Text);
    }

    private static void ValidateAssembly(SqlClrContext sqlClrContext)
    {
        ValidateContext(sqlClrContext: sqlClrContext);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.Assembly);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.DeclaredTypes);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlClrContext.Assembly.Name);

        if (string.IsNullOrEmpty(value: sqlClrContext.Assembly.ImageContent) || sqlClrContext.Assembly.ImageContent.Any(predicate: item => item > 255))
        {
            throw new ArgumentException(message: "The catalog assembly has no valid deployable image.");
        }

        foreach (var declaration in sqlClrContext.DeclaredTypes)
        {
            if (declaration is null || declaration.UserTypeKind != SqlUserTypeKind.Clr || declaration.ClrAssemblyName != sqlClrContext.Assembly.Name || string.IsNullOrWhiteSpace(value: declaration.ClrClassName))
            {
                throw new ArgumentException(message: "Every declaration must be a CLR UDT bound to the supplied assembly.");
            }
        }
    }

    private static void ValidateFormat(SqlClrContext sqlClrContext)
    {
        ValidateType(sqlClrContext: sqlClrContext);
        ArgumentNullException.ThrowIfNull(argument: sqlClrContext.BinaryInput);

        if (sqlClrContext.BinaryInput.Content is null || sqlClrContext.BinaryInput.StorageFamily != SqlStorageFamily.Binary || sqlClrContext.BinaryInput.IsNull || sqlClrContext.BinaryInput.Content.Any(predicate: item => item > 255))
        {
            throw new ArgumentException(message: "Invalid binary CLR value.");
        }
    }
}