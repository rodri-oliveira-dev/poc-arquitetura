# CORS configuravel, seguro e validado - requisitos

## Objetivo

Remover origens CORS hardcoded de `ApiDefaults` e substituir por configuracao tipada, fechada por padrao e validada no startup.

## Requisitos funcionais

- Configurar CORS pela secao `Cors`.
- Suportar `Enabled`, `AllowedOrigins`, `AllowedMethods`, `AllowedHeaders`, `ExposedHeaders`, `AllowCredentials` e `PreflightMaxAgeSeconds`.
- Permitir configuracao diferente por API e ambiente via `appsettings`, environment variables ou mecanismo equivalente do ASP.NET Core.
- Manter desenvolvimento local simples para APIs com consumidor browser.
- Permitir que APIs sem consumidor browser fiquem sem CORS.

## Requisitos de seguranca

- Nao habilitar CORS quando `Enabled=false`.
- Nao habilitar CORS quando `AllowedOrigins` estiver vazio.
- Nao manter origens no pacote Shared.
- Manter origens locais apenas em configuracao de desenvolvimento.
- Exigir origens absolutas `http` ou `https`.
- Rejeitar origem com path, query string ou fragmento.
- Rejeitar wildcard inseguro.
- Impedir wildcard com credentials.
- Validar metodos e headers configurados.
- Nao usar `AllowAnyOrigin` como solucao generica.
- Nao alterar autenticacao, JWT, autorizacao ou rate limiting.
- Nao confiar em `Origin` para autorizacao de negocio.

## Cenarios de teste obrigatorios

- CORS desabilitado.
- Origem permitida.
- Origem rejeitada.
- Multiplas origens.
- Preflight valido.
- Metodo nao permitido.
- Header nao permitido.
- Origem malformada.
- Origem contendo path.
- Wildcard com credentials.
- Ausencia de `Access-Control-Allow-Origin` para origem nao autorizada.
- Comportamento de APIs que nao precisam de CORS.
