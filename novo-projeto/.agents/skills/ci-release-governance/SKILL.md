---
name: ci-release-governance
description: Use esta skill para revisar ou ajustar GitHub Actions, hooks, cobertura, versionamento, releases e automações do repositório. Não use para mudanças funcionais no backend sem impacto no pipeline.
---

# Objetivo

Evoluir CI/CD e automações com segurança, rastreabilidade e custo proporcional à maturidade do projeto.

## Quando usar

- Workflows em `.github/workflows/`.
- Actions compostas em `.github/actions/`.
- Hooks em `.githooks/`.
- Build, testes, cobertura e artifacts.
- Versionamento e Conventional Commits.
- CodeQL, dependency review e auditoria NuGet.
- Releases e publicação, quando houver estratégia definida.

## Princípios

- Comece com o pipeline mínimo que protege o projeto atual.
- Use permissões mínimas.
- Fixe actions por SHA quando possível.
- Evite jobs caros em toda alteração.
- Não exija secret para validar PR de fork.
- Não publique, faça deploy ou release durante uma validação comum.
- Mantenha CI e comandos locais equivalentes quando isso for viável.
- Não use o pipeline para esconder problemas de build ou testes.

## Pipeline inicial recomendado

- restore com auditoria NuGet;
- build Release;
- testes automatizados;
- coleta de cobertura sem gate prematuro;
- CodeQL;
- dependency review.

Adicione depois, somente quando necessário:

- gate de cobertura;
- OpenAPI lint;
- testes de container;
- testes end-to-end;
- análise de frontend;
- DAST;
- mutation testing;
- publicação de imagens;
- release automático;
- deploy.

## Processo

1. Identifique o objetivo e o evento que dispara a automação.
2. Leia workflows, hooks e documentação relacionados.
3. Compare comportamento documentado e configurado.
4. Revise permissões, secrets, triggers, concorrência, timeout e artifacts.
5. Preserve Central Package Management e comandos oficiais.
6. Evite duplicar lógica complexa em vários workflows.
7. Diferencie gate bloqueante de análise informativa.
8. Não rode testes de carga ou e2e caros em todo commit sem justificativa.
9. Valide YAML e comandos localmente quando possível.
10. Atualize documentação quando o fluxo oficial mudar.

## Segurança

- Não use secrets em `pull_request_target` com checkout de código não confiável.
- Não escreva tokens em logs.
- Use `permissions` explícitas e mínimas.
- Não amplie acesso de escrita sem justificativa.
- Não execute scripts de PR externo com credenciais privilegiadas.
- Não publique artifact contendo configuração sensível.

## Releases

Somente introduza release automático quando existirem:

- artefato definido;
- versionamento decidido;
- estratégia de rollback;
- ambiente de destino;
- credenciais protegidas;
- aprovação e documentação operacional.

## Restrições

- Não executar deploy, publish ou release real sem pedido explícito.
- Não remover validação para contornar falha.
- Não reduzir cobertura ou segurança silenciosamente.
- Não criar matriz, cache ou detecção de impacto complexa antes de haver ganho mensurável.
- Não alterar código de produção fora do escopo.
