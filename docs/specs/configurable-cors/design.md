# CORS configuravel, seguro e validado - design

## Configuracao

A configuracao fica na secao `Cors` de cada API:

```json
{
  "Cors": {
    "Enabled": true,
    "AllowedOrigins": [
      "https://app.example.com"
    ],
    "AllowedMethods": [
      "GET",
      "POST"
    ],
    "AllowedHeaders": [
      "Authorization",
      "Content-Type",
      "Idempotency-Key",
      "X-Correlation-Id"
    ],
    "ExposedHeaders": [
      "X-Correlation-Id"
    ],
    "AllowCredentials": false,
    "PreflightMaxAgeSeconds": 600
  }
}
```

`ApiDefaults` registra uma option tipada `CorsOptions`, valida com `CorsOptionsValidator` e monta a policy `ApiCorsPolicy` em `CorsPolicyPostConfigureOptions`.

## Comportamento fechado por padrao

`Enabled=false` e `AllowedOrigins=[]` sao o estado padrao. Nessa situacao, `UseApiDefaults` nao chama `UseCors`, e a API nao retorna `Access-Control-Allow-Origin` para requests com `Origin`.

Se `Enabled=true` mas `AllowedOrigins` estiver vazio, o comportamento continua fechado. Isso permite que uma API mantenha a estrutura de configuracao sem expor CORS por acidente.

## APIs com CORS local

No estado atual do repositorio, Ledger, Balance, Transfer, Payment e Identity podem ser exercitadas por clientes browser locais durante desenvolvimento/manual tests. Elas recebem origens locais apenas em `appsettings.Development.json`.

`AuditService.Api` permanece sem origens configuradas porque nao ha consumidor browser definido para a API de auditoria. Se uma UI administrativa surgir, a origem devera ser configurada explicitamente no ambiente dessa UI.

## CORS nao e autenticacao

CORS e uma politica aplicada por navegadores para controlar se uma pagina de uma origem pode ler respostas de outra origem. Ela nao autentica usuarios, nao concede permissoes e nao substitui JWT, scopes, policies ou autorizacao por merchant.

Clientes server-to-server, scripts, ferramentas CLI e atacantes que nao dependem do enforcement do navegador podem enviar requests HTTP sem respeitar CORS. Por isso CORS nao protege chamadas server-to-server nem deve ser usado como autorizacao de negocio.

## Producao

Ambientes compartilhados e produtivos devem configurar `Cors:Enabled=true` apenas para APIs que tenham consumidor browser real. As origens devem ser HTTPS, absolutas e explicitas, por exemplo `https://app.example.com`.

Nao ha dominio produtivo hardcoded no pacote Shared nem nos appsettings base. O deploy deve fornecer a lista de origens pelo mecanismo de configuracao do ambiente.

## Subdominios e wildcards

Wildcards nao sao aceitos em `AllowedOrigins`, incluindo `*` e padroes como `https://*.example.com`. Subdominios devem ser listados um a um enquanto nao houver requisito concreto, modelo de ameaca e testes para wildcard de subdominio.

`AllowCredentials=true` exige ainda mais cuidado e nunca pode ser combinado com wildcard. O design nao chama `AllowAnyOrigin`.
