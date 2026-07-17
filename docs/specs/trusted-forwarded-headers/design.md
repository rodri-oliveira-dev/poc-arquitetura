# Design

## Diagnostico

`ApiDefaultsServiceCollectionExtensions` configurava
`ForwardedHeadersOptions` para processar `X-Forwarded-For`,
`X-Forwarded-Proto` e `X-Forwarded-Host`, preservava `ForwardLimit = 1` e
adicionava hosts encaminhados permitidos a partir de cada API. Em seguida,
limpava `KnownIPNetworks` e `KnownProxies` para permitir o Nginx local com IP
dinamico.

As APIs chamam `UseForwardedHeaders()` antes de Swagger, `UseApiDefaults()`,
autenticacao e autorizacao. A ordem esta correta e deve ser preservada.

O Nginx local envia:

- `X-Forwarded-For $proxy_add_x_forwarded_for`;
- `X-Forwarded-Proto https`;
- `X-Forwarded-Host $host`;
- `X-Forwarded-Port 7443`.

Terraform/GCP ainda e baseline de infraestrutura e nao define uma topologia de
ingress produtiva para as APIs. A configuracao deve, portanto, ser generica:
aceitar IPs e CIDRs fornecidos pelo ambiente sem acoplar Shared a GKE, Cloud
Run, Kubernetes ou a um produto especifico.

## Decisoes

- Criar `TrustedForwardedHeadersOptions` na secao `ForwardedHeaders`.
- Manter os hosts passados para `AddApiDefaults` como baseline de
  `AllowedHosts` para compatibilidade com as APIs existentes.
- Permitir adicionar hosts por configuracao em
  `ForwardedHeaders:AllowedHosts`.
- Configurar `ForwardedHeadersOptions` via `IPostConfigureOptions`, lendo as
  options tipadas e o ambiente.
- Validar as options com `IValidateOptions` e `ValidateOnStart`.
- Considerar ambientes locais apenas `Development` e `Local`.
- Em modo local permissivo, limpar `KnownIPNetworks` e `KnownProxies` somente
  quando `EnableLocalPermissiveMode=true` e o ambiente for local.
- Fora de ambiente local, exigir `TrustedProxies` ou `TrustedNetworks`.
- Validar IPs com `IPAddress.TryParse`.
- Validar CIDRs no formato `endereco/prefixo`, com prefixo 0..32 para IPv4 e
  0..128 para IPv6.
- Preservar `ForwardLimit = 1` para aceitar somente o hop externo imediato.

## Configuracao

```json
{
  "ForwardedHeaders": {
    "TrustedProxies": [ "10.0.0.10", "2001:db8::10" ],
    "TrustedNetworks": [ "10.128.0.0/20", "2001:db8:1234::/64" ],
    "AllowedHosts": [ "api.example.com" ],
    "EnableLocalPermissiveMode": false
  }
}
```

## Topologias consideradas

- Docker Compose local: Nginx opcional em rede bridge com IP dinamico; usa
  `EnableLocalPermissiveMode=true` apenas no overlay local.
- Nginx local: continua encaminhando os headers existentes para as APIs.
- Kubernetes/GKE: configurar o CIDR ou IP do ingress/load balancer como
  `TrustedNetworks` ou `TrustedProxies`.
- Cloud Run ou load balancer gerenciado: configurar o proxy ou range efetivo
  documentado pelo ambiente, sem assumir default permissivo.

## Exemplos

Docker Compose local atras do Nginx do repositorio:

```yaml
environment:
  ASPNETCORE_ENVIRONMENT: "Local"
  ForwardedHeaders__EnableLocalPermissiveMode: "true"
```

Nginx local:

```nginx
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto https;
proxy_set_header X-Forwarded-Host $host;
```

GKE/Kubernetes com ingress em rede conhecida:

```yaml
env:
  - name: ASPNETCORE_ENVIRONMENT
    value: Production
  - name: ForwardedHeaders__TrustedNetworks__0
    value: 10.128.0.0/20
  - name: ForwardedHeaders__AllowedHosts__0
    value: api.example.com
```

Load balancer ou ingress com IP fixo conhecido:

```yaml
env:
  - name: ForwardedHeaders__TrustedProxies__0
    value: 10.0.0.10
  - name: ForwardedHeaders__AllowedHosts__0
    value: api.example.com
```

## Limites conhecidos

- A biblioteca nao descobre automaticamente ranges de cloud providers.
- Se a topologia tiver mais de um proxy confiavel antes da API, sera necessario
  revisar `ForwardLimit` e documentar a decisao antes de alterar o default.
- `AllowedHosts` limita `X-Forwarded-Host`; o `AllowedHosts` nativo do ASP.NET
  permanece configurado separadamente por aplicacao.
