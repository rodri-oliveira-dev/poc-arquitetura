# ADR-0032: Baseline de seguranca de containers

## Status
Aceito

## Data
2026-04-26

## Contexto
Os Dockerfiles finais das APIs publicavam a aplicacao em imagens `aspnet` sem declarar usuario nao-root. O compose local tambem usava imagens por tag sem digest, mantinha `grafana/k6:latest` no override de carga e nao declarava limites explicitos de CPU, memoria ou processos.

Como o repositorio e uma POC executada principalmente via `nerdctl compose`, o baseline precisa melhorar a postura de seguranca sem quebrar a ergonomia local, as portas documentadas, os scripts de carga e a aplicacao manual de migrations.

## Decisao
Adotar o seguinte baseline minimo para containers da POC:

- executar `Auth.Api`, `LedgerService.Api` e `BalanceService.Api` como usuario nao-root no estagio final dos Dockerfiles usando o `APP_UID` disponibilizado pelas imagens oficiais .NET;
- copiar os artefatos publicados com ownership do usuario da aplicacao e preparar os diretorios gravaveis necessarios, incluindo `/data` no `Auth.Api`;
- manter o `Auth.Api` com volume nomeado em `/data` no compose para evitar dependencia de permissao de bind mount local;
- definir limites locais de CPU, memoria e processos em `compose.yaml` e `compose.k6.yaml` com `deploy.resources.limits`, conforme validado no `nerdctl compose` usado pelo repositorio;
- substituir `grafana/k6:latest` por uma tag explicita;
- manter imagens base e de infraestrutura por tags versionadas, sem digest, nesta POC;
- exigir digest pinning em ambientes compartilhados/produtivos ou pipelines de publicacao, usando uma lista/lock de imagens por plataforma;
- documentar scan local de imagens como validacao recomendada antes de publicar ou promover imagens.

Arquivos afetados:

- `src/Auth.Api/Dockerfile`
- `src/LedgerService.Api/Dockerfile`
- `src/BalanceService.Api/Dockerfile`
- `compose.yaml`
- `compose.k6.yaml`
- `README.md`
- `tests/LedgerService.UnitTests`

## Consequencias

### Beneficios
- Reduz impacto de escape ou execucao indevida dentro dos containers das APIs.
- Evita drift para imagens `latest` nos testes de carga.
- Limita consumo acidental de recursos na stack local.
- Mantem a execucao local com `nerdctl compose up -d --build`.
- Cria testes automatizados para impedir regressao do baseline.

### Trade-offs / custos
- Usuario nao-root pode exigir atencao extra quando novos volumes gravaveis forem adicionados.
- Limites locais podem precisar de ajuste em maquinas menores ou em cenarios de carga mais agressivos.
- Tags sem digest permanecem mutaveis e nao garantem reprodutibilidade binaria perfeita.
- O scan de imagem continua documentado como validacao operacional, nao como gate automatizado no CI.

## Alternativas consideradas

1. **Fixar todas as imagens por digest no compose local**
   Pros: maior reprodutibilidade e protecao contra tag mutavel.
   Contras: aumenta manutencao para uma POC multi-plataforma e pode quebrar maquinas com arquitetura diferente se o digest nao for gerenciado por plataforma.

2. **Usar `cpus` e `mem_limit` no nivel do servico**
   Pros: campos diretos e historicamente usados por compose local.
   Contras: o `nerdctl compose` do ambiente sinaliza esses campos como depreciados e recomenda `deploy.resources.limits`.

3. **Manter bind mount local para a chave do Auth.Api**
   Pros: facilita inspecao do arquivo no host.
   Contras: com usuario nao-root, a permissao do diretorio do host pode variar por sistema operacional e quebrar a primeira execucao.
