using System.Globalization;

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ComposeEnvGen;

internal static class Program
{
    public static int Main(string[] args)
    {
        var options = CommandLineOptions.Parse(args);

        var readResult = ComposeReader.TryRead(options.ComposePath, out var composeContent);
        if (readResult != ExitCodes.Ok)
        {
            return readResult;
        }

        var parseResult = ComposeReader.TryReadServices(composeContent, out var servicesNode);
        if (parseResult != ExitCodes.Ok)
        {
            return parseResult;
        }

        var discoveryResult = ServiceDiscovery.TryResolveEndpoints(servicesNode, out var endpoints);
        if (discoveryResult != ExitCodes.Ok)
        {
            return discoveryResult;
        }

        var envLines = new List<string>
        {
            "# Arquivo gerado automaticamente. NAO versionar.",
            "# Gerado a partir do compose.yaml para rodar k6 dentro da rede do compose.",
            "",
            $"LEDGER_SERVICE_NAME={endpoints.Ledger.Name}",
            $"BALANCE_SERVICE_NAME={endpoints.Balance.Name}",
            $"TRANSFER_SERVICE_NAME={endpoints.Transfer.Name}",
            "",
            $"BASE_URL_LEDGER=http://{endpoints.Ledger.Name}:{endpoints.Ledger.Port}",
            $"BASE_URL_BALANCE=http://{endpoints.Balance.Name}:{endpoints.Balance.Port}",
            $"BASE_URL_TRANSFER=http://{endpoints.Transfer.Name}:{endpoints.Transfer.Port}",
            "",
            // Paths inferidos do README atual (rotas estaveis).
            "LEDGER_POST_PATH=/api/v1/lancamentos",
            "BALANCE_DAILY_PATH=/api/v1/consolidados/diario",
            "BALANCE_PERIOD_PATH=/api/v1/consolidados/periodo",
            "TRANSFER_PATH=/api/v1/transferencias",
            "",
            "MERCHANT_ID=tese",
            "SOURCE_MERCHANT_ID=m1",
            "DESTINATION_MERCHANT_ID=m2",
        };

        var writeResult = EnvFile.TryWrite(options.OutputPath, envLines);
        if (writeResult != ExitCodes.Ok)
        {
            return writeResult;
        }

        Console.WriteLine($"[ComposeEnvGen] OK: {options.OutputPath}");
        return ExitCodes.Ok;
    }
}

internal sealed record CommandLineOptions(string ComposePath, string OutputPath)
{
    public static CommandLineOptions Parse(string[] args)
    {
        string? composePath = null;
        string? outputPath = null;
        var index = 0;

        while (index < args.Length)
        {
            var nextIndex = index + 1;
            if (nextIndex < args.Length && IsOption(args[index], "--compose"))
            {
                composePath = args[nextIndex];
                index += 2;
                continue;
            }

            if (nextIndex < args.Length && IsOption(args[index], "--out"))
            {
                outputPath = args[nextIndex];
                index += 2;
                continue;
            }

            index++;
        }

        return new CommandLineOptions(composePath ?? "compose.yaml", outputPath ?? ".env.k6.auto");
    }

    private static bool IsOption(string value, string option)
    {
        return string.Equals(value, option, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class ComposeReader
{
    public static int TryRead(string composePath, out string composeContent)
    {
        try
        {
            composeContent = File.ReadAllText(composePath);
            return ExitCodes.Ok;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"[ComposeEnvGen] Falha ao ler '{composePath}': {ex.Message}");
            composeContent = string.Empty;
            return ExitCodes.ComposeReadFailed;
        }
    }

    public static int TryReadServices(string composeContent, out YamlMappingNode servicesNode)
    {
        servicesNode = [];

        var parseResult = TryParseYaml(composeContent, out var yaml);
        if (parseResult != ExitCodes.Ok)
        {
            return parseResult;
        }

        if (yaml.Documents.FirstOrDefault()?.RootNode is not YamlMappingNode root)
        {
            Console.Error.WriteLine("[ComposeEnvGen] YAML sem root mapping.");
            return ExitCodes.ComposeParseFailed;
        }

        if (ComposeHelpers.GetChild(root, "services") is not YamlMappingNode services)
        {
            Console.Error.WriteLine("[ComposeEnvGen] 'services' nao encontrado no compose.");
            return ExitCodes.ComposeParseFailed;
        }

        servicesNode = services;
        return ExitCodes.Ok;
    }

    private static int TryParseYaml(string composeContent, out YamlStream yaml)
    {
        try
        {
            yaml = [];
            yaml.Load(new StringReader(composeContent));
            return ExitCodes.Ok;
        }
        catch (YamlException ex)
        {
            Console.Error.WriteLine($"[ComposeEnvGen] Falha ao parsear YAML: {ex.Message}");
            yaml = [];
            return ExitCodes.ComposeParseFailed;
        }
    }
}

internal static class ExitCodes
{
    public const int Ok = 0;
    public const int InvalidArgs = 2;
    public const int ComposeReadFailed = 3;
    public const int ComposeParseFailed = 4;
    public const int OutputWriteFailed = 5;
}

internal static class EnvFile
{
    public static string EscapeValue(string value)
    {
        return value.Replace("\r", " ").Replace("\n", " ");
    }

    public static int TryWrite(string outputPath, IEnumerable<string> envLines)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
            File.WriteAllLines(outputPath, envLines.Select(EscapeValue));
            return ExitCodes.Ok;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"[ComposeEnvGen] Falha ao escrever '{outputPath}': {ex.Message}");
            return ExitCodes.OutputWriteFailed;
        }
    }
}

