# Revisao C4/LikeC4 - requirements

## Objetivo

Revisar a documentacao arquitetural C4/LikeC4 para garantir que ela represente a implementacao atual do repositorio, sem promover roadmap, ADR historica ou desejo arquitetural a estado implementado.

## Requisitos funcionais

1. O modelo deve representar os bounded contexts implementados: `LedgerService`, `BalanceService`, `TransferService`, `PaymentService`, `IdentityService` e `AuditService`.
2. APIs, Workers, schemas PostgreSQL, Kafka, Pub/Sub legado/opcional, Keycloak, Stripe, Mailpit/Resend, Nginx local e observabilidade devem aparecer no nivel C4 correto.
3. `Application`, `Domain`, `Infrastructure` e projetos `Shared` nao podem ser tratados como processos implantaveis.
4. O caminho Kafka deve ser apresentado como default dos fluxos principais; Pub/Sub deve permanecer isolado como legado/opcional para Ledger/Balance.
5. O `IdentityService` nao deve ser documentado como emissor de tokens; Keycloak e o IdP e emissor JWT/JWKS.
6. O `PaymentService` deve aparecer como dono do ciclo de vida do pagamento externo, webhook/Inbox e materializacao no Ledger via HTTP, sem publicar evento financeiro direto no Kafka.
7. O `AuditService.Worker` deve ser representado como consumer Kafka implementado de `AuditRecordRequested.v1`, deixando claro que os demais dominios ainda nao possuem producers reais.
8. Views de contexto, container, componente, deployment e dynamic devem responder perguntas diferentes e evitar diagramas gigantes como primeira leitura.
9. A documentacao textual deve orientar jornadas de leitura para iniciantes, desenvolvedores, arquitetos, fluxos e operacao.
10. A revisao deve registrar inventario, divergencias, decisoes, validacoes, riscos residuais e arquivos alterados.

## Requisitos nao funcionais

- Manter a documentacao em portugues e ASCII.
- Evitar novas ferramentas quando comandos existentes ja validarem o modelo.
- Evitar duplicidade manual entre Mermaid, imagens estaticas e LikeC4.
- Preservar ADRs historicas e specs anteriores como historico, sinalizando drift em documentos atuais.
- Validar com comandos proporcionais: build LikeC4 e testes arquiteturais.

## Fora de escopo

- Alterar codigo de producao ou testes de aplicacao para combinar com diagramas.
- Gerar OpenAPI, pois nenhum contrato HTTP foi alterado.
- Publicar GitHub Pages, fazer push, merge ou release.
- Criar infraestrutura produtiva real.

## Criterios de aceite

- `npm run architecture:build` deve concluir com sucesso.
- Testes arquiteturais devem ser executados ou ter limitacao registrada.
- O relatorio deve explicar estado inicial, divergencias, correcoes e riscos residuais.
- `docs/README.md` deve apontar para esta spec SDD.
