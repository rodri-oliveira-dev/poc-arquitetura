using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api.Contracts.Requests;

public sealed record CreateUserRequest(
    [property: Required]
    [property: MinLength(1)]
    string? Username,
    [property: Required]
    [property: MinLength(1)]
    string? Name,
    [property: Required]
    [property: EmailAddress]
    string? Email,
    [property: Required]
    [property: MinLength(8)]
    string? Password,
    string? Document);