internal static class ComposeHelpers
{
    public static YamlNode? GetChild(YamlMappingNode map, string key)
    {
        return map.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;
    }

    public static IEnumerable<string> GetStringSequence(YamlMappingNode map, string key)
    {
        var node = GetChild(map, key);
        if (node is not YamlSequenceNode seq)
        {
            yield break;
        }

        foreach (var item in seq)
        {
            if (item is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            {
                yield return scalar.Value;
            }
        }
    }

    public static IDictionary<string, string> GetEnvironment(YamlMappingNode service)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var envNode = GetChild(service, "environment");
        if (envNode is YamlMappingNode envMap)
        {
            AddEnvironmentMapping(dict, envMap);
        }
        else if (envNode is YamlSequenceNode envSeq)
        {
            AddEnvironmentSequence(dict, envSeq);
        }

        return dict;
    }

    private static void AddEnvironmentMapping(Dictionary<string, string> dict, YamlMappingNode envMap)
    {
        foreach (var kv in envMap.Children)
        {
            var key = (kv.Key as YamlScalarNode)?.Value;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            dict[key] = (kv.Value as YamlScalarNode)?.Value ?? string.Empty;
        }
    }

    private static void AddEnvironmentSequence(Dictionary<string, string> dict, YamlSequenceNode envSeq)
    {
        foreach (var item in envSeq.Children)
        {
            if (item is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                continue;
            }

            var parts = scalar.Value.Split('=', 2);
            dict[parts[0]] = parts.Length == 2 ? parts[1] : string.Empty;
        }
    }

    public static int? TryParseContainerPortFromPorts(IEnumerable<string> ports)
    {
        foreach (var port in ports)
        {
            var value = port.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var noProto = value.Split('/', 2)[0];
            var parts = noProto.Split(':');
            var containerPart = parts.Length == 1 ? parts[0] : parts[^1];
            if (int.TryParse(containerPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var containerPort))
            {
                return containerPort;
            }
        }

        return null;
    }

    public static int? TryParseHttpPortFromAspNetCoreUrls(string? urls)
    {
        if (string.IsNullOrWhiteSpace(urls))
        {
            return null;
        }

        var tokens = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (!token.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var index = token.LastIndexOf(':');
            if (index <= 0)
            {
                continue;
            }

            var portText = token[(index + 1)..];
            if (int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            {
                return port;
            }
        }

        return null;
    }
}

internal static class ServiceDiscovery
{
    private static readonly int[] NonHttpContainerPorts = [5432, 9092, 9093];

    public static int TryResolveEndpoints(YamlMappingNode servicesNode, out ServiceEndpoints endpoints)
    {
        endpoints = default;

        var serviceNames = GetServiceNames(servicesNode);
        if (!TryPickEndpoint(servicesNode, serviceNames, ["ledger"], "Ledger (keyword: ledger)", out var ledger))
        {
            return ExitCodes.ComposeParseFailed;
        }

        if (!TryPickEndpoint(servicesNode, serviceNames, ["balance", "consolid"], "Balance (keywords: balance|consolid)", out var balance))
        {
            return ExitCodes.ComposeParseFailed;
        }

        if (!TryPickEndpoint(servicesNode, serviceNames, ["transfer"], "Transfer (keyword: transfer)", out var transfer))
        {
            return ExitCodes.ComposeParseFailed;
        }

        endpoints = new ServiceEndpoints(ledger, balance, transfer);
        return ExitCodes.Ok;
    }

