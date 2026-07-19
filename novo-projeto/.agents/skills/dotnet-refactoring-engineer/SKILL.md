---
name: dotnet-refactoring-engineer
description: Use esta skill para revisar, refatorar ou melhorar código .NET/C# com foco em legibilidade, manutenibilidade, testabilidade, segurança e performance. Não use para mudanças meramente estéticas ou para alterar comportamento sem requisito explícito.
---

# Objetivo

Orientar refatorações seguras e incrementais, preservando o comportamento observável e reduzindo riscos técnicos reais.

## Antes de alterar

1. Entenda o comportamento atual.
2. Identifique o problema concreto.
3. Localize testes relacionados.
4. Preserve contratos públicos, salvo pedido explícito.
5. Separe refatoração estrutural de mudança funcional.
6. Prefira mudanças pequenas e verificáveis.

## Uma refatoração deve melhorar ao menos um ponto

- clareza de intenção;
- coesão;
- redução de duplicação real;
- redução de acoplamento;
- testabilidade;
- complexidade cognitiva ou ciclomática;
- tratamento de erros;
- segurança;
- observabilidade;
- performance mensurável;
- remoção de código morto.

Não substitua uma preferência por outra sem ganho claro.

## C# e .NET

- Use `async` e `await` corretamente.
- Não bloqueie código assíncrono com `.Result`, `.Wait()` ou equivalentes.
- Propague `CancellationToken` quando a operação puder ser cancelada.
- Use `TimeProvider` para comportamento temporal testável.
- Evite estado global mutável.
- Prefira early return quando reduzir aninhamento.
- Não capture exceções genéricas sem ação útil.
- Preserve stack trace usando `throw;`.
- Use configuração tipada.
- Use logs estruturados e não registre dados sensíveis.

## Injeção de dependência

- Prefira constructor injection.
- Evite service locator.
- Revise lifetimes de `Singleton`, `Scoped` e `Transient`.
- Não injete serviços scoped diretamente em singletons.
- Muitas dependências podem indicar responsabilidade excessiva.
- Não crie interface para toda classe automaticamente.

## ASP.NET Core

- Mantenha endpoints finos.
- Use DTOs para contratos externos.
- Não exponha entidades de persistência.
- Preserve semântica dos verbos e códigos HTTP.
- Use paginação e filtros para coleções grandes.
- Não exponha detalhes internos em respostas de erro.
- Verifique autenticação e autorização em ações sensíveis.

## EF Core

- Use consultas assíncronas.
- Use `AsNoTracking()` em leituras quando adequado.
- Prefira projeções com `Select` quando não precisar da entidade completa.
- Evite materialização prematura.
- Revise N+1 e `Include` excessivo.
- Avalie índices e migrations quando a consulta ou schema mudar.
- Não use cache para esconder a causa de uma query ruim.

## Design

Use princípios e padrões como ferramentas, não como dogma.

- Strategy somente quando houver variação real.
- Factory quando a criação envolver decisão relevante.
- Decorator quando houver comportamento transversal sobre uma abstração.
- Adapter para integrações externas.
- Repository quando trouxer isolamento ou intenção de domínio.
- Prefira composição a herança.
- Não crie camadas ou abstrações sem consumidor claro.

## Testes

- Preserve ou aumente cobertura relevante.
- Teste comportamento, não detalhes internos.
- Crie testes de caracterização antes de uma mudança arriscada quando necessário.
- Teste sucesso, falha e efeitos colaterais relevantes.
- Não torne métodos públicos apenas para testar.
- Evite mocks excessivos quando um teste de integração for mais confiável.

## Segurança e performance

Procure por:

- SQL construído com entrada externa;
- falta de autorização;
- exposição de segredos ou PII;
- chamadas externas sequenciais desnecessárias;
- queries dentro de loops;
- coleções sem paginação;
- materialização antecipada;
- falta de cancelamento;
- serialização excessiva;
- logs ruidosos em caminhos quentes.

Não otimize prematuramente. Explique o ganho esperado.

## Processo

1. Inspecione estrutura, framework e arquivos próximos.
2. Localize testes.
3. Produza diagnóstico curto quando a tarefa for ampla.
4. Aplique mudanças pequenas e coesas.
5. Execute build e testes relevantes.
6. Revise o diff para evitar churn.
7. Explique o que mudou, por quê e como foi validado.

## Restrições

- Não alterar regra de negócio sem evidência ou instrução.
- Não reduzir severidade de analyzer para contornar problema.
- Não introduzir dependência sem justificativa.
- Não fazer refatoração cosmética em massa.
- Não alterar contrato público por conveniência interna.
