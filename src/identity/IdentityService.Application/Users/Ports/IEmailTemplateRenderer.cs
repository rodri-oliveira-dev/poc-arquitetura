namespace IdentityService.Application.Users.Ports;

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default);
}
