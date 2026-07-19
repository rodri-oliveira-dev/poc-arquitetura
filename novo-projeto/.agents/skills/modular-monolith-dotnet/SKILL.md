---
name: modular-monolith-dotnet
description: Use esta skill ao criar, dividir, unir, revisar ou refatorar módulos de negócio em um monólito modular .NET. Aplique-a para definir limites, superfície pública, ownership de dados, dependências permitidas, comunicação entre módulos, transações, testes arquiteturais e critérios de extração futura. Não use para criar microsserviços automaticamente, para organizar código apenas por camadas técnicas ou para impor uma estrutura fixa sem evidência de domínio e acoplamento.
---

# Monólito modular em .NET

## Objetivo

Construir e preservar módulos de negócio coesos, com acoplamento explícito e verificável, dentro de um único deploy.

A modularização deve reduzir o custo de mudança, tornar ownership e dependências compreensíveis e permitir evolução futura sem antecipar a complexidade operacional de microsserviços.

Esta skill complementa:

- `ddd-modeling-vernon`, para descoberta de linguagem e bounded contexts;
- `ddd-implementation-vernon`, para modelagem tática dentro do módulo;
- `dotnet-service-change`, para alterações funcionais localizadas;
- `integration-tests-dotnet`, para testes com infraestrutura real;
- `multitenancy-dotnet`, para isolamento entre tenants;
- `configuring-opentelemetry-dotnet`, para observabilidade entre módulos e futuros serviços.

## Princípios

- Um módulo representa uma capacidade de negócio, não uma pasta técnica.
- Um módulo é um limite de mudança, linguagem, ownership e dependências.
- Bounded context não implica microsserviço.
- Deploy único não autoriza dependências arbitrárias.
- Dados compartilhados por conveniência normalmente ocultam ownership indefinido.
- A separação deve reduzir acoplamento relevante, não apenas aumentar a quantidade de projetos.
- Fronteiras são hipóteses evolutivas e devem ser revisadas com evidência.
- Modularidade sem enforcement tende a degradar.
- Toda regra arquitetural importante deve, quando possível, possuir uma fitness function automatizada.

## Fontes conceituais

Use estas referências como base de raciocínio, sem copiar texto ou tratar uma implementação específica como regra universal:

- `Software Architecture: The Hard Parts`, de Neal Ford, Mark Richards, Pramod Sadalage e Zhamak Dehghani: acoplamento, decomposição, granularidade e trade-offs.
- `Building Evolutionary Architectures`, de Neal Ford, Rebecca Parsons, Patrick Kua e Pramod Sadalage: fitness functions e governança arquitetural automatizada.
- `Implementing Domain-Driven Design` e `Domain-Driven Design Distilled`, de Vaughn Vernon: bounded contexts, context maps e integração entre modelos.
- `Modular Monolith with DDD`, de Kamil Grzybek: exemplo .NET de módulos, contratos, integração e testes arquiteturais.
- ArchUnitNET: enforcement de dependências e regras arquiteturais em assemblies .NET.
- Spring Modulith: referência de verificação de ciclos, APIs públicas, dependências permitidas e testes isolados por módulo.
- `Monolith First`, de Martin Fowler: granularidade inicial mais grossa e extração guiada por aprendizado.
- Team Topologies: fluxo de valor, ownership e carga cognitiva como sinais para limites.

## Quando usar

Use esta skill ao:

- criar o primeiro desenho de módulos;
- adicionar uma nova capacidade de negócio;
- decidir se uma funcionalidade pertence a um módulo existente;
- dividir um módulo grande;
- unir módulos excessivamente fragmentados;
- criar ou revisar contratos entre módulos;
- introduzir evento interno ou integração assíncrona;
- definir ownership de tabelas, schemas, DbContexts ou projeções;
- revisar dependências de projetos e namespaces;
- criar testes arquiteturais;
- analisar ciclos ou mudanças que atravessam muitos módulos;
- preparar uma fronteira para possível extração futura;
- revisar se uma separação em microsserviço é realmente necessária.

## Quando não usar

- Mudança puramente interna a um módulo sem impacto em sua fronteira.
- Renomeação ou formatação sem mudança estrutural.
- Criação automática de um módulo para cada entidade, tabela ou endpoint.
- Extração de microsserviço sem driver arquitetural concreto.
- Imposição de mensageria quando uma chamada local simples atende melhor.
- Criação de múltiplos projetos apenas para reproduzir um diagrama de camadas.

## Processo obrigatório

### 1. Delimite a capacidade de negócio

Antes de criar ou alterar um módulo, descreva:

