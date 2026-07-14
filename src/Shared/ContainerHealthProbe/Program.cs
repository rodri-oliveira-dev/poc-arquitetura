using System.Net;

if (args.Length != 1 || !Uri.TryCreate(args[0], UriKind.Absolute, out var uri))
{
    Console.Error.WriteLine("Uso: ContainerHealthProbe <url>");
    return 2;
}

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
using var client = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(3)
};

try
{
    using var response = await client.GetAsync(uri, cts.Token);
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
