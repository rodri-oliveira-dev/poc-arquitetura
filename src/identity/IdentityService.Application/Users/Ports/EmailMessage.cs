namespace IdentityService.Application.Users.Ports;

public sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody);
