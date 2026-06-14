# Estudo 001 - Matriz funcional de Ledger e Balance

## Leitura da Product Owner

### Contexto

O repositorio possui documentacao tecnica madura, contratos HTTP e eventos documentados. O proximo passo de estudo e consolidar uma visao funcional curta que conecte lancamentos, estornos, eventos, Outbox e projecao de saldo.

### Objetivo de negocio

Facilitar a escolha de proximos estudos a partir de uma leitura clara do dominio.

### Historia de usuario

Como mantenedor do repositorio, quero uma matriz funcional dos fluxos principais para entender impacto, regras conhecidas e duvidas antes de alterar codigo.

### Criterios de aceite

- Criar um documento em `docs/domain/ledger-balance-functional-map.md`.
- Mapear fluxos de escrita, publicacao de evento, consumo e consulta de saldo.
- Listar entradas, saidas, regras conhecidas e efeitos observaveis.
- Marcar duvidas de negocio sem inventar comportamento nao documentado.
- Linkar o documento em `docs/README.md`.

## Leitura do Arquiteto

### Abordagem tecnica

Este estudo deve ser apenas documental. Ele cria base para evoluir DDD discovery, linguagem ubiqua, boundaries e modelagem de casos de uso sem misturar implementacao.

### Conceitos praticados

- DDD discovery.
- Linguagem ubiqua.
- CQRS com projecao assincrona.
- Outbox transacional.
- Documentacao como fonte de decisao.

### Escopo sugerido de PR

Somente documentacao. Nao alterar codigo, migrations, workflows ou contratos.

### Classificacao

Util, mas opcional.
