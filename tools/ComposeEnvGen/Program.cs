using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace ComposeEnvGen;

internal static class Program
{
    public static int Main(string[] args)
    {
        var argsList = args.ToList();
        string? composePath = null;
        string? outPath = null;

        for (var i = 0; i < argsList.Count; i++)
        {
            var a = argsList[i];
            if (string.Equals(a, "--compose", StringComparison.OrdinalIgnoreCase) && i + 1 < argsList.Count)
            {
                composePath = argsList[++i];
                continue;
            }
            if (string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase) && i + 1 < argsList.Count)
            {
                outPath = argsList[++i];
                continue;
            }
        }

        composePath ??= "compose.yaml";
        outPath ??= ".env.k6.auto";

        string composeContent;
        try
        {
            composeContent = File.ReadAllText(composePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ComposeEnvGen] Falha ao ler '{composePath}': {ex.Message}");
            return ExitCodes.ComposeReadFailed;
        }

        YamlStream yaml;
        try
        {
            yaml = new YamlStream();
            yaml.Load(new StringReader(composeContent));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ComposeEnvGen] Falha ao parsear YAML: {ex.Message}");
            return ExitCodes.ComposeParseFailed;
        }

        var root = yaml.Documents.FirstOrDefault()?.RootNode as YamlMappingNode;
        if (root is null)
        {
            Console.Error.WriteLine("[ComposeEnvGen] YAML sem root mapping.");
            return ExitCodes.ComposeParseFailed;
        }

        var servicesNode = ComposeHelpers.GetChild(root, "services") as YamlMappingNode;
        if (servicesNode is null)
        {
            Console.Error.WriteLine("[ComposeEnvGen] 'services' não encontrado no compose.");
            return ExitCodes.ComposeParseFailed;
        }

        var serviceNames = servicesNode.Children.Keys
            .OfType<YamlScalarNode>()
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        var ledgerServiceName = ServiceDiscovery.PickBestService(servicesNode, serviceNames, ["ledger"]);
        var balanceServiceName = ServiceDiscovery.PickBestService(servicesNode, serviceNames, ["balance", "consolid"]);
        var authServiceName = ServiceDiscovery.PickBestService(servicesNode, serviceNames, ["auth", "keycloak", "identity"]);

        if (ledgerServiceName is null)
        {
            Console.Error.WriteLine("[ComposeEnvGen] Não foi possível inferir o service do Ledger (keyword: ledger).");
            return ExitCodes.ComposeParseFailed;
        }
        if (balanceServiceName is null)
        {
            Console.Error.WriteLine("[ComposeEnvGen] Não foi possível inferir o service do Balance (keywords: balance|consolid).");
            return ExitCodes.ComposeParseFailed;
        }
        if (authServiceName is null)
        {
            Console.Error.WriteLine("[ComposeEnvGen] Não foi possível inferir o service de Auth (keywords: auth|keycloak|identity).");
            return ExitCodes.ComposeParseFailed;
        }

        var ledgerService = servicesNode.Children[new YamlScalarNode(ledgerServiceName)] as YamlMappingNode;
        var balanceService = servicesNode.Children[new YamlScalarNode(balanceServiceName)] as YamlMappingNode;
        var authService = servicesNode.Children[new YamlScalarNode(authServiceName)] as YamlMappingNode;

        if (ledgerService is null || balanceService is null || authService is null)
        {
            Console.Error.WriteLine("[ComposeEnvGen] Erro interno: service mapping não encontrado.");
            return ExitCodes.ComposeParseFailed;
        }

        var ledgerPort = ServiceDiscovery.DetermineInternalHttpPort(ledgerService);
        var balancePort = ServiceDiscovery.DetermineInternalHttpPort(balanceService);
        var authPort = ServiceDiscovery.DetermineInternalHttpPort(authService);

        var envLines = new List<string>
        {
            "# Arquivo gerado automaticamente. NAO versionar.",
            "# Gerado a partir do compose.yaml para rodar k6 dentro da rede do compose.",
            "",
            $"LEDGER_SERVICE_NAME={ledgerServiceName}",
            $"BALANCE_SERVICE_NAME={balanceServiceName}",
            $"AUTH_SERVICE_NAME={authServiceName}",
            "",
            $"BASE_URL_LEDGER=http://{ledgerServiceName}:{ledgerPort}",
            $"BASE_URL_BALANCE=http://{balanceServiceName}:{balancePort}",
            $"AUTH_BASE_URL=http://{authServiceName}:{authPort}",
            "",
            // Paths inferidos do README atual (rotas estáveis)
            "TOKEN_URL=/auth/login",
            "LEDGER_POST_PATH=/api/v1/lancamentos",
            "BALANCE_DAILY_PATH=/v1/consolidados/diario",
            "BALANCE_PERIOD_PATH=/v1/consolidados/periodo",
            "",
            "# Credenciais default (PoC) - podem ser sobrescritas por env no get-token",
            "USERNAME=poc-usuario",
            "PASSWORD=Poc#123",
            "SCOPE=ledger.write balance.read",
            "MERCHANT_ID=tese",
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? ".");
            File.WriteAllLines(outPath, envLines.Select(l => EnvFile.EscapeValue(l)));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ComposeEnvGen] Falha ao escrever '{outPath}': {ex.Message}");
            return ExitCodes.OutputWriteFailed;
        }

        Console.WriteLine($"[ComposeEnvGen] OK: {outPath}");
        return ExitCodes.Ok;
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
        // Mantém simples: grava cru. Se tiver CR/LF, substitui por espaço.
        return value.Replace("\r", " ").Replace("\n", " ");
    }
}