- qual problema de negócio ele resolve;
- quem usa essa capacidade;
- qual linguagem é usada;
- quais decisões e invariantes pertencem a ela;
- quais dados ela possui;
- quais eventos ou resultados ela produz;
- quais capacidades externas ela consome;
- quais mudanças normalmente acontecem juntas.

Não derive módulos apenas de substantivos do domínio.

Exemplo inadequado:

```text
PetBreed
PetColor
PetWeight
PetDocument
```

Esses conceitos podem fazer parte da mesma capacidade `Pets`, caso mudem juntos e não possuam ciclo de vida independente.

### 2. Analise coesão

Mantenha elementos no mesmo módulo quando houver forte evidência de que:

- mudam pelo mesmo motivo;
- participam da mesma invariante;
- precisam de consistência transacional conjunta;
- compartilham linguagem e ciclo de vida;
- são mantidos pelo mesmo fluxo de trabalho;
- separados exigiriam comunicação frequente e artificial;
- não possuem autonomia útil individualmente.

Registre quais sinais justificam a coesão. Não use apenas proximidade técnica ou reutilização de classes.

### 3. Analise acoplamento

Considere pelo menos:

- acoplamento estático: referências de projeto, namespaces, tipos e contratos;
- acoplamento dinâmico: chamadas em runtime, disponibilidade e latência;
- acoplamento de dados: tabelas, transações, joins e schemas compartilhados;
- acoplamento temporal: necessidade de execução simultânea ou ordenada;
- acoplamento semântico: conhecimento das regras internas de outro módulo;
- acoplamento operacional: deploy, escala, observabilidade e recuperação;
- acoplamento de mudança: arquivos e módulos que frequentemente mudam juntos.

Separar código reduz um tipo de acoplamento e pode aumentar outro. Explicite o trade-off.

### 4. Escolha a granularidade mínima suficiente

Comece com módulos suficientemente grandes para preservar coesão e aprendizado.

Não divida somente porque:

- existem muitas classes;
- há várias tabelas;
- o nome parece representar um subdomínio;
- uma implementação de referência usa mais módulos;
- uma extração futura é imaginável.

Considere dividir quando houver evidência de:

- linguagem ou regras distintas;
- ownership de dados claramente diferente;
- mudanças frequentes por motivos diferentes;
- características arquiteturais divergentes;
- segurança ou compliance distintos;
- necessidade de escala ou disponibilidade diferente;
- carga cognitiva excessiva;
- times ou ownership independentes;
- coordenação interna maior do que a integração necessária entre as partes.

### 5. Defina a superfície pública

Cada módulo deve tornar explícito o que outros módulos podem usar.

A superfície pública pode conter, conforme necessidade:

- comandos ou serviços de aplicação;
- queries deliberadamente expostas;
- DTOs ou contratos;
- eventos publicados;
- interfaces de integração;
- métodos de registro em DI;
- endpoints pertencentes ao módulo.

Tudo que não fizer parte do contrato deve permanecer interno sempre que possível.

Regras:

- não exponha entidades de domínio ou persistência como contrato entre módulos;
- não exponha `DbContext`, repository ou configuração EF Core;
- não permita que outro módulo componha regras usando internals;
- mantenha contratos pequenos e orientados a casos de uso;
- versão ou evolua contratos quando mudanças incompatíveis forem necessárias.

### 6. Defina ownership de dados

Para cada tabela, projeção ou documento, identifique um único módulo owner.

Um módulo não deve:

- consultar diretamente tabelas de outro módulo;
- usar entidades EF Core de outro módulo;
- receber o `DbContext` de outro módulo;
- alterar dados de outro módulo por repository compartilhado;
- criar foreign keys cross-module sem avaliar o acoplamento introduzido;
- reutilizar uma tabela comum apenas para evitar um contrato explícito.

Dentro do mesmo banco, prefira isolamento lógico verificável. Quando fizer sentido, um módulo pode possuir:

- schema próprio;
- DbContext próprio;
- migrations próprias;
- usuário ou permissões específicas de banco.

Essas opções não são obrigatórias por padrão. Escolha-as somente quando reforçarem um limite real e não criarem complexidade operacional desnecessária.

Todo dado de negócio continua sujeito ao `tenant_id` conforme ADR-0001.

### 7. Escolha a comunicação entre módulos

Avalie conscientemente uma das opções:

#### Chamada síncrona por contrato

Use quando:

- o chamador precisa de resposta imediata;
- a operação é local e de baixa latência;
- a dependência de disponibilidade é aceitável;
- não há necessidade de desacoplamento temporal.

