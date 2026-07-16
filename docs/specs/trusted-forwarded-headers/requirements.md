# Forwarded Headers confiaveis por ambiente

## Contexto

As APIs usam `UseForwardedHeaders` no inicio do pipeline para aplicar
`X-Forwarded-For`, `X-Forwarded-Proto` e `X-Forwarded-Host` antes de Swagger,
redirecionamento HTTPS, autenticacao, autorizacao, rate limiting e logs.

O overlay local com Nginx roda em Docker Compose e o IP do container pode mudar
na rede bridge. A configuracao anterior limpava `KnownIPNetworks` e
`KnownProxies` globalmente para acomodar esse caso, mas isso tambem faria
ambientes nao locais confiarem em headers enviados diretamente por clientes.

## Requisitos verificaveis

- Manter `UseForwardedHeaders` antes dos componentes que dependem de scheme,
  host ou IP.
- Preservar `ForwardLimit = 1`.
- Expor options tipadas para proxies confiaveis, redes CIDR confiaveis, hosts
  encaminhados permitidos e modo local permissivo.
- Permitir modo permissivo somente quando explicitamente habilitado em ambiente
  `Development` ou `Local`.
- Exigir pelo menos um proxy confiavel ou uma rede confiavel fora de ambiente
  local.
- Falhar no startup quando uma configuracao nao local estiver insegura ou
  incompleta.
- Validar enderecos IP e CIDRs, incluindo IPv4 e IPv6.
- Impedir confianca irrestrita em headers enviados diretamente por clientes.
- Nao depender de IP fixo do container Nginx no Compose local.
- Manter compatibilidade com chamadas diretas locais, testes de integracao e
  OWASP ZAP.
- Documentar exemplos para Docker Compose local, Nginx, GKE/Kubernetes, load
  balancer e ingress.

## Criterios de aceitacao

- Compose local com overlay Nginx continua aceitando o IP dinamico do proxy.
- Ambientes nao locais falham cedo quando nao configuram proxies ou redes.
- Headers falsificados de clientes diretos sao ignorados.
- `X-Forwarded-Proto` legitimo altera o scheme apenas quando o proxy remoto e
  confiavel.
- `X-Forwarded-Host` e aceito somente quando o host encaminhado esta permitido.
- Cadeias com mais de um proxy nao sobrescrevem alem de `ForwardLimit = 1`.

## Fora do escopo

- Alterar autenticacao JWT ou autorizacao por merchant.
- Adicionar dependencia de infraestrutura especifica ao pacote Shared.
- Criar service mesh, ingress novo ou novo componente de borda.
- Alterar CORS, rate limiting, contratos HTTP ou OpenAPI.
- Executar push, merge ou release.