internal static class ComposeHelpers
{
    public static YamlNode? GetChild(YamlMappingNode map, string key)
    {
        if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node)) return null;
        return node;
    }

    public static IEnumerable<string> GetStringSequence(YamlMappingNode map, string key)
    {
        var node = GetChild(map, key);
        if (node is not YamlSequenceNode seq) yield break;
        foreach (var item in seq)
        {
            if (item is YamlScalarNode s && !string.IsNullOrWhiteSpace(s.Value))
                yield return s.Value!;
        }
    }

    public static IDictionary<string, string> GetEnvironment(YamlMappingNode service)
    {
        // docker compose aceita environment como mapping ou sequence KEY=VALUE.
        var envNode = GetChild(service, "environment");
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (envNode is YamlMappingNode envMap)
        {
            foreach (var kv in envMap.Children)
            {
                var k = (kv.Key as YamlScalarNode)?.Value;
                if (string.IsNullOrWhiteSpace(k)) continue;
                var v = (kv.Value as YamlScalarNode)?.Value ?? string.Empty;
                dict[k!] = v;
            }
        }
        else if (envNode is YamlSequenceNode envSeq)
        {
            foreach (var item in envSeq.Children)
            {
                if (item is not YamlScalarNode s || string.IsNullOrWhiteSpace(s.Value)) continue;
                var parts = s.Value!.Split('=', 2);
                dict[parts[0]] = parts.Length == 2 ? parts[1] : string.Empty;
            }
        }
        return dict;
    }

    public static int? TryParseContainerPortFromPorts(IEnumerable<string> ports)
    {
        // formatos comuns:
        // - "5226:8080"
        // - "5226:8080/tcp"
        // - "8080" (container-only)
        foreach (var p in ports)
        {
            var v = p.Trim();
            if (string.IsNullOrEmpty(v)) continue;
            // remove /proto
            var noProto = v.Split('/', 2)[0];
            var parts = noProto.Split(':');
            if (parts.Length == 1)
            {
                if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var containerOnly))
                    return containerOnly;
            }
            else
            {
                var containerPart = parts[^1];
                if (int.TryParse(containerPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var container))
                    return container;
            }
        }
        return null;
    }

    public static int? TryParseHttpPortFromAspNetCoreUrls(string? urls)
    {
        // Ex.: http://+:8080;https://+:8443
        if (string.IsNullOrWhiteSpace(urls)) return null;
        var tokens = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var t in tokens)
        {
            if (!t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) continue;
            // http://+:8080 or http://0.0.0.0:8080
            var idx = t.LastIndexOf(':');
            if (idx <= 0) continue;
            var portStr = t[(idx + 1)..];
            if (int.TryParse(portStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
                return port;
        }
        return null;
    }
}