Evite referenciar a implementação interna. Use contrato público ou interface deliberada.

#### Evento interno

Use quando:

- outro módulo reage a um fato já ocorrido;
- o produtor não deve conhecer os consumidores;
- consistência eventual é aceitável;
- retry, idempotência e falha parcial foram considerados.

Não use evento apenas para esconder uma chamada direta.

#### Evento de integração

Use quando o contrato precisar sobreviver à extração de processo, deploy ou repositório. Diferencie-o do Domain Event interno e preserve contexto de observabilidade e tenant.

#### Projeção local

Use quando um módulo precisa consultar frequentemente dados derivados de outro sem depender de joins ou chamadas síncronas repetidas. Defina atualização, atraso aceitável, recuperação e ownership.

#### Orquestração de workflow

Use quando um processo de negócio atravessa módulos e precisa tornar estados, compensações ou progresso explícitos. Não introduza Saga apenas por existir mais de um módulo.

### 8. Avalie limites transacionais

Uma transação de negócio deve preferencialmente estar dentro de um módulo.

Quando um caso de uso exige alterar dados de vários módulos atomicamente, investigue:

- se a fronteira está incorreta;
- se os dados realmente possuem ownership distinto;
- se a invariante pertence a um módulo coordenador;
- se uma confirmação assíncrona é aceitável;
- se o workflow precisa de estado explícito;
- se a transação distribuída está sendo evitada apenas nominalmente.

Não compartilhe a mesma transação EF Core entre módulos como solução automática. Também não force consistência eventual quando a regra exige atomicidade real.

### 9. Estruture o módulo proporcionalmente

Não existe uma quantidade obrigatória de projetos.

Um módulo pequeno pode começar como:

```text
Modules/
└── Pets/
    ├── Domain/
    ├── Application/
    ├── Infrastructure/
    └── Api/
```

Um módulo com necessidade real de enforcement por assembly pode evoluir para:

```text
Modules/
└── Scheduling/
    ├── Scheduling.Domain/
    ├── Scheduling.Application/
    ├── Scheduling.Infrastructure/
    ├── Scheduling.Contracts/
    └── Scheduling.Api/
```

Escolha mais assemblies quando eles produzirem um limite compilável útil. Evite projetos vazios, camadas sem comportamento e referências circulares.

### 10. Preserve Building Blocks pequenos

`BuildingBlocks` deve conter somente capacidades técnicas realmente transversais e estáveis, como a library de observabilidade já existente.

Não coloque em Building Blocks:

- entidades de negócio;
- Value Objects pertencentes a um módulo;
- enums de domínio;
- DTOs compartilhados por conveniência;
- repositories genéricos;
- serviços de aplicação comuns sem significado técnico claro.

Antes de mover algo para compartilhado, verifique se há verdadeira identidade conceitual ou apenas duplicação superficial.

### 11. Crie fitness functions

Toda fronteira importante deve ser protegida por teste quando tecnicamente viável.

Use ArchUnitNET ou testes equivalentes para validar:

- ausência de ciclos entre módulos;
- módulos não referenciam internals de outros módulos;
- `Domain` não referencia `Infrastructure`, API, EF Core ou ASP.NET Core;
- contratos não dependem da implementação;
- Building Blocks não dependem de módulos de negócio;
- um módulo não referencia `DbContext` ou entidades persistidas de outro;
- somente dependências explicitamente permitidas são utilizadas;
- adapters de infraestrutura não vazam para contratos públicos;
- módulos não criam dependência circular por eventos ou handlers.

Também considere fitness functions sobre:

- número de dependências entre módulos;
- violações de namespace;
- imports proibidos;
- mudanças de contratos;
- cobertura dos testes de arquitetura.

Não crie testes frágeis baseados apenas em nomes quando uma regra estrutural mais direta for possível.

### 12. Valide isolamento funcional

Além dos testes arquiteturais, valide:

- casos de uso públicos do módulo;
- integração por contratos;
- transações internas;
- comportamento com dois tenants;
- eventos e idempotência quando aplicável;
- observabilidade das chamadas entre módulos;
- falhas parciais e recuperação.

Um teste arquitetural não prova que o fluxo de negócio está correto.

### 13. Revise a fronteira com evidência

Sinais de que dois módulos talvez devam ser unidos:

- quase toda mudança exige alterar ambos;
- chamadas síncronas são excessivas e granulares;
- um módulo não possui decisão ou lifecycle próprio;
- há transações compartilhadas recorrentes;
- contratos apenas espelham internals;
- existe duplicação de modelo sem diferença semântica.

Sinais de que um módulo talvez deva ser dividido:

