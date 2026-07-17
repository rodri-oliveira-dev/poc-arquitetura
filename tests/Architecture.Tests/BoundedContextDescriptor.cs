namespace Architecture.Tests;

internal sealed record BoundedContextDescriptor(
    string ServiceName,
    string PhysicalFolder,
    IReadOnlySet<ArchitectureLayer> Layers,
    bool HasPersistence,
    IReadOnlySet<MessagingProvider> AllowedMessagingProviders,
    IReadOnlyDictionary<ArchitectureLayer, IReadOnlySet<ArchitectureLayer>> AllowedInternalLayerReferences,
    IReadOnlyDictionary<ArchitectureLayer, IReadOnlySet<string>> AllowedSharedProjectReferences,
    IReadOnlySet<string> DomainForbiddenTypeNameTerms)
{
    public bool HasWorker => Layers.Contains(ArchitectureLayer.Worker);

    public string AssemblyName(ArchitectureLayer layer)
        => $"{ServiceName}.{layer}";

    public string ProjectFileName(ArchitectureLayer layer)
        => $"{AssemblyName(layer)}.csproj";

    public override string ToString()
        => ServiceName;
}
