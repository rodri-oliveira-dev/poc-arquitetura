using System.Net;

using IdentityService.Application.Users.Ports;

namespace IdentityService.Infrastructure.Email;

public sealed class FileEmailTemplateRenderer : IEmailTemplateRenderer
{
    public async Task<string> RenderAsync(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        ArgumentNullException.ThrowIfNull(values);

        var resolvedPath = ResolvePath(templatePath);
        var content = await File.ReadAllTextAsync(resolvedPath, cancellationToken);

        foreach (var (key, value) in values)
        {
            content = content.Replace(
                $"{{{{{key}}}}}",
                WebUtility.HtmlEncode(value),
                StringComparison.Ordinal);
        }

        return content;
    }

    private static string ResolvePath(string templatePath)
        => Path.IsPathRooted(templatePath)
            ? templatePath
            : Path.Combine(AppContext.BaseDirectory, templatePath);
}
