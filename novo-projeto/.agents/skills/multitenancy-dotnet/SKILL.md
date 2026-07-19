---
name: multitenancy-dotnet
description: Use esta skill ao criar ou revisar funcionalidades .NET que envolvam autenticação, persistência, queries, comandos, jobs, eventos, cache, idempotência, importação, exportação ou testes em dados multitenant. O tenant deve vir da claim validada tenant_id e todas as tabelas de negócio devem possuir tenant_id. Não use para documentação sem impacto em multitenancy nem para inventar mecanismos de isolamento ainda não decididos em ADR.
---

# Multitenancy em .NET

## Objetivo

Aplicar de forma consistente a decisão registrada em:

```text
docs/adrs/0001-multitenancy-claim-e-isolamento-por-linha.md
```

O isolamento entre tenants é uma propriedade de segurança. Toda alteração deve impedir leitura, escrita, associação ou processamento cruzado de dados entre organizações de petshop.

## Decisões obrigatórias

- O tenant autenticado vem exclusivamente da claim validada `tenant_id` do access token.
- Todas as tabelas de negócio possuem a coluna obrigatória `tenant_id`.
- Não existe tenant padrão ou fallback silencioso.
- O Domain não acessa `HttpContext`, claims, JWT ou headers.
- Operações administrativas cross-tenant exigem fluxo e autorização explícitos.

## Quando usar

Use esta skill ao trabalhar com:

- configuração de autenticação e autorização;
- resolução e propagação do tenant atual;
- endpoints que leem ou alteram dados de negócio;
- entidades persistidas e mappings EF Core;
- `DbContext`, repositories, specifications ou queries;
- migrations, índices, constraints e relacionamentos;
- jobs, schedulers e `BackgroundService`;
- eventos de domínio ou integração;
- cache e idempotência;
- importações, exportações e relatórios;
- testes unitários, de integração, contrato ou ponta a ponta que envolvam ownership dos dados;
- revisão de segurança ou code review com risco de acesso cross-tenant.

## Quando não usar

- Mudança puramente visual ou documental sem impacto na decisão.
- Código técnico que comprovadamente não armazena nem processa dados de negócio.
- Implementação de Row-Level Security, Global Query Filters ou outro mecanismo ainda não aprovado pela arquitetura.
- Criação de abstração genérica de multitenancy sem consumidor real.

## Processo obrigatório

### 1. Identifique o dado e seu ownership

Antes de alterar código, responda:

- A informação pertence a um tenant?
- Qual módulo é dono desse dado?
- A operação é comum a um tenant ou administrativa cross-tenant?
- O caso de uso depende de contexto autenticado ou será executado em background?

Na dúvida, trate dados funcionais do petshop como tenant-owned e registre a incerteza.

### 2. Resolva o tenant na borda confiável

Para requisições autenticadas:

- leia `tenant_id` somente do principal já autenticado;
- rejeite claim ausente, vazia ou inválida;
- não confie em `tenant_id` recebido em body, rota, query string ou header;
- não permita que um valor enviado pelo cliente substitua o tenant autenticado;
- diferencie autenticação inválida de autorização insuficiente.

Não espalhe acesso direto ao `HttpContext` pela aplicação. Use uma abstração pequena e explícita na borda de Application/Infrastructure quando necessária, por exemplo um contexto de tenant atual, sem acoplar o Domain ao ASP.NET Core.

### 3. Propague o tenant explicitamente

O tenant deve estar disponível para o caso de uso e para a persistência sem depender de estado global mutável.

- Não use variável estática para tenant atual.
- Não armazene tenant em singleton mutável.
- Não presuma que um job possui `HttpContext`.
- Não derive tenant de um registro carregado sem antes limitar a consulta ao tenant autorizado.
- Não aceite `TenantId` do command como autoridade quando o command vier diretamente de input externo.

### 4. Modele persistência tenant-owned

Ao criar uma tabela de negócio:

- adicione `tenant_id` como coluna obrigatória;
- impeça alteração comum do tenant após a criação do registro;
- configure índice para `tenant_id` quando necessário ao padrão de acesso;
- inclua `tenant_id` em índices ou constraints de unicidade quando a unicidade for local ao tenant;
- revise relacionamentos para impedir associação entre registros de tenants diferentes;
- avalie migration sempre que a estrutura persistida mudar.

Exemplos de unicidade local:

```text
(tenant_id, email_normalizado)
(tenant_id, codigo_do_servico)
(tenant_id, profissional_id, inicio, fim)
```

Os exemplos não são contratos prontos. Use apenas quando refletirem regras reais do módulo.

### 5. Proteja leituras e comandos

Toda operação tenant-owned deve aplicar o tenant antes de materializar dados.

Verifique especialmente:

