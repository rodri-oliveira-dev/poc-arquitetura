namespace IdentityService.Application.Users.Commands;

public sealed class CreateUserConsistencyOptions
{
    public const string SectionName = "IdentityService:CreateUserConsistency";

    public TimeSpan CompensationTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
