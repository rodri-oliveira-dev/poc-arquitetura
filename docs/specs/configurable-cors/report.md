# CORS configuravel, seguro e validado - relatorio

## Inspecao

`ApiDefaultsServiceCollectionExtensions` continha origens locais hardcoded em `AddCors`: `http://localhost:3000`, `http://localhost:5173`, `https://localhost:3001` e `https://localhost:5173`. `UseApiDefaults` sempre executava `UseCors`, mesmo para APIs sem consumidor browser.

## Implementacao

Foi criada a secao tipada `Cors` com os campos `Enabled`, `AllowedOrigins`, `AllowedMethods`, `AllowedHeaders`, `ExposedHeaders`, `AllowCredentials` e `PreflightMaxAgeSeconds`.

O pacote Shared passou a:

- registrar `CorsOptions` com `ValidateOnStart`;
- validar origem absoluta `http`/`https`;
- rejeitar path, query string, fragmento e wildcard;
- validar metodos e headers;
- rejeitar wildcard combinado com credentials;
- montar `ApiCorsPolicy` sem `AllowAnyOrigin`;
- pular `UseCors` quando CORS estiver desabilitado ou sem origens.

## Configuracao

Os appsettings base das APIs ficam fechados com `Cors:Enabled=false` e listas vazias. As origens locais foram movidas para os `appsettings.Development.json` de Ledger, Balance, Transfer, Payment e Identity.

`AuditService.Api` permanece sem origens locais porque nao ha consumidor browser definido.

## Testes

Foram adicionados testes em `tests/Shared/ApiDefaults.Tests` cobrindo CORS desabilitado, origem permitida/rejeitada, multiplas origens, preflight valido, metodo/header nao permitido, origem malformada, origem com path, wildcard com credentials, ausencia de `Access-Control-Allow-Origin` e API sem CORS.

## Riscos residuais

- Cada ambiente produtivo ainda precisa fornecer origens reais explicitamente.
- Se alguma UI futura consumir `AuditService.Api`, sera necessario configurar origem explicita no ambiente dessa UI.
- Wildcards de subdominio continuam fora do escopo ate haver requisito concreto.
