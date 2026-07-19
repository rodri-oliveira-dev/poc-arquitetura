# ADR-0001: Multitenancy por claim e isolamento lógico por linha

- **Status:** Aceita
- **Data:** 2026-07-18
- **Decisão:** `tenant_id` no token e em todas as tabelas de negócio

## Contexto

A plataforma será utilizada por múltiplas organizações de petshop. Cada organização deve acessar somente seus próprios tutores, pets, profissionais, serviços, agendas, atendimentos, cobranças e demais dados de negócio.

O isolamento entre tenants é uma propriedade de segurança e consistência do sistema. Ele não pode depender apenas de filtros aplicados pelo frontend nem de um identificador livremente informado pelo cliente.

A arquitetura inicial será um monólito modular com persistência compartilhada. Mesmo sem microsserviços ou bancos separados, os limites entre tenants devem existir desde a primeira tabela e o primeiro caso de uso.

## Decisão

### Identificação do tenant

Toda requisição autenticada deve possuir a claim obrigatória:

```text
tenant_id
```

A API deve obter o tenant exclusivamente do access token validado pelo mecanismo de autenticação.

O valor de `tenant_id` recebido em body, query string, rota ou header não é fonte de autoridade para operações comuns. Quando algum contrato precisar exibir ou receber um identificador de tenant por motivo administrativo, o backend ainda deve validar esse valor contra a identidade, a autorização e o escopo da operação.

Se a claim estiver ausente, vazia ou inválida, a operação deve ser rejeitada. Não haverá tenant padrão nem fallback silencioso.

### Persistência

Todas as tabelas de negócio devem possuir a coluna obrigatória:

```text
tenant_id
```

A coluna deve ser `NOT NULL` quando a tabela for introduzida no banco.

São exemplos de tabelas de negócio:

- tutores;
- pets;
- profissionais;
- serviços;
- disponibilidades;
- agendamentos;
- atendimentos;
- pacotes;
- cobranças;
- notificações de negócio;
- filas de espera;
- recursos físicos vinculados à operação do petshop.

Tabelas estritamente técnicas ou globais, como histórico de migrations, podem não possuir `tenant_id`. Qualquer nova exceção para uma tabela que armazene informação funcional deve ser explicitamente justificada e registrada.

### Escopo das operações

Toda leitura, inclusão, alteração e exclusão de dados de negócio deve ser executada dentro do tenant atual.

As seguintes regras são obrigatórias:

- registros de um tenant não podem ser lidos ou alterados por outro;
- o tenant de um registro não pode ser trocado como uma atualização comum;
- unicidade local ao tenant deve incluir `tenant_id` no índice ou constraint;
- relacionamentos entre dados tenant-owned devem impedir associação cruzada;
- consultas administrativas cross-tenant devem ser separadas dos fluxos comuns;
- operações cross-tenant exigem autorização específica e auditoria.

### Propagação interna

O tenant deve ser resolvido na borda autenticada e propagado explicitamente para os casos de uso e para a infraestrutura.

O Domain não deve depender de:

- `HttpContext`;
- claims;
- JWT;
- middleware;
- headers HTTP;
- bibliotecas de autenticação.

Jobs, processamento assíncrono, eventos, cache, idempotência, importação e exportação devem preservar `tenant_id` sempre que operarem dados de negócio.

### Testes

Toda funcionalidade persistente deve possuir testes que usem pelo menos dois tenants e comprovem que:

- o Tenant A acessa seus próprios registros;
- o Tenant A não lê registros do Tenant B;
- o Tenant A não altera ou exclui registros do Tenant B;
- uma claim ausente ou inválida é rejeitada;
- informar outro tenant pelo payload, rota, query ou header não contorna o tenant autenticado;
- índices e constraints preservam a unicidade no escopo correto.

## Consequências positivas

- O isolamento é incorporado desde o início do projeto.
- O token fornece uma origem confiável para o tenant.
- O banco pode ser compartilhado sem misturar ownership dos dados.
- Queries, índices e constraints passam a refletir o limite real de segurança.
- A arquitetura pode evoluir posteriormente para schema ou banco por tenant sem alterar o significado de ownership.

## Consequências negativas e custos

- Todas as entidades persistidas e queries de negócio precisam considerar `tenant_id`.
- Índices compostos e relacionamentos ficam mais complexos.
- Testes de integração precisam trabalhar com múltiplos tenants.
- Jobs e mensagens não podem depender de um contexto HTTP implícito.
- Falhas de implementação podem causar vazamento de dados, exigindo revisão e testes rigorosos.

## Alternativas consideradas

### Tenant informado pelo cliente

Rejeitada como fonte de autoridade porque body, query string, rota e headers controlados pelo cliente podem ser adulterados.

### Tenant padrão quando a claim estiver ausente

Rejeitada porque oculta erro de autenticação e pode direcionar operações para o tenant incorreto.

### Schema por tenant

Não adotado neste momento. Aumentaria a complexidade de provisionamento, migrations, pooling e operação antes de existir necessidade comprovada.

### Banco por tenant

Não adotado neste momento. Oferece isolamento físico maior, mas adiciona custo operacional e não é necessário para a primeira fase do projeto.

## Decisões ainda pendentes

Esta ADR não define:

- o tipo concreto de `TenantId` no código e no PostgreSQL;
- o provedor de identidade;
- o mecanismo de autenticação usado no ambiente local;
- o mecanismo técnico de enforcement no EF Core;
- uso ou não de Global Query Filters;
- uso ou não de interceptors;
- uso ou não de PostgreSQL Row-Level Security;
- o modelo de permissões administrativas cross-tenant.

Essas escolhas devem ser feitas antes da implementação correspondente e registradas em ADR complementar quando afetarem segurança, persistência ou operação.

## Orientação para implementação

Ao implementar ou revisar qualquer funcionalidade afetada por esta decisão, use:

```text
.agents/skills/multitenancy-dotnet/SKILL.md
```
