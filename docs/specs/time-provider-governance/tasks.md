# TimeProvider e governanca residual - tarefas

## Inventario

- [x] Buscar usos de `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, `SystemClock`, `IClock`, parametros opcionais de relogio e fallbacks internos.
- [x] Identificar composition roots e testes que substituem relogio.

## Especificacao

- [x] Separar requisitos de tempo e governanca.
- [x] Definir status canonicos de ADR.

## Design

- [x] Adotar `TimeProvider` diretamente.
- [x] Manter tipos temporais existentes quando fazem parte do contrato ou da persistencia atual.
- [x] Evitar abstracao propria nova.

## Implementacao

- [x] Migrar handlers, services, repositories e workers relevantes de `IClock` para `TimeProvider`.
- [x] Remover `IClock/SystemClock` dos contextos migrados.
- [x] Ajustar testes com providers fixos.
- [x] Criar arquivos de governanca.
- [x] Adicionar validador simples de ADRs.

## Testes

- [x] Build agregado em Release.
- [ ] Testes agregados completos com cobertura.

## Documentacao

- [x] Registrar requisitos, design, tarefas e relatorio SDD.
- [x] Documentar contribuicao, seguranca e ownership.