internal static class ServiceDiscovery
{
    private static readonly int[] NonHttpContainerPorts = [5432, 9092, 9093];

    public static string? PickBestService(
        YamlMappingNode servicesNode,
        IEnumerable<string> serviceNames,
        string[] includeKeywords)
    {
        var candidates = serviceNames
            .Where(n => includeKeywords.Any(kw => n.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (candidates.Count == 0) return null;

        (string Name, int Score) best = (candidates[0], int.MinValue);

        foreach (var name in candidates)
        {
            if (servicesNode.Children[new YamlScalarNode(name)] is not YamlMappingNode svc)
                continue;

            var score = 0;

            // Nome
            if (name.Contains("service", StringComparison.OrdinalIgnoreCase)) score += 30;
            if (name.Contains("api", StringComparison.OrdinalIgnoreCase)) score += 30;
            if (name.Contains("db", StringComparison.OrdinalIgnoreCase)) score -= 50;
            if (name.Contains("postgres", StringComparison.OrdinalIgnoreCase)) score -= 50;
            if (name.Contains("kafka", StringComparison.OrdinalIgnoreCase)) score -= 50;
            if (name.Contains("init", StringComparison.OrdinalIgnoreCase)) score -= 20;

            // Indícios de HTTP app
            var env = ComposeHelpers.GetEnvironment(svc);
            if (env.ContainsKey("ASPNETCORE_URLS")) score += 100;

            var ports = ComposeHelpers.GetStringSequence(svc, "ports").ToArray();
            var containerPort = ComposeHelpers.TryParseContainerPortFromPorts(ports);
            if (containerPort.HasValue)
            {
                if (containerPort.Value is 80 or 8080 or 5000 or 5001) score += 50;
                if (NonHttpContainerPorts.Contains(containerPort.Value)) score -= 100;
            }

            // Build tende a ser nosso serviço local
            if (ComposeHelpers.GetChild(svc, "build") is not null) score += 20;

            // Evita escolher DB por engano
            var image = (ComposeHelpers.GetChild(svc, "image") as YamlScalarNode)?.Value ?? string.Empty;
            if (image.Contains("postgres", StringComparison.OrdinalIgnoreCase)) score -= 200;

            if (score > best.Score)
                best = (name, score);
        }

        return best.Score == int.MinValue ? null : best.Name;
    }

    public static int DetermineInternalHttpPort(YamlMappingNode service)
    {
        // 1) ports:
        var ports = ComposeHelpers.GetStringSequence(service, "ports").ToArray();
        var fromPorts = ComposeHelpers.TryParseContainerPortFromPorts(ports);
        if (fromPorts.HasValue) return fromPorts.Value;

        // 2) ASPNETCORE_URLS
        var env = ComposeHelpers.GetEnvironment(service);
        if (env.TryGetValue("ASPNETCORE_URLS", out var urls))
        {
            var fromUrls = ComposeHelpers.TryParseHttpPortFromAspNetCoreUrls(urls);
            if (fromUrls.HasValue) return fromUrls.Value;
        }

        // 3) PORT / HTTP_PORT / etc.
        foreach (var key in new[] { "HTTP_PORT", "PORT" })
        {
            if (env.TryGetValue(key, out var v) && int.TryParse(v, out var port))
                return port;
        }

        // 4) fallback
        return 8080;
    }
}
