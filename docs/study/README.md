# Estudos orientados

Este documento organiza uma lista inicial de estudos derivados das personas do repositorio.

Os estudos foram pensados para o laboratorio local e para evolucao incremental do dominio, da arquitetura, dos testes e da documentacao.

## Itens iniciais

1. [Matriz funcional de Ledger e Balance](001-matriz-funcional-ledger-balance.md)
2. [Relatorio local de divergencia da projecao](002-relatorio-local-divergencia-projecao.md)
3. [Historico de validacoes](003-historico-validacoes.md)
4. [Metricas para observabilidade](004-observabilidade-metricas.md)
5. [Cobertura orientada a risco](005-cobertura-orientada-a-risco.md)
6. [Contratos de erro e status codes](006-contratos-erros-status-codes.md)

## Ordem sugerida

1. Comecar pelo estudo 001 para consolidar o entendimento funcional.
2. Usar o estudo 005 para escolher testes de maior valor antes de mexer em comportamento sensivel.
3. Avancar para o estudo 006 para melhorar previsibilidade de contrato.
4. Depois escolher entre os estudos 002, 003 e 004 conforme o foco tecnico do momento.

## Observacao

As personas ficam em `docs/personas`. O objetivo e manter uma leitura dupla: Product Owner para clareza funcional e Arquiteto de Software para trade-offs, fronteiras tecnicas e pontos de aprendizado.
