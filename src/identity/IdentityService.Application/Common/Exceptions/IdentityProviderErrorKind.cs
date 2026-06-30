namespace IdentityService.Application.Common.Exceptions;

public enum IdentityProviderErrorKind
{
    Conflict,
    Unauthorized,
    Timeout,
    Unexpected
}
