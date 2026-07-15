# OWASP ZAP para todas as APIs

Este fluxo executa DAST diretamente contra todas as APIs HTTP da POC. Nenhum gateway e criado ou utilizado: cada servico e analisado pelo proprio documento OpenAPI.

O runner anterior, `scripts/security/run-owasp-zap.sh` e sua versao PowerShell, continua disponivel para compatibilidade com o fluxo local de Ledger e Balance. O GitHub Actions usa `scripts/security/run-owasp-zap-all-apis.sh` para a cobertura ampliada.

## APIs analisadas

| API | Porta no host | Endereco na rede Compose |
|---|---:|---|
| LedgerService.Api | `5226` | `http://ledger-service:8080` |
| BalanceService.Api | `5228` | `http://balance-service:8080` |
| TransferService.Api | `5230` | `http://transfer-service:8080` |
| PaymentService.Api | `5234` | `http://payment-service:8080` |
| AuditService.Api | `5235` | `http://audit-service:8080` |
| IdentityService.Api | `5232` | `http://identity-service:8080` |

O documento importado em cada servico e `/swagger/v1/swagger.json`.

O overlay `compose.owasp-zap.yaml` habilita Swagger somente na stack efemera do scan. A configuracao padrao dos servicos e os bindings em `127.0.0.1` permanecem inalterados.

## Cobertura de contrato

Antes de executar o ZAP, o runner:

1. valida `GET /health` nas seis APIs;
2. baixa cada documento OpenAPI a partir do container do ZAP;
3. confirma que o contrato possui pelo menos um `path`;
4. conta as operacoes HTTP declaradas;
5. executa um `zap-api-scan.py` separado por API;
6. registra paths, operacoes, status e exit code no `summary.md`.

Um contrato vazio, uma API ausente ou uma falha de rede nao pode ser reportado como scan bem-sucedido.

O ZAP importa e considera todas as operacoes declaradas nos contratos. A execucao efetiva de cada operacao ainda depende de o OpenAPI fornecer parametros, exemplos e payloads utilizaveis pelo scanner. Endpoints operacionais fora do contrato, como health checks, sao validados separadamente para disponibilidade.

## Autenticacao

O workflow usa Bearer Token do Keycloak local por padrao. A variavel `ENV_FILE` aponta para o `.env.ci` descartavel, e `scripts/validation/get-token.sh` obtem o token via `client_credentials`.

O token e injetado pelo ZAP Replacer como `Authorization: Bearer`. Assim, o scan nao fica restrito a respostas `401` e consegue alcancar os endpoints protegidos de Ledger, Balance, Transfer, Payment, Audit e Identity.

## Execucao automatica

O workflow `.github/workflows/owasp-zap.yml`:

- executa automaticamente depois do sucesso de `main-dotnet-ci` em um push na `main`;
- pode ser iniciado manualmente por `workflow_dispatch`;
- aplica as migrations dos seis contextos;
- inicia PostgreSQL, Keycloak, Mailpit e as seis APIs;
- verifica que todos os containers compartilham a rede `poc-net`;
- executa um scan independente por contrato;
- publica o artifact `owasp-zap-baseline-reports` por sete dias;
- remove containers e volumes ao final.

Workers, Kafka, Pub/Sub, Nginx e qualquer gateway permanecem fora deste baseline porque nao sao necessarios para importar e testar a superficie HTTP declarada pelas APIs.

## Execucao local

Com a stack local ja iniciada:

```bash
bash ./scripts/security/run-owasp-zap-all-apis.sh \
  --use-authentication
```

Usando a rede Compose e os nomes internos dos servicos:

```bash
bash ./scripts/security/run-owasp-zap-all-apis.sh \
  --docker-network poc-arquitetura_poc-net \
  --use-authentication \
  --ledger-zap-url http://ledger-service:8080 \
  --balance-zap-url http://balance-service:8080 \
  --transfer-zap-url http://transfer-service:8080 \
  --payment-zap-url http://payment-service:8080 \
  --audit-zap-url http://audit-service:8080 \
  --identity-zap-url http://identity-service:8080
```

As URLs do host continuam sendo usadas nos health checks. As opcoes `*-zap-url` definem os enderecos vistos pelo container ZAP.

## Tipo de scan e criterio de falha

O padrao usa `zap-api-scan.py -f openapi -S`, sem active scan. Para uma execucao controlada e mais invasiva, use `--active-scan` apenas em ambiente descartavel e autorizado.

Falham sempre:

- API indisponivel;
- rede Docker inexistente;
- OpenAPI inacessivel ou invalido;
- contrato sem paths ou sem operacoes HTTP;
- falha operacional do container ZAP;
- menos de seis APIs analisadas.

Os alertas do ZAP permanecem consultivos na execucao automatica. Em uma execucao manual, `fail_on_alerts=true` propaga os alertas como falha. Portanto, o fluxo nao se torna gate de release, mas deixa de aceitar resultados falsamente verdes causados por falta de cobertura.

## Relatorios

Cada execucao cria uma pasta timestampada em `artifacts/zap/<yyyyMMdd-HHmmss>/` no CI ou no diretorio configurado por `--output-root`.

Para cada API sao gerados, quando o ZAP conclui a etapa correspondente:

- `<servico>.html`;
- `<servico>.json`;
- `<servico>.md`;
- `<servico>.log`.

O `summary.md` informa as seis APIs, URLs, paths, operacoes HTTP, status, exit codes, total de operacoes dos contratos e confirma que nenhum gateway foi utilizado.

Este baseline complementa, mas nao substitui pentest, threat modeling, testes de autorizacao por identidade ou tenant, testes de abuso de regras de negocio e validacao da borda produtiva.
