# ADR-0067: Separacao de Workers dos Processos de API

## Status

Aceito

## Contexto

As APIs do `LedgerService` e do `BalanceService` ja hospedaram processamento em background no mesmo processo HTTP. Isso misturava responsabilidades operacionais diferentes: atender requests, consumir Kafka, publicar Outbox, fazer polling de processos pendentes e executar retries com delays continuos.

Essa combinacao dificultava escala independente, readiness, rollback, troubleshooting e observabilidade. Uma API HTTP pode estar pronta para receber requests mesmo quando consumidores Kafka estao temporariamente indisponiveis; da mesma forma, um worker pode precisar de reinicio, ajuste de paralelismo ou diagnostico sem impactar a superficie HTTP.

## Decisao

Separar APIs e workers em processos distintos.

APIs devem servir HTTP. Workers devem executar processamento assincrono continuo.

A camada `Application` continua compartilhada entre API e Worker para preservar casos de uso, validacoes e orquestracao. A camada `Infrastructure` pode ser compartilhada para persistencia, repositories e componentes realmente comuns. HostedServices e adapters tecnicos exclusivos de background devem ficar fisicamente no projeto Worker correspondente, com composition root explicito por processo.

Workers separados nesta decisao:

- `LedgerService.Worker`: publica Outbox no Kafka, processa estornos pendentes e consome solicitacoes de reprocessamento.
- `BalanceService.Worker`: consome eventos Kafka do Ledger e atualiza a projecao de saldos do Balance.

Cada processo deve ter `ServiceName` proprio na observabilidade:

- `LedgerService.Api`
- `LedgerService.Worker`
- `BalanceService.Api`
- `BalanceService.Worker`

A readiness das APIs deve refletir dependencias necessarias para atender HTTP e persistir comandos/consultas. Ela nao deve depender de consumidores Kafka que foram movidos para workers.

## Consequencias positivas

- APIs e workers podem ser escalados, reiniciados e diagnosticados separadamente.
- Readiness da API fica mais fiel a responsabilidade HTTP.
- Rollback de API e rollback de Worker podem ser tratados como movimentos operacionais diferentes.
- Logs, metricas e traces ficam separados por `ServiceName`, reduzindo ambiguidade em troubleshooting.
- Falhas em consumo Kafka, Outbox ou polling continuo deixam de derrubar a disponibilidade HTTP quando nao forem dependencias diretas da request.

## Consequencias negativas e trade-offs

- A stack local e os ambientes passam a ter mais processos para configurar e observar.
- O deploy precisa garantir ordem e compatibilidade entre API antiga e Worker novo durante a migracao.
- Configuracoes antes concentradas no `appsettings` da API passam a existir tambem nos hosts Worker.
- Testes de composicao sao necessarios para impedir regressao e registro acidental de HostedServices na API.

## Alternativas consideradas

- Manter HostedServices dentro das APIs: simples no curto prazo, mas preserva o acoplamento operacional que motivou a mudanca.
- Usar feature flags para desligar HostedServices nas APIs: ajuda no rollout, mas ainda deixa a composicao ambigua e mais facil de configurar errado.
- Criar workers completamente independentes sem compartilhar `Application` e `Infrastructure`: separa processos, mas duplica regras e aumenta risco de divergencia.

## Impacto operacional

O deploy deve tratar APIs e workers como unidades operacionais separadas. Durante rollout, e obrigatorio impedir que API antiga e Worker novo executem o mesmo HostedService simultaneamente.

Antes de subir o Worker novo em um ambiente que ainda tenha API antiga, confirme que os HostedServices equivalentes estao desabilitados ou removidos da API antiga. Essa verificacao e critica para evitar duplicidade de consumo Kafka, publicacao duplicada da Outbox ou processamento repetido de pendencias.

Em observabilidade, dashboards, logs e alertas devem filtrar por `ServiceName` para diferenciar indisponibilidade HTTP de falhas de processamento assincrono.

## Como executar localmente

Para subir a stack local completa com APIs e workers separados:

```powershell
./scripts/start-local-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack.sh
```

Para execucao manual no host, suba a infraestrutura local e execute os processos separadamente:

```powershell
dotnet run --project ./src/LedgerService.Api/LedgerService.Api.csproj
dotnet run --project ./src/LedgerService.Worker/LedgerService.Worker.csproj
dotnet run --project ./src/BalanceService.Api/BalanceService.Api.csproj
dotnet run --project ./src/BalanceService.Worker/BalanceService.Worker.csproj
```

## Como validar que nao ha duplicidade de workers

Valide a composicao dos processos com os testes dedicados:

```powershell
dotnet test ./LedgerService.slnx --configuration Release --filter "FullyQualifiedName~ProcessCompositionPolicyTests"
```

Esses testes devem confirmar que:

- `LedgerService.Api` nao registra `OutboxPublisherService`, `EstornoLancamentoProcessorService` nem `ReprocessamentoLancamentosConsumerService`.
- `LedgerService.Worker` registra os HostedServices esperados quando as flags estao habilitadas.
- `BalanceService.Api` nao registra `LedgerEventsConsumer`.
- `BalanceService.Worker` registra `LedgerEventsConsumer` quando Kafka esta habilitado.

Em ambiente, valide tambem pelos logs e metricas que existe apenas um processo ativo por responsabilidade. Durante migracoes, confira explicitamente que API antiga e Worker novo nao publicam Outbox, consomem o mesmo topico ou processam a mesma fila de pendencias ao mesmo tempo.
