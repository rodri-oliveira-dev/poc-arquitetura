# k6 load tests

Este diretório contém scripts k6 usados pelos runners em `./scripts/run-loadtests.*`.

## Configuração

A configuração é carregada na ordem:

1. Variáveis de ambiente do k6 (`__ENV`)
2. Arquivo `.env.k6.auto` (gerado por `scripts/compose-env.*`)
3. Defaults (fallback para execução local via `localhost`)

Os cenários exigem `TOKEN` (JWT) a menos que `ALLOW_ANON=true`.
