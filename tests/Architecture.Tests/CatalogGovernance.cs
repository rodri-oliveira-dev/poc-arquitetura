namespace Architecture.Tests;

internal static class CatalogGovernance
{
    public static IReadOnlyList<string> FindUncatalogedContextFolders(
        DirectoryInfo repositoryRoot,
        IEnumerable<BoundedContextDescriptor> catalog)
    {
        string sourceRoot = Path.Combine(repositoryRoot.FullName, "src");
        HashSet<string> catalogFolders = [.. catalog.Select(context => context.PhysicalFolder)];

        return [.. Directory.GetDirectories(sourceRoot)
            .Select(directory => new DirectoryInfo(directory))
            .Where(directory => !string.Equals(directory.Name, "Shared", StringComparison.OrdinalIgnoreCase))
            .Where(HasServiceProject)
            .Where(directory => !catalogFolders.Contains(directory.Name))
            .Select(directory => directory.Name)
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    public static IReadOnlyList<string> FindForbiddenInternalProjectReferences(
        BoundedContextDescriptor context,
        ArchitectureLayer layer,
        ProjectModel project)
    {
        IReadOnlySet<ArchitectureLayer> allowedLayers =
            context.AllowedInternalLayerReferences.TryGetValue(layer, out IReadOnlySet<ArchitectureLayer>? value)
                ? value
                : new HashSet<ArchitectureLayer>();

        HashSet<string> allowedProjects = [.. allowedLayers.Select(context.ProjectFileName)];

        return [.. project.ProjectReferenceFileNames
            .Where(reference => reference.StartsWith($"{context.ServiceName}.", StringComparison.OrdinalIgnoreCase))
            .Where(reference => !allowedProjects.Contains(reference))
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    public static IReadOnlyList<string> FindForbiddenSharedProjectReferences(
        BoundedContextDescriptor context,
        ArchitectureLayer layer,
        ProjectModel project)
    {
        IReadOnlySet<string> allowedProjects =
            context.AllowedSharedProjectReferences.TryGetValue(layer, out IReadOnlySet<string>? value)
                ? value
                : new HashSet<string>();

        return [.. project.ProjectReferenceFileNames
            .Where(reference => !reference.StartsWith($"{context.ServiceName}.", StringComparison.OrdinalIgnoreCase))
            .Where(reference => reference.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(reference => !allowedProjects.Contains(reference))
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    private static bool HasServiceProject(DirectoryInfo directory)
        => directory
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Any(project => project.Name.Contains("Service.", StringComparison.Ordinal));
}