- consultas por ID;
- paginação e busca textual;
- updates e deletes em lote;
- comandos que recebem IDs de entidades relacionadas;
- joins e projections;
- consultas raw SQL;
- acesso a registros arquivados ou soft-deleted;
- endpoints de download, relatório e exportação.

Uma consulta por identificador globalmente único ainda precisa respeitar o tenant. A improbabilidade de colisão de IDs não substitui autorização.

### 6. Trate processos sem HTTP

Jobs, schedulers, consumidores e rotinas de manutenção devem receber o tenant explicitamente no item de trabalho, registro ou mensagem.

- Persista ou transporte `tenant_id` junto com a operação.
- Não dependa da claim depois que a execução sair do ciclo HTTP.
- Revalide o escopo antes de carregar dados.
- Em processamento de múltiplos tenants, isole cada unidade de trabalho.
- Falha em um tenant não deve mudar silenciosamente o contexto do próximo.

### 7. Preserve tenant em integrações técnicas

Quando aplicável, inclua o tenant no escopo de:

- chave de cache;
- chave de idempotência;
- registro de Inbox ou Outbox;
- evento ou mensagem;
- nome lógico de arquivo exportado;
- lock distribuído;
- checkpoint de job.

Não use `tenant_id` como label de métrica de alta cardinalidade. Em logs estruturados, registre-o apenas quando necessário para diagnóstico e sem associá-lo a dados sensíveis desnecessários.

### 8. Teste isolamento

Todo fluxo persistente relevante deve usar pelo menos dois tenants.

Casos mínimos:

1. Tenant A cria e lê seu registro.
2. Tenant B não lê o registro do Tenant A.
3. Tenant B não altera nem exclui o registro do Tenant A.
4. Claim `tenant_id` ausente é rejeitada.
5. Claim inválida é rejeitada.
6. Enviar o Tenant A no body, rota, query ou header não contorna um token do Tenant B.
7. Unicidade local permite o mesmo valor em tenants diferentes quando essa for a regra.
8. Unicidade local bloqueia duplicidade dentro do mesmo tenant.
9. Relacionamentos cross-tenant são rejeitados.
10. Jobs e eventos processam o tenant correto sem `HttpContext`.

Prefira PostgreSQL real via Testcontainers quando o teste depender de constraints, índices, SQL, transações ou comportamento do provider.

## Checklist por tipo de mudança

### Novo endpoint

- A claim `tenant_id` é obrigatória?
- O endpoint usa o tenant autenticado, não o input do cliente?
- Todas as consultas e alterações estão limitadas ao tenant?
- Os códigos de erro não revelam a existência de registros de outro tenant?
- Há teste com dois tenants?

### Nova entidade ou tabela

- É dado de negócio?
- Possui `tenant_id` obrigatório?
- O tenant é imutável após criação?
- Índices únicos incluem tenant quando necessário?
- Relacionamentos impedem mistura de tenants?
- A migration está correta?

### Nova query

- O filtro de tenant é aplicado antes da materialização?
- Projections e joins mantêm o isolamento?
- Raw SQL inclui o tenant de forma parametrizada?
- Paginação e contagem usam o mesmo escopo?

### Novo job ou evento

- `tenant_id` está persistido ou transportado?
- A execução funciona sem contexto HTTP?
- Retry, idempotência e cache incluem tenant?
- Logs permitem identificar o tenant sem criar métrica de alta cardinalidade?

## Sinais de risco

Interrompa e revise o desenho quando encontrar:

- `tenant_id` vindo de DTO público e usado diretamente como autorização;
- repository com método `GetById` que não recebe nem aplica escopo de tenant;
- query administrativa reutilizada por endpoint comum;
- tabela de negócio sem `tenant_id`;
- índice único que ignora tenant apesar de a regra ser local;
- job que tenta acessar claim ou `HttpContext`;
- cache com chave sem tenant;
- update ou delete em lote sem filtro de tenant;
- teste usando apenas um tenant;
- tentativa de esconder registro cross-tenant somente no frontend;
- bypass de filtro implementado como flag booleana genérica.

## Decisões não autorizadas por esta skill

Não escolha automaticamente:

- tipo concreto de `TenantId`;
- Global Query Filters;
- EF Core interceptor;
- PostgreSQL Row-Level Security;
- schema por tenant;
- banco por tenant;
- provedor de identidade;
- modelo de impersonation ou suporte administrativo.

Quando uma dessas escolhas se tornar necessária, avalie contexto, riscos e alternativas e registre uma ADR complementar antes de generalizar a implementação.

## Saída esperada

Ao concluir a tarefa, informe:

- como o tenant foi resolvido;
- onde foi propagado;
- quais tabelas, queries, índices ou contratos foram afetados;
- quais testes de isolamento foram executados;
- quais mecanismos permanecem pendentes de decisão;
- qualquer risco residual de acesso cross-tenant.
