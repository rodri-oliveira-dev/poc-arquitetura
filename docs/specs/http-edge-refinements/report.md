# Relatorio

## Forwarded hosts

Antes, hosts locais eram adicionados por codigo via `AddApiDefaults`, o que
permitia que `localhost` satisfizesse parte da configuracao mesmo fora de um
ambiente local.

Depois, `ForwardedHeaders:AllowedHosts` vem somente de configuracao. Os hosts
locais estao nos `appsettings.Development.json` das APIs. Producao precisa
fornecer host publico real por configuracao externa, alem de proxy ou rede
confiavel.

Testes cobrem desenvolvimento com hosts locais, modo permissivo local,
producao sem hosts, producao com `localhost`, producao com host valido,
multiplos hosts, host encaminhado aceito, host rejeitado, compose local e
ausencia de regressao em Swagger/health.

## Swagger

A policy `swagger` foi removida.

A decisao evita manter uma policy sem efeito: Swagger e registrado por
middleware antes de `UseRateLimiter()` e nao tinha metadata de endpoint. A
exposicao continua controlada por ambiente/flag e os testes de Swagger validam
que UI e JSON seguem funcionando com os headers de seguranca existentes.

## Rate limiting

Modelo considerado: misto, com predominancia de usuario final quando ha
subject.

Precedencia final:

1. `sub` ou `ClaimTypes.NameIdentifier`;
2. `client_id` ou `azp` como componente adicional;
3. `client_id` ou `azp` sozinho em machine-to-machine;
4. merchants autorizados;
5. fallback por IP remoto normalizado.

Os valores continuam protegidos por hash. As metricas nao expõem subject,
client, merchant ou IP bruto.

## Catalogo arquitetural

Propriedades removidas:

- `StripeConceptLayers`;
- `HasApi`.

Propriedades mantidas por consumo real:

- `HasPersistence`;
- `AllowedMessagingProviders`;
- `AllowedInternalLayerReferences`;
- `AllowedSharedProjectReferences`;
- `DomainForbiddenTypeNameTerms`.

Nenhuma regra nova foi adicionada para `StripeConceptLayers`, porque o campo
permitia todas as camadas do Payment e nao acrescentava governanca. A regra
existente continua restringindo conceitos Stripe aos projetos do PaymentService.

## Riscos residuais

O rate limiting permanece local por replica. Swagger nao e limitado pela API;
quando for exposto fora de desenvolvimento, deve ficar atras da borda do
ambiente ou exigir nova decisao para roteamento/rate limiting efetivo.

## Validacao

- `dotnet tool restore`: sucesso.
- `dotnet restore .\PocArquitetura.slnx`: sucesso.
- `dotnet build .\PocArquitetura.slnx --configuration Release --no-restore`:
  sucesso.
- `dotnet test .\tests\Shared\ApiDefaults.Tests\ApiDefaults.Tests.csproj
  --configuration Release --no-build --settings .\coverlet.runsettings`:
  sucesso, 205 testes.
- `dotnet test .\tests\Architecture.Tests\Architecture.Tests.csproj
  --configuration Release --no-build --settings .\coverlet.runsettings`:
  sucesso, 67 testes.
- `dotnet format .\PocArquitetura.slnx --verify-no-changes --severity error
  --include <arquivos .cs alterados>`: sucesso.

A suite agregadora `dotnet test .\PocArquitetura.slnx --configuration Release
--no-build --settings .\coverlet.runsettings` foi executada, mas falhou em
testes que dependem de Testcontainers/Docker. O erro raiz reportado pelo Docker
foi `BadGateway` com `failed to connect to the backend: timed out dialing
Hyper-V socket`. As falhas de startup por `ForwardedHeaders:AllowedHosts` foram
corrigidas antes dessa nova execucao.
