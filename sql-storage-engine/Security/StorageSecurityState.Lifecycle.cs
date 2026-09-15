namespace sql_storage_engine.Security;

internal sealed partial record StorageSecurityState
{
    private static void ValidateLifecycle(StorageSecurityState state)
    {
        if (state.RetiredPrincipals is null || state.DatabasePermissions is null || state.PrincipalDependencies is null ||
            state.RetiredPrincipals.Length > 1024 || state.DatabasePermissions.Length > 1024 || state.PrincipalDependencies.Length > 1024 ||
            state.RetiredPrincipals.Distinct().Count() != state.RetiredPrincipals.Length ||
            state.DatabasePermissions.Distinct().Count() != state.DatabasePermissions.Length ||
            state.PrincipalDependencies.Distinct().Count() != state.PrincipalDependencies.Length)
            throw new ArgumentException("Invalid principal lifecycle catalog.");
        foreach (var id in state.RetiredPrincipals)
        {
            ValidatePrincipal(id);
            if (state.Principals.Any(p => p.Id == id) || state.Permissions.Any(p => p.Principal == id))
                throw new ArgumentException("Retired principal retains authority.");
        }
        foreach (var permission in state.DatabasePermissions)
            if (!Enum.IsDefined(permission.Action) || !Enum.IsDefined(permission.Effect) || !state.Principals.Any(p => p.Id == permission.Principal))
                throw new ArgumentException("Invalid database permission.");
        foreach (var dependency in state.PrincipalDependencies)
            if (!Enum.IsDefined(dependency.Kind) || dependency.ReferenceId == Guid.Empty || !state.Principals.Any(p => p.Id == dependency.Principal))
                throw new ArgumentException("Invalid principal dependency.");
    }
}
