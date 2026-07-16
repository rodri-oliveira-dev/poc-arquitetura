using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using IdentityService.Application.Common.Exceptions;
using IdentityService.Application.Users.Ports;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.IdentityProvider;

public sealed partial class KeycloakAdminClient(
    HttpClient httpClient,
    IOptions<KeycloakAdminOptions> options,
    ILogger<KeycloakAdminClient> logger) : IIdentityProviderUserService
{
    public async Task<CreateIdentityProviderUserResult> CreateUserAsync(
        CreateIdentityProviderUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var accessToken = await GetAdminAccessTokenAsync(cancellationToken);
            var userId = await CreateKeycloakUserAsync(request, accessToken, cancellationToken);

            try
            {
                await SetPasswordAsync(userId, request.Password, accessToken, cancellationToken);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                using var compensationTimeout = CreateCompensationTimeout();
                await CompensateCreatedUserAsync(userId, accessToken, ex, compensationTimeout.Token);
                throw;
            }
            catch (Exception ex)
            {
                await CompensateCreatedUserAsync(userId, accessToken, ex, cancellationToken);
                throw;
            }

            return new CreateIdentityProviderUserResult(userId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw BuildTimeoutException(ex);
        }
        catch (TimeoutException ex)
        {
            throw BuildTimeoutException(ex);
        }
        catch (HttpRequestException ex)
        {
            LogUnexpectedFailure(ex);
            throw new IdentityProviderException(
                IdentityProviderErrorKind.Unexpected,
                "Falha inesperada ao comunicar com o provedor de identidade.",
                innerException: ex);
        }
    }

    public async Task DeleteUserAsync(
        string keycloakUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keycloakUserId))
            throw new ArgumentException("KeycloakUserId is required.", nameof(keycloakUserId));

        try
        {
            var accessToken = await GetAdminAccessTokenAsync(cancellationToken);
            await DeleteKeycloakUserAsync(keycloakUserId, accessToken, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw BuildTimeoutException(ex);
        }
        catch (TimeoutException ex)
        {
            throw BuildTimeoutException(ex);
        }
        catch (HttpRequestException ex)
        {
            LogUnexpectedFailure(ex);
            throw new IdentityProviderException(
                IdentityProviderErrorKind.Unexpected,
                "Falha inesperada ao comunicar com o provedor de identidade.",
                innerException: ex);
        }
    }

    private KeycloakAdminOptions RequiredOptions
    {
        get
        {
            var current = options.Value;

            if (string.IsNullOrWhiteSpace(current.BaseUrl))
                throw new InvalidOperationException("IdentityProvider:Keycloak:BaseUrl nao foi configurado.");

            if (string.IsNullOrWhiteSpace(current.Realm))
                throw new InvalidOperationException("IdentityProvider:Keycloak:Realm nao foi configurado.");

            if (string.IsNullOrWhiteSpace(current.TokenEndpoint))
                throw new InvalidOperationException("IdentityProvider:Keycloak:TokenEndpoint nao foi configurado.");

            if (string.IsNullOrWhiteSpace(current.ClientId))
                throw new InvalidOperationException("IdentityProvider:Keycloak:ClientId nao foi configurado.");

            _ = string.IsNullOrWhiteSpace(current.ClientSecret)
                ? throw new InvalidOperationException("IdentityProvider:Keycloak:ClientSecret nao foi configurado.")
                : current.ClientSecret;

            return current;
        }
    }

    private async Task<string> GetAdminAccessTokenAsync(CancellationToken cancellationToken)
    {
        var currentOptions = RequiredOptions;
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildTokenUri(currentOptions))
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", currentOptions.ClientId!),
                new KeyValuePair<string, string>("client_secret", currentOptions.ClientSecret!)
            ])
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw BuildHttpFailure(response.StatusCode, "obter token administrativo do Keycloak");

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new IdentityProviderException(
                IdentityProviderErrorKind.Unexpected,
                "Keycloak retornou resposta vazia ao emitir token administrativo.");

        return string.IsNullOrWhiteSpace(token.AccessToken)
            ? throw new IdentityProviderException(
                IdentityProviderErrorKind.Unexpected,
                "Keycloak nao retornou access_token ao emitir token administrativo.")
            : token.AccessToken;
    }

    private async Task<string> CreateKeycloakUserAsync(
        CreateIdentityProviderUserRequest input,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var currentOptions = RequiredOptions;
        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"admin/realms/{Escape(currentOptions.Realm!)}/users",
            accessToken);
        request.Content = JsonContent.Create(new KeycloakCreateUserRequest(
            input.Name,
            input.Username,
            input.Email,
            Enabled: true,
            EmailVerified: false));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new IdentityProviderException(
                IdentityProviderErrorKind.Conflict,
                "Keycloak rejeitou a criacao do usuario porque email ou username ja existem.",
                response.StatusCode);
        }

        await EnsureSuccessAsync(response, "criar usuario no Keycloak", cancellationToken);

        return ExtractUserId(response.Headers.Location)
            ?? throw new IdentityProviderException(
                IdentityProviderErrorKind.Unexpected,
                "Keycloak criou o usuario, mas nao retornou o identificador na resposta.");
    }

    private async Task SetPasswordAsync(
        string keycloakUserId,
        string password,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var currentOptions = RequiredOptions;
        using var request = CreateAuthorizedRequest(
            HttpMethod.Put,
            $"admin/realms/{Escape(currentOptions.Realm!)}/users/{Escape(keycloakUserId)}/reset-password",
            accessToken);
        request.Content = JsonContent.Create(new KeycloakPasswordCredential(
            Type: "password",
            Value: password,
            Temporary: false));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "definir senha no Keycloak", cancellationToken);
    }

    private async Task CompensateCreatedUserAsync(
        string keycloakUserId,
        string accessToken,
        Exception originalException,
        CancellationToken cancellationToken)
    {
        LogKeycloakUserCreationCompensationStarted(logger, keycloakUserId);

        try
        {
            await DeleteKeycloakUserAsync(keycloakUserId, accessToken, cancellationToken);
            LogKeycloakUserCreationCompensationSucceeded(logger, keycloakUserId);
        }
#pragma warning disable CA1031 // Compensation failure must be logged without hiding the original creation failure.
        catch (Exception compensationException)
#pragma warning restore CA1031
        {
            LogKeycloakUserCreationCompensationFailed(
                logger,
                compensationException,
                keycloakUserId,
                originalException.GetType().Name);
        }
    }

    private CancellationTokenSource CreateCompensationTimeout()
    {
        var timeout = RequiredOptions.CompensationTimeout;
        if (timeout <= TimeSpan.Zero)
            timeout = new KeycloakAdminOptions().CompensationTimeout;

        var source = new CancellationTokenSource();
        source.CancelAfter(timeout);

        return source;
    }

    private async Task DeleteKeycloakUserAsync(
        string keycloakUserId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var currentOptions = RequiredOptions;
        using var request = CreateAuthorizedRequest(
            HttpMethod.Delete,
            $"admin/realms/{Escape(currentOptions.Realm!)}/users/{Escape(keycloakUserId)}",
            accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return;

        await EnsureSuccessAsync(response, "remover usuario no Keycloak", cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        await response.Content.LoadIntoBufferAsync(cancellationToken);
        throw BuildHttpFailure(response.StatusCode, operation);
    }

    private static IdentityProviderException BuildHttpFailure(HttpStatusCode statusCode, string operation)
    {
        return statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? new IdentityProviderException(
                IdentityProviderErrorKind.Unauthorized,
                $"Keycloak retornou {(int)statusCode} ({statusCode}) ao {operation}. Verifique as credenciais e permissoes administrativas.",
                statusCode)
            : new IdentityProviderException(
                IdentityProviderErrorKind.Unexpected,
                $"Keycloak retornou {(int)statusCode} ({statusCode}) ao {operation}.",
                statusCode);
    }

    private static Uri BuildTokenUri(KeycloakAdminOptions currentOptions)
    {
        var tokenEndpoint = currentOptions.TokenEndpoint!.Trim();
        return IsRootRelativePath(tokenEndpoint)
            ? BuildRelativeTokenUri(currentOptions.BaseUrl!, tokenEndpoint)
            : BuildAbsoluteOrRelativeTokenUri(currentOptions.BaseUrl!, tokenEndpoint);
    }

    private static Uri BuildAbsoluteOrRelativeTokenUri(string baseUrl, string tokenEndpoint)
    {
        return Uri.TryCreate(tokenEndpoint, UriKind.Absolute, out var absoluteUri) switch
        {
            false => BuildRelativeTokenUri(baseUrl, tokenEndpoint),
            true when IsHttpUri(absoluteUri) => absoluteUri,
            _ => throw new InvalidOperationException(
                "IdentityProvider:Keycloak:TokenEndpoint deve ser uma URL HTTP/HTTPS absoluta ou um caminho relativo.")
        };
    }

    private static Uri BuildRelativeTokenUri(string baseUrl, string tokenEndpoint)
    {
        var baseUri = new Uri($"{baseUrl.TrimEnd('/')}/", UriKind.Absolute);
        return new Uri(baseUri, tokenEndpoint.TrimStart('/'));
    }

    private static bool IsRootRelativePath(string value)
    {
        return value.Length > 0
            && (value[0] == '/' || value[0] == '\\');
    }

    private static bool IsHttpUri(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method,
        string requestUri,
        string accessToken)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static string? ExtractUserId(Uri? location)
    {
        if (location is null)
            return null;

        var path = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString.Split('?', 2)[0];

        var userId = path.TrimEnd('/').Split('/').LastOrDefault();
        return string.IsNullOrWhiteSpace(userId)
            ? null
            : Uri.UnescapeDataString(userId);
    }

    private static string Escape(string value)
        => Uri.EscapeDataString(value);

    private IdentityProviderException BuildTimeoutException(Exception exception)
    {
        LogTimeoutFailure(exception);
        return new IdentityProviderException(
            IdentityProviderErrorKind.Timeout,
            "Timeout ao comunicar com o provedor de identidade.",
            innerException: exception);
    }

    private void LogUnexpectedFailure(Exception exception)
        => LogUnexpectedIdentityProviderFailure(logger, exception);

    private void LogTimeoutFailure(Exception exception)
        => LogIdentityProviderTimeout(logger, exception);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Falha inesperada ao comunicar com o provedor de identidade.")]
    private static partial void LogUnexpectedIdentityProviderFailure(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Timeout ao comunicar com o provedor de identidade.")]
    private static partial void LogIdentityProviderTimeout(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Compensando usuario criado no Keycloak apos falha ao concluir criacao. KeycloakUserId: {KeycloakUserId}")]
    private static partial void LogKeycloakUserCreationCompensationStarted(
        ILogger logger,
        string keycloakUserId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Usuario criado no Keycloak removido durante compensacao. KeycloakUserId: {KeycloakUserId}")]
    private static partial void LogKeycloakUserCreationCompensationSucceeded(
        ILogger logger,
        string keycloakUserId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Falha ao compensar usuario criado no Keycloak. KeycloakUserId: {KeycloakUserId}; OriginalExceptionType: {OriginalExceptionType}")]
    private static partial void LogKeycloakUserCreationCompensationFailed(
        ILogger logger,
        Exception exception,
        string keycloakUserId,
        string originalExceptionType);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record KeycloakCreateUserRequest(
        [property: JsonPropertyName("firstName")] string Name,
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("emailVerified")] bool EmailVerified);

    private sealed record KeycloakPasswordCredential(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] string Value,
        [property: JsonPropertyName("temporary")] bool Temporary);
}
