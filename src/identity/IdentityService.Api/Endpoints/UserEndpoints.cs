using System.ComponentModel.DataAnnotations;

using IdentityService.Api.Contracts.Requests;
using IdentityService.Api.Contracts.Responses;
using IdentityService.Api.Security;
using IdentityService.Application.Users.Commands;

namespace IdentityService.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/users")
            .WithTags("Users")
            .WithGroupName("v1")
            .RequireAuthorization(ScopePolicies.IdentityWritePolicy);

        group.MapPost("/", CreateUserAsync)
            .WithName("CreateIdentityUser")
            .WithSummary("Cadastra usuario no IdentityService")
            .WithDescription("Cria o usuario no provider de identidade e registra o vinculo local com merchantId.")
            .Accepts<CreateUserRequest>("application/json")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return group;
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        CreateUserCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var result = await handler.Handle(
            new CreateUserCommand(
                request.Name!,
                request.Email!,
                request.Username!,
                request.Password!,
                request.Document),
            cancellationToken);

        var response = new CreateUserResponse(
            result.Id,
            result.KeycloakUserId,
            result.MerchantId,
            result.Username,
            result.Email);

        return Results.Created($"/api/v1/users/{response.Id}", response);
    }

    private static Dictionary<string, string[]> Validate(CreateUserRequest request)
    {
        ValidationContext context = new(request);
        List<ValidationResult> results = [];

        if (Validator.TryValidateObject(request, context, results, validateAllProperties: true))
        {
            return [];
        }

        Dictionary<string, string[]> errors = new(StringComparer.Ordinal);
        foreach (ValidationResult result in results)
        {
            string key = result.MemberNames.FirstOrDefault() ?? string.Empty;
            errors[key] = [result.ErrorMessage ?? "The request is invalid."];
        }

        return errors;
    }
}
