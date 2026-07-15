using System.Globalization;
using System.Net;

namespace ContainerHealthProbe;

internal static class ProbeRunner
{
    public static async Task<int> RunAsync(string[] args, Func<HttpClient>? clientFactory = null)
    {
        if (!ProbeTarget.TryCreate(args, out var uri))
        {
            await Console.Error.WriteLineAsync("Uso: ContainerHealthProbe <porta> <caminho-relativo>");
            return 2;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        if (clientFactory is not null)
        {
            return await SendAsync(clientFactory(), uri, cts.Token);
        }

        using var handler = ProbeTarget.CreateHandler();
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        return await SendAsync(client, uri, cts.Token);
    }

    private static async Task<int> SendAsync(HttpClient client, Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(uri, cancellationToken);
            return response.StatusCode == HttpStatusCode.OK ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            return 1;
        }
        catch (TaskCanceledException)
        {
            return 1;
        }
        catch (InvalidOperationException)
        {
            return 1;
        }
    }
}

internal static class ProbeTarget
{
    private const string LoopbackHost = "127.0.0.1";

    public static bool TryCreate(string[] args, out Uri uri)
    {
        uri = null!;
        if (args.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            return false;
        }

        var path = args[1];
        if (!IsAllowedPath(path))
        {
            return false;
        }

        uri = new UriBuilder(Uri.UriSchemeHttp, LoopbackHost, port, path).Uri;
        return true;
    }

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false
    };

    private static bool IsAllowedPath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
            path.StartsWith('/') &&
            !path.StartsWith("//", StringComparison.Ordinal) &&
            !path.Contains("..", StringComparison.Ordinal) &&
            !path.Contains('\\') &&
            !path.Contains('#') &&
            !path.Contains('?');
    }
}
