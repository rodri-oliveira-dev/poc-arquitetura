namespace Architecture.Tests;

internal static class ArchitectureTestPaths
{
    public static DirectoryInfo RepositoryRoot { get; } = GetRepositoryRoot();

    public static string ContextFolder(BoundedContextDescriptor context)
        => Path.Combine(RepositoryRoot.FullName, "src", context.PhysicalFolder);

    public static string ProjectPath(BoundedContextDescriptor context, ArchitectureLayer layer)
        => Path.Combine(ContextFolder(context), context.AssemblyName(layer), context.ProjectFileName(layer));

    private static DirectoryInfo GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory;
    }
}
