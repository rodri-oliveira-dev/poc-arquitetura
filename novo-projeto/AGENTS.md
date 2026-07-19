# AGENTS.md

## Objetivo

Este repositório contém o backend de uma plataforma de operação e agendamento de atendimentos para petshop.

O projeto deve começar simples, com um monólito modular em .NET, fronteiras de domínio explícitas, PostgreSQL quando a persistência for introduzida, APIs REST/OpenAPI e testes automatizados. Microsserviços, mensageria e infraestrutura adicional só devem ser introduzidos quando houver necessidade concreta.

Todo o sistema é multitenant. Cada tenant representa uma organização de petshop que utiliza a plataforma, e o isolamento entre tenants é uma propriedade obrigatória de segurança e consistência.

Observabilidade distribuída é requisito arquitetural. APIs, workers, futuros serviços e adapters de mensageria devem preservar correlação, contexto W3C e tenant conforme os building blocks e ADRs do projeto.

O trabalho dos agentes deve ser pequeno, correto, reproduzível e coerente com a arquitetura existente. Responda em português, salvo pedido explícito em outro idioma.

## Fontes de verdade

Consulte somente os arquivos relevantes para a tarefa atual:

1. `README.md`;
2. `docs/README.md`, quando existir;
3. `docs/adrs/`, quando existir;
4. `docs/architecture/`, quando existir;
5. `docs/domain/`, quando existir;
6. `Directory.Packages.props`;
7. `Directory.Build.props`;
8. `.editorconfig`;
9. `global.json`;
10. `coverlet.runsettings`;
11. a solution e os projetos diretamente relacionados à mudança.

Não carregue toda a documentação indiscriminadamente. Localize primeiro o módulo, contrato ou decisão relacionada ao pedido.

## Direção arquitetural inicial

- Comece como monólito modular.
- Organize código por capacidade de negócio, não por pastas técnicas globais.
- Módulos candidatos incluem `Customers`, `Pets`, `Scheduling`, `ServiceCatalog`, `Workforce`, `Attendance`, `Billing` e `Notifications`.
- Esses nomes são hipóteses iniciais, não fronteiras definitivas.
- Preserve autonomia de modelo, linguagem e persistência entre módulos.
- Não compartilhe entidades de domínio entre módulos apenas porque possuem campos parecidos.
- Não crie microsserviço, fila, cache distribuído, gateway ou banco separado sem requisito claro.

## Multitenancy

A decisão arquitetural principal está registrada em `docs/adrs/0001-multitenancy-claim-e-isolamento-por-linha.md`.

- O tenant de uma requisição autenticada deve ser obtido exclusivamente da claim validada `tenant_id` presente no access token.
- Não aceite o tenant informado livremente pelo cliente em body, query string, rota ou header como fonte de autoridade para operações comuns.
- Não use tenant padrão, fallback silencioso ou tenant implícito quando a claim estiver ausente ou inválida.
- Todas as tabelas de negócio devem possuir a coluna obrigatória `tenant_id`.
- Toda leitura, escrita, alteração e exclusão de dados de negócio deve respeitar o tenant atual.
- Unicidade que seja local a um tenant deve incluir `tenant_id` no índice ou constraint correspondente.
- Relacionamentos entre registros pertencentes a tenants devem impedir associação cruzada.
- O Domain não deve depender de `HttpContext`, claims, JWT ou middleware para descobrir o tenant.
- O tenant deve ser resolvido na borda confiável e propagado explicitamente para Application e Infrastructure.
- Jobs, eventos, cache, idempotência, importações, exportações e processos assíncronos devem preservar o tenant quando lidarem com dados de negócio.
- Logs podem registrar o tenant como contexto estruturado quando necessário, sem expor dados sensíveis. Não use `tenant_id` como label de métrica de alta cardinalidade.
- Toda funcionalidade que acesse dados persistidos deve possuir testes de isolamento com pelo menos dois tenants.
- Operações administrativas cross-tenant exigem fluxo, autorização e auditoria explícitos; nunca devem surgir como exceção informal aos filtros normais.
- Ao implementar ou revisar código afetado por multitenancy, use `.agents/skills/multitenancy-dotnet/SKILL.md`.

## Observabilidade e propagação

A decisão arquitetural está registrada em `docs/adrs/0002-library-propagacao-observabilidade.md`.

- Use `src/BuildingBlocks/PetShop.Observability/` para propagação agnóstica de transporte e `PetShop.Observability.AspNetCore` somente nas APIs.
- Não replique helpers de headers, parsing W3C, baggage, correlation ou criação de Activities dentro de cada serviço.
- `CorrelationId` é independente de `TraceId` e deve continuar disponível mesmo sem Activity amostrada.
- Em HTTP de saída, use `CorrelationIdDelegatingHandler` para `X-Correlation-Id`; deixe `traceparent`, `tracestate` e `baggage` para a instrumentação padrão do `HttpClient` OpenTelemetry.
- Não envie `tenant_id` como header HTTP de autoridade. Entre APIs, o tenant continua sendo validado pelo token e pela autorização.
- Em mensagens e jobs tenant-owned, propague os headers canônicos `correlation_id`, `tenant_id`, `traceparent`, `tracestate` e `baggage`.
- Adapters Kafka, Pub/Sub ou de outros brokers devem apenas converter headers nativos para pares `string/string`; não devem duplicar a lógica de propagação.
- Ao gravar Outbox, persista o snapshot do contexto original. O relay deve restaurá-lo como parent, criar uma Activity `Producer` e publicar o contexto do novo span.
- Consumers devem extrair o contexto, criar uma Activity `Consumer` e abrir o escopo de execução antes de processar a mensagem.
- Retry, DLQ e replay devem preservar todos os headers de propagação.
- Mantenha nomes de `ActivitySource`, operações e tags estáveis.
- Não transporte PII, tokens, segredos ou payloads completos em baggage.
- Não use `correlation_id`, `tenant_id` ou IDs de negócio como labels de métricas.
- Cada executável configura `service.name`, sampling, exporter e OTLP; a library não escolhe vendor APM.
- Ao alterar propagação, tracing, métricas ou configuração OpenTelemetry, use `.agents/skills/configuring-opentelemetry-dotnet/SKILL.md`.

