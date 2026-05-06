# ADR-0046: Plano futuro de adocao incremental do .NET Aspire

## Status
Proposto

## Data
2026-05-06

## Contexto
O projeto e uma POC distribuida em .NET 10 com tres APIs (`Auth.Api`, `LedgerService.Api` e `BalanceService.Api`), dois bancos PostgreSQL, Kafka em KRaft, Outbox, DLQ, JWT via JWKS, health/readiness, OpenTelemetry opcional, Docker Compose via `nerdctl compose`, testes automatizados e pipelines GitHub Actions.

O ambiente local completo ja esta documentado em `README.md` e `docs/development/local-development.md`, usando `compose.yaml` para subir APIs, bancos, Kafka e o job de criacao de topicos. As migrations continuam manuais por decisao existente. Os testes de integracao atuais usam `WebApplicationFactory`, substituem persistencia por EF InMemory em parte do escopo HTTP e desligam Kafka por configuracao.

Ja existe a ADR-0018 propondo avaliar a adocao incremental do .NET Aspire. O levantamento tecnico posterior reforcou que Aspire pode agregar valor, mas tambem confirmou riscos de drift entre `compose.yaml`, scripts, documentacao, configuracoes locais, observabilidade e CI/CD.

Durante o levantamento, a disponibilidade local dos templates Aspire nao foi confirmada: `dotnet new list aspire` nao encontrou templates instalados. A medicao completa dos testes tambem nao foi concluida nesta sessao, pois a execucao excedeu o tempo observado. Esses pontos devem ser tratados como pre-requisitos de qualquer spike.

## Decisao
Manter a adocao de .NET Aspire como ajuste futuro, recomendado parcialmente e condicionado a um spike tecnico pequeno, mensuravel e reversivel.

Aspire nao deve ser adotado agora como substituto automatico de Docker Compose, Testcontainers, `WebApplicationFactory`, pipelines existentes ou estrategia de producao.

O primeiro passo futuro deve ser um spike em branch separada, com escopo minimo:

- validar compatibilidade e templates Aspire com o SDK .NET usado pelo repositorio;
- criar um AppHost experimental apenas para desenvolvimento local;
- modelar inicialmente `Auth.Api`, `LedgerService.Api` e PostgreSQL Ledger;
- preservar migrations manuais ate decisao explicita em contrario;
- validar Aspire Dashboard, logs, traces, health/readiness e configuracao local;
- comparar o fluxo com o `nerdctl compose` atual;
- medir tempo de startup, clareza de configuracao e impacto no onboarding.

Somente apos esse spike deve ser avaliado:

- incluir Kafka, topicos, DLQ e `BalanceService.Api` no AppHost;
- criar um projeto `ServiceDefaults`;
- padronizar OpenTelemetry, health checks, service discovery e resiliencia HTTP;
- introduzir testes baseados em Aspire;
- alterar CI/CD para compilar ou executar AppHost.

O AppHost, se aprovado, deve representar a topologia local de desenvolvimento. Ele nao deve ser tratado como definicao de deploy produtivo.

`ServiceDefaults`, se aprovado, deve ser customizado para preservar o hardening ja existente nas APIs, incluindo JWT/JWKS, Swagger controlado por ambiente, CORS, rate limiting, security headers, correlation id, `/health`, `/ready` e OpenTelemetry opt-in.

## Consequencias

### Beneficios
- Mantem rastreabilidade da decisao antes de adicionar projetos e dependencias Aspire.
- Evita adotar Aspire apenas por tendencia tecnologica.
- Permite validar valor real em ambiente local antes de mexer na arquitetura da solution.
- Preserva o fluxo atual com `compose.yaml` enquanto o spike nao provar ganho concreto.
- Reduz risco de acoplamento prematuro ao AppHost.
- Cria um caminho claro para melhorar onboarding e diagnostico local com Aspire Dashboard.
- Abre espaco para padronizar observabilidade e health checks sem refatoracao ampla.

### Trade-offs / custos
- Mantem, por enquanto, a friccao atual de Compose, portas, variaveis e migrations manuais.
- Exige tempo futuro para preparar templates, criar spike, medir baseline e comparar fluxos.
- Pode criar dois caminhos locais temporarios se AppHost coexistir com Compose.
- `ServiceDefaults` pode duplicar configuracoes atuais se for aplicado sem desenho cuidadoso.
- Testes baseados em Aspire podem aumentar tempo de CI se entrarem cedo demais.
- A equipe precisara entender AppHost, recursos Aspire, parametros, dashboard e limites de uso.

### Riscos
- Drift entre `compose.yaml`, README, scripts, VS Code, AppHost e pipelines.
- Confundir Aspire local orchestration com estrategia produtiva.
- Modelar Kafka/topicos/DLQ de forma diferente do Compose atual.
- Enfraquecer hardening existente ao aceitar defaults de template sem revisao.
- Tornar testes de integracao mais lentos e menos isolados ao substituir `WebApplicationFactory` sem necessidade.
- Criar dependencia de container runtime em caminhos de CI que hoje nao sobem infraestrutura.

## Alternativas consideradas

1. **Nao adotar Aspire**
   - Mantem simplicidade operacional e evita novo tooling.
   - Rejeitado como decisao definitiva porque a solucao tem topologia distribuida real e Aspire pode melhorar onboarding e diagnostico local.

2. **Substituir imediatamente Docker Compose por Aspire**
   - Rejeitado porque `compose.yaml` ja e funcional e documentado, e a substituicao sem spike aumentaria risco de drift e regressao.

3. **Adotar apenas ServiceDefaults, sem AppHost**
   - Possivel, mas nao deve ser o primeiro passo. O maior problema observado esta na orquestracao e diagnostico local da topologia; ServiceDefaults exige mudancas transversais nas APIs e maior cuidado com hardening.

4. **Usar Aspire nos testes de integracao desde o inicio**
   - Rejeitado para a fase inicial. Os testes atuais sao leves e isolados; Aspire deve ser considerado apenas para smoke/e2e distribuido quando o fluxo Ledger -> Kafka -> Balance justificar subir a topologia.

5. **Manter Compose e adicionar AppHost experimental**
   - Alternativa recomendada para o spike. Permite comparar valor real com baixo risco e sem interromper o fluxo atual.

## Proximos passos
- Confirmar instalacao dos templates Aspire e versao compativel com .NET 10.
- Medir baseline atual de `nerdctl compose up -d --build`, aplicacao de migrations e `dotnet test`.
- Criar branch separada para spike, se aprovado.
- Criar AppHost experimental minimo com Auth, Ledger e PostgreSQL Ledger.
- Validar dashboard, logs, traces, health/readiness e configuracao injetada.
- Comparar AppHost e Compose em clareza, tempo de startup, manutencao e onboarding.
- Decidir se Kafka, Balance e DLQ entram em um segundo spike.
- Avaliar ServiceDefaults apenas depois de mapear os defaults atuais das APIs.
- Manter `WebApplicationFactory` e testes unitarios fora de Aspire.
- Criar nova ADR ou atualizar esta decisao antes de tornar Aspire o fluxo local oficial.