- linguagem interna conflitante;
- partes mudam por razões diferentes;
- ownership e segurança divergem;
- uma área concentra dependências de todas as demais;
- o módulo tornou-se passagem obrigatória para fluxos não relacionados;
- a carga cognitiva impede entendimento e teste isolado.

Não realize merge ou split grande sem registrar impacto, migração e estratégia incremental.

### 14. Avalie extração para microsserviço

Um módulo ser extraível é desejável. Extraí-lo imediatamente não é.

Considere extração somente quando existir driver concreto, como:

- deploy independente necessário;
- escala ou disponibilidade diferente;
- isolamento de falhas;
- segurança ou compliance próprios;
- equipe e ownership autônomos;
- ciclo de release incompatível;
- tecnologia diferente justificada;
- redução mensurável de coordenação.

Antes da extração, confirme:

- contrato público estável;
- ownership de dados claro;
- comunicação observável;
- idempotência e recuperação;
- propagação de `tenant_id`, correlation e W3C;
- ausência de transações ocultas cross-module;
- custo operacional aceitável.

## Checklist para um novo módulo

1. A capacidade de negócio foi descrita?
2. O motivo para não colocá-la em módulo existente está explícito?
3. Linguagem, regras e ownership são coerentes?
4. A superfície pública é mínima?
5. Internals permanecem inacessíveis externamente?
6. O módulo possui ownership claro de dados?
7. `tenant_id` está presente em seus dados de negócio?
8. A comunicação escolhida é adequada ao acoplamento temporal?
9. As transações ficam preferencialmente dentro do módulo?
10. Observabilidade está preservada nas integrações?
11. Existem testes de arquitetura para o novo limite?
12. Existem testes funcionais com pelo menos dois tenants?
13. O módulo adiciona complexidade proporcional ao benefício?
14. A documentação ou ADR precisa ser atualizada?

## Checklist para revisão de dependências

- A referência é permitida e necessária?
- Existe ciclo direto ou transitivo?
- O chamador conhece detalhes internos do chamado?
- Um contrato de caso de uso substituiria a referência concreta?
- O acoplamento síncrono é intencional?
- A consulta está acessando dados pertencentes a outro módulo?
- Um evento introduziria consistência eventual sem benefício?
- Uma projeção local reduziria dependências recorrentes?
- A dependência precisa ser protegida por fitness function?

## Sinais de risco

Interrompa e revise o desenho quando encontrar:

- pasta `Shared` ou `Common` acumulando conceitos de negócio;
- módulos nomeados apenas por entidade CRUD;
- referências bidirecionais;
- um módulo importando Infrastructure de outro;
- entidades EF Core atravessando fronteiras;
- joins diretos entre tabelas de módulos diferentes;
- eventos usados como RPC disfarçado;
- dezenas de eventos para manter consistência imediata;
- um módulo `Core` que todos usam e ninguém possui;
- contratos que replicam integralmente entidades internas;
- endpoints manipulando múltiplos DbContexts para concluir uma única regra;
- solução dividida em muitos projetos vazios;
- tentativa de extrair microsserviço para corrigir baixa coesão interna;
- modularidade documentada, mas sem testes de enforcement.

## Aplicação inicial ao petshop

Os módulos candidatos registrados em `AGENTS.md` são hipóteses:

```text
Customers
Pets
Scheduling
ServiceCatalog
Workforce
Attendance
Billing
Notifications
```

Antes de materializá-los como projetos separados, valide os limites.

Exemplos de questões:

- `Customers` e `Pets` mudam juntos o suficiente para começar como um único módulo?
- disponibilidade pertence a `Workforce` ou a `Scheduling`?
- check-in e execução do serviço justificam `Attendance` separado do agendamento?
- preços e pacotes pertencem a `ServiceCatalog` ou `Billing`?
- `Notifications` possui regras próprias ou inicialmente é apenas um adapter técnico?
- a agenda exige consistência com recursos físicos e profissionais no mesmo limite?

Não transforme a lista inicial em arquitetura definitiva sem discovery.

## Saída esperada

Ao concluir uma tarefa de modularização, informe:

- capacidade de negócio analisada;
- módulos afetados;
- evidências de coesão e acoplamento;
- superfície pública definida ou alterada;
- ownership de dados;
- forma de comunicação escolhida;
- impacto transacional;
- dependências permitidas e proibidas;
- fitness functions adicionadas ou atualizadas;
- testes funcionais e de isolamento executados;
- riscos e trade-offs restantes;
- se alguma ADR foi criada ou precisa ser criada;
- por que microsserviço foi ou não considerado.
