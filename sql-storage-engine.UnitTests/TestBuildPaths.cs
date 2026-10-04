namespace sql_storage_engine.UnitTests;

internal static class TestBuildPaths
{
    private static DirectoryInfo Project
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !directory.EnumerateFiles("*.csproj").Any()) directory = directory.Parent;
            return directory ?? throw new InvalidOperationException("Cannot locate the running test project.");
        }
    }
    internal static string Repository => Project.Parent!.FullName;
    internal static string Probe(string name)
    {
        var output = Path.GetRelativePath(Project.FullName, AppContext.BaseDirectory);
        var file = Path.Combine(Repository, "tests", name, output, name + ".dll");
        if (!File.Exists(file)) throw new FileNotFoundException("Build the probe under the running test output profile.", file);
        return file;
    }
}
