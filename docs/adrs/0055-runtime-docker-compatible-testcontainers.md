# ADR-0055: Runtime Docker-compatible para Testcontainers

## Status
Aceito

## Data
2026-05-07

## Contexto
O projeto possui stack local baseada em `compose.yaml` e testes de integracao com Testcontainers PostgreSQL.

Havia referencias anteriores a `nerdctl` como CLI principal da stack local. Porem, o Testcontainers depende de uma Docker-compatible API acessivel, nao da CLI `nerdctl` ou `docker` em si.

No Windows sem Docker Desktop, `containerd/nerdctl` puro nao atende bem a esse requisito. Rancher Desktop com `moby/dockerd` expoe uma API compativel com Docker e permite executar Testcontainers sem tornar Docker Desktop obrigatorio.

## Decisao
Padronizar documentacao e scripts locais do projeto para comandos `docker` e `docker compose`.

Manter suporte a ambientes sem Docker Desktop desde que exponham uma Docker-compatible API.

Para Windows sem Docker Desktop, o ambiente recomendado e Rancher Desktop com:

- `moby/dockerd` como container engine;
- `DOCKER_HOST=npipe:////./pipe/docker_engine`.

Se o cliente .NET usado pelo Testcontainers rejeitar esse valor com erro de URI npipe invalida, o formato equivalente aceito pelo Docker.DotNet e `npipe://./pipe/docker_engine`.

`DOCKER_HOST` deve ser configurado no ambiente local do desenvolvedor, nunca de forma permanente no codigo da aplicacao.

## Consequencias
Scripts e documentacao deixam de depender de `nerdctl`.

Testcontainers passa a ter orientacao explicita de ambiente: Docker-compatible API acessivel, com Docker Desktop opcional.

`containerd/nerdctl` puro deixa de ser tratado como ambiente suportado para testes de integracao baseados em Testcontainers.

ADRs historicas que citam `nerdctl` permanecem como registro de decisoes substituidas, mas a orientacao vigente passa a ser esta ADR e `docs/development/local-development.md`.

## Beneficios
- Reduz ambiguidade entre CLI de container e API exigida pelo Testcontainers.
- Preserva compatibilidade com Windows sem Docker Desktop.
- Alinha stack local, scripts de carga e troubleshooting em torno de `docker compose`.
- Evita configurar variaveis locais como `DOCKER_HOST` dentro do codigo da aplicacao.

## Trade-offs / custos
- Desenvolvedores que usavam `nerdctl compose` precisam migrar o comando local para `docker compose`.
- A suite com Testcontainers continua dependente de runtime local corretamente configurado.
- Ambientes com apenas `containerd/nerdctl` podem continuar subindo containers manualmente, mas nao sao tratados como suportados para Testcontainers.

## Alternativas consideradas
1. **Manter `nerdctl compose` como comando principal**
   - Rejeitado porque reforca uma CLI que nao resolve a dependencia real do Testcontainers em Docker-compatible API.

2. **Exigir Docker Desktop**
   - Rejeitado porque o ambiente alvo inclui Windows sem Docker Desktop.

3. **Configurar `DOCKER_HOST` no codigo de teste**
   - Rejeitado porque acoplaria os testes a detalhes locais e dificultaria execucao em CI ou em outros runtimes compativeis.

4. **Substituir Testcontainers por mocks**
   - Rejeitado porque os testes atuais validam comportamento que depende de PostgreSQL real, migrations, transacoes e locking.
