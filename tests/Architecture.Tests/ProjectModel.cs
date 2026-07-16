using System.Xml.Linq;

namespace Architecture.Tests;

internal sealed class ProjectModel(string path)
{
    private readonly XDocument _document = XDocument.Load(path);

    public string Path
    {
        get;
    } = path;

    public IReadOnlyList<string> PackageAndFrameworkReferences
        => [.. _document.Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Where(reference => reference.Length > 0)];

    public IReadOnlyList<string> ProjectReferenceFileNames
        => [.. _document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(GetProjectReferenceFileName)
            .Where(reference => reference is not null)
            .Select(reference => reference!)];

    public static string? GetProjectReferenceFileName(XElement projectReference)
    {
        string normalizedPath = ((string?)projectReference.Attribute("Include") ?? string.Empty)
            .Replace('\\', System.IO.Path.DirectorySeparatorChar)
            .Replace('/', System.IO.Path.DirectorySeparatorChar);

        return System.IO.Path.GetFileName(normalizedPath);
    }
}
