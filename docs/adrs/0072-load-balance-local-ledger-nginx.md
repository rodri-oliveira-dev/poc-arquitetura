# ADR-0072: Load balance local do LedgerService.Api com Nginx

## Status
Aceito

## Data
2026-05-24

## Contexto

A POC ja possui uma borda local opcional com Nginx para HTTPS, portal e Swaggers. O `compose.yaml` principal preserva uma instancia direta da `LedgerService.Api` em `http://localhost:5226/`, o que facilita desenvolvimento, scripts e testes de carga existentes.

Para demonstrar escala horizontal local do Ledger sem alterar contrato HTTP nem introduzir Kubernetes, autoscaling ou service discovery avancado, precisamos executar duas instancias da API Ledger atras do Nginx.

O uso direto de `docker compose up --scale ledger-service=2` nao e adequado no estado atual porque o servico principal possui `container_name` fixo e publica uma porta fixa do host. Esses dois pontos criariam conflito ao escalar o servico diretamente.

## Decisao

Manter o `compose.yaml` principal inalterado para a stack minima e adicionar a escala local no overlay `compose.nginx.yaml`.

O overlay:

- cria duas instancias explicitas da `LedgerService.Api`: `ledger-service-1` e `ledger-service-2`;
- usa o mesmo build, a mesma porta interna `8080` e a mesma configuracao local da API Ledger;
- nao publica portas HTTP dessas instancias no host;
- coloca o servico direto `ledger-service` no profile `direct-ledger` quando o overlay e usado, evitando que uma execucao limpa do overlay suba a instancia direta junto das duas instancias balanceadas;
- configura o Nginx com upstream estatico `ledger_api` e algoritmo `least_conn`;
- preserva o endpoint publico unico `https://ledger.localhost:7443/`;
- preserva Swaggers e demais endpoints HTTP do Ledger via Nginx;
- registra `upstream_addr` e `upstream_status` no access log JSON do Nginx;
- devolve `X-Upstream-Addr` nas respostas de `ledger.localhost` apenas para diagnostico local.

## Consequencias

- A stack minima sem Nginx continua usando `ledger-service` e `http://localhost:5226/`.
- O acesso externo ao Ledger balanceado ocorre somente via Nginx em `https://ledger.localhost:7443/`.
- BalanceService.Api e Auth.Api continuam com um unico container no overlay e sem mudanca de contrato.
- Os scripts e testes k6 existentes continuam apontando para as portas HTTP diretas por padrao; eles nao passam a validar o Nginx automaticamente.
- O access log do Nginx passa a ser a forma principal de diagnosticar qual upstream recebeu cada chamada.
- Esta decisao demonstra balanceamento local, mas nao representa autoscaling real de producao.

## Alternativas consideradas

1. **Usar `docker compose up --scale ledger-service=2`**
   - Rejeitado porque o servico atual tem `container_name` fixo e porta fixa publicada no host.

2. **Mover a escala para o `compose.yaml` principal**
   - Rejeitado para preservar simplicidade da stack minima, compatibilidade com scripts e porta direta atual.

3. **Usar DNS interno do Compose com replicas anonimas**
   - Rejeitado nesta etapa porque a topologia explicita com dois servicos e mais simples de diagnosticar na POC e evita conflito com nomes/portas atuais.

4. **Adicionar service discovery dinamico**
   - Fora do escopo. A POC precisa apenas demonstrar load balance local estatico.