## Regras obrigatórias

- Faça a menor mudança possível para resolver o problema.
- Preserve as fronteiras entre API, Application, Domain e Infrastructure quando essas camadas existirem.
- Não mova regra de negócio para controller, endpoint, middleware, mapper ou infraestrutura.
- Não coloque EF Core, SQL, HTTP, mensageria ou configuração técnica no Domain.
- Não adicione `Version=` em `PackageReference`; use Central Package Management.
- Não altere migrations existentes sem necessidade explícita.
- Não introduza segredos no repositório.
- Não invente URLs, portas, contratos, comandos ou arquitetura.
- Não altere testes apenas para fazê-los passar.
- Em refatorações, preserve o comportamento observável existente, salvo quando a tarefa pedir mudança funcional.
- Não misture refatoração estrutural e mudança funcional sem explicar a necessidade.
- Use `TimeProvider` para comportamento dependente de tempo que precise ser testável.
- Propague `CancellationToken` em operações assíncronas relevantes.
- Atualize documentação quando houver mudança de contrato, arquitetura, setup local ou comportamento importante.
- Remova arquivos abandonados, vazios ou contendo apenas whitespace antes de concluir.

## Critério de implementação

Não implemente recomendações apenas porque são boas práticas genéricas.

Antes de alterar código, infraestrutura, testes ou documentação, avalie:

- necessidade;
- problema observável;
- custo operacional;
- complexidade adicionada;
- benefício esperado;
- risco de manutenção;
- possibilidade de solução mais simples.

Quando uma recomendação não fizer sentido, registre a decisão em vez de criar uma implementação artificial.

## DDD

- Use DDD tático somente em áreas com linguagem relevante, invariantes, concorrência ou ciclo de vida.
- Um Aggregate é um limite de consistência, não um espelho de tabela.
- Prefira métodos que revelem intenção de negócio a setters públicos.
- Use Value Objects para conceitos importantes, como identificadores, intervalos de horário, duração, status, porte e restrições.
- Diferencie Domain Events internos de Integration Events.
- Não crie evento para toda mudança de propriedade.
- Não use um projeto `Shared` como depósito de conceitos de domínio.

## APIs e contratos

- Endpoints devem permanecer finos.
- Contratos HTTP não devem expor entidades de persistência.
- Use códigos HTTP coerentes e Problem Details para erros.
- Coleções potencialmente grandes devem ter paginação e filtros.
- O backend deve revalidar regras críticas; o frontend não é fonte de verdade.
- Concorrência de agenda deve resultar em conflito explícito e tratável.
- Quando OpenAPI for adotado, mantenha o contrato sincronizado por geração ou validação automatizada.

## EF Core e persistência

Sempre que alterar entidades persistidas, mappings, `DbContext`, índices, constraints, relacionamentos ou tipos de coluna:

1. avalie se a mudança exige migration;
2. crie nova migration quando houver alteração de schema;
3. não modifique migrations antigas apenas para organizar;
4. valide concorrência e constraints no banco quando fizerem parte da regra;
5. use PostgreSQL real em testes quando SQL, transações, índices ou comportamento do provider forem relevantes;
6. confirme que toda tabela de negócio possui `tenant_id` obrigatório;
7. confirme que consultas e alterações não permitem acesso cruzado entre tenants;
8. revise índices únicos e relacionamentos para incluir o limite do tenant quando aplicável.

## Skills dos agentes

Antes de executar uma tarefa especializada, verifique `.agents/skills/` e selecione somente as skills cuja `description` corresponda ao pedido.

As skills complementam este arquivo. Em caso de conflito, as regras deste `AGENTS.md` prevalecem.

## Validação

Execute validações proporcionais ao impacto:

- mudança localizada: teste mais próximo;
- mudança de módulo: projetos e testes do módulo;
- mudança transversal: solution agregadora;
- mudança de persistência: testes com provider real quando necessário;
- mudança de contrato: testes HTTP e validação OpenAPI quando disponível;
- mudança em dados de negócio: testes de isolamento entre pelo menos dois tenants;
- mudança em propagação: testes de continuidade W3C, correlation, tenant e preservação de headers.

Fluxo base:

```bash
dotnet tool restore
dotnet restore ./<Solution>.slnx
dotnet build ./<Solution>.slnx --configuration Release --no-restore
dotnet test ./<Solution>.slnx --configuration Release --no-build --no-restore --settings ./coverlet.runsettings
```

Descubra o nome real da solution; não invente `<Solution>`.

Registre claramente qualquer validação que não possa ser executada e o motivo.

## Git e commits

- Nunca aplique alterações diretamente na branch `main`.
- Crie ou use uma branch de trabalho relacionada ao objetivo.
- Não faça push ou abra pull request sem solicitação explícita.
- Use Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`, `build:` ou `ci:`.
- Revise o diff antes de commitar.
- Não crie commit se houver falha de build ou teste sem registrar claramente o motivo.
- Evite formatar ou renomear arquivos fora do escopo.