    public static string? PickBestService(
        YamlMappingNode servicesNode,
        IEnumerable<string> serviceNames,
        string[] includeKeywords)
    {
        var candidates = serviceNames
            .Where(n => includeKeywords.Any(kw => Contains(n, kw)))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        (string Name, int Score) best = (candidates[0], int.MinValue);

        foreach (var name in candidates)
        {
            if (servicesNode.Children[new YamlScalarNode(name)] is not YamlMappingNode service)
            {
                continue;
            }

            var score = ScoreServiceName(name) + ScoreServiceConfiguration(service);
            if (score > best.Score)
            {
                best = (name, score);
            }
        }

        return best.Score == int.MinValue ? null : best.Name;
    }

    public static int DetermineInternalHttpPort(YamlMappingNode service)
    {
        var ports = ComposeHelpers.GetStringSequence(service, "ports").ToArray();
        var fromPorts = ComposeHelpers.TryParseContainerPortFromPorts(ports);
        if (fromPorts.HasValue)
        {
            return fromPorts.Value;
        }

        var env = ComposeHelpers.GetEnvironment(service);
        if (env.TryGetValue("ASPNETCORE_URLS", out var urls))
        {
            var fromUrls = ComposeHelpers.TryParseHttpPortFromAspNetCoreUrls(urls);
            if (fromUrls.HasValue)
            {
                return fromUrls.Value;
            }
        }

        foreach (var key in new[] { "HTTP_PORT", "PORT" })
        {
            if (env.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            {
                return port;
            }
        }

        return 8080;
    }

    private static List<string> GetServiceNames(YamlMappingNode servicesNode)
    {
        return
        [
            .. servicesNode.Children.Keys
            .OfType<YamlScalarNode>()
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!),
        ];
    }

    private static bool TryPickEndpoint(
        YamlMappingNode servicesNode,
        IReadOnlyList<string> serviceNames,
        string[] includeKeywords,
        string errorLabel,
        out ServiceEndpoint endpoint)
    {
        endpoint = default;

        var serviceName = PickBestService(servicesNode, serviceNames, includeKeywords);
        if (serviceName is null)
        {
            Console.Error.WriteLine($"[ComposeEnvGen] Nao foi possivel inferir o service do {errorLabel}.");
            return false;
        }

        if (servicesNode.Children[new YamlScalarNode(serviceName)] is not YamlMappingNode service)
        {
            Console.Error.WriteLine("[ComposeEnvGen] Erro interno: service mapping nao encontrado.");
            return false;
        }

        endpoint = new ServiceEndpoint(serviceName, DetermineInternalHttpPort(service));
        return true;
    }

    private static int ScoreServiceName(string name)
    {
        var score = 0;
        score += Contains(name, "service") ? 30 : 0;
        score += Contains(name, "api") ? 30 : 0;
        score -= Contains(name, "db") ? 50 : 0;
        score -= Contains(name, "postgres") ? 50 : 0;
        score -= Contains(name, "kafka") ? 50 : 0;
        score -= Contains(name, "init") ? 20 : 0;
        return score;
    }

    private static int ScoreServiceConfiguration(YamlMappingNode service)
    {
        var env = ComposeHelpers.GetEnvironment(service);
        var score = env.ContainsKey("ASPNETCORE_URLS") ? 100 : 0;
        score += ScoreContainerPort(service);
        score += ComposeHelpers.GetChild(service, "build") is not null ? 20 : 0;

        var image = (ComposeHelpers.GetChild(service, "image") as YamlScalarNode)?.Value ?? string.Empty;
        score -= Contains(image, "postgres") ? 200 : 0;
        return score;
    }

    private static int ScoreContainerPort(YamlMappingNode service)
    {
        var ports = ComposeHelpers.GetStringSequence(service, "ports").ToArray();
        var containerPort = ComposeHelpers.TryParseContainerPortFromPorts(ports);
        if (!containerPort.HasValue)
        {
            return 0;
        }

        var score = containerPort.Value is 80 or 8080 or 5000 or 5001 ? 50 : 0;
        return NonHttpContainerPorts.Contains(containerPort.Value) ? score - 100 : score;
    }

    private static bool Contains(string value, string expected)
    {
        return value.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }
}

internal readonly record struct ServiceEndpoint(string Name, int Port);

internal readonly record struct ServiceEndpoints(ServiceEndpoint Ledger, ServiceEndpoint Balance, ServiceEndpoint Transfer);
