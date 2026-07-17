# TimeProvider e governanca residual - requisitos

## Objetivo

Padronizar dependencias temporais com `TimeProvider` e fechar lacunas de governanca para manter o repositorio previsivel como referencia arquitetural publica.

## Workstream A - tempo e determinismo

Requisitos:

- usar `TimeProvider` como abstracao principal de tempo;
- registrar `TimeProvider.System` nas composition roots;
- remover `IClock/SystemClock` e fallbacks opcionais que criavam relogio internamente;
- tornar dependencia temporal obrigatoria em handlers, services, repositories e workers que dependem de tempo;
- passar instantes da Application para o Domain quando o instante fizer parte do comando ou transicao;
- preservar `DateTimeOffset` onde o contrato/modelo ja usa offset e `DateTime` UTC onde o schema atual persiste `timestamp with time zone` via EF/Npgsql;
- manter timestamps gerados por banco apenas quando forem parte deliberada do fluxo de persistencia;
- permitir testes deterministico com providers fixos.

## Workstream B - governanca

Requisitos:

- criar `SECURITY.md`, `CONTRIBUTING.md` e `.github/CODEOWNERS`;
- definir status canonicos de ADR;
- adicionar validacao automatizada simples para novos ADRs;
- normalizar o indice principal de ADRs sem reescrever o historico;
- preservar contexto historico de ADRs substituidas;
- remover nomes temporarios de implementacao em novos documentos deste workstream.

## Fora de escopo

- migrations historicas;
- refactor amplo de todos os bounded contexts;
- troca indiscriminada de tipos de data;
- contatos, e-mails ou equipes nao existentes;
- push, merge ou release.
