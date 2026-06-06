# ADR-0082: Cloud SQL PostgreSQL para desenvolvimento local com Auth Proxy

## Status
Aceito

## Data
2026-06-05

## Contexto
O repositorio ja possui Terraform em `infra/terraform/environments/dev` para
recursos GCP do fluxo Pub/Sub. A evolucao do ambiente dev passa a incluir um
PostgreSQL gerenciado para validar conectividade, migrations e operacao minima
em Cloud SQL sem transformar essa etapa na arquitetura final de producao.

O primeiro acesso ao banco PostgreSQL na GCP sera feito pela maquina local da
pessoa desenvolvedora. Neste momento, a decisao e nao iniciar com VPC privada,
Cloud Run Job ou conectividade privada. Tambem nao e aceitavel liberar
`authorized_networks` amplas, versionar senhas, credenciais, `terraform.tfvars`
real, state ou planos binarios.

A stack local continua tendo PostgreSQL via Docker Compose e testes de
integracao com PostgreSQL local por Testcontainers. Cloud SQL entra como alvo de
smoke manual/local, nao como dependencia obrigatoria de CI.

## Decisao
Criar Cloud SQL PostgreSQL por Terraform, integrado ao root module dev
`infra/terraform/environments/dev`, usando modulo reutilizavel dedicado para
Cloud SQL.

O modulo deve criar a instancia PostgreSQL, o database e o usuario de
aplicacao, sem expor senha em outputs. Senhas devem ser fornecidas por fonte
local ignorada pelo Git, como `terraform.tfvars` real fora do controle de
versao ou variavel de ambiente sensivel.

Para desenvolvimento local, o acesso ao Cloud SQL deve usar Cloud SQL Auth
Proxy:

- em debug no host, a aplicacao usa `Host=127.0.0.1;Port=5432`;
- em Docker Compose, a aplicacao usa `Host=cloud-sql-proxy;Port=5432`;
- o proxy pode escutar em `0.0.0.0` dentro do container para ser acessivel pela
  rede Compose;
- quando publicado no host, o proxy deve ficar preso a `127.0.0.1`, por exemplo
  `127.0.0.1:5432:5432`;
- credenciais locais do proxy devem ser montadas por volume e nunca versionadas.

Nao liberar `authorized_networks` amplas e nao usar `0.0.0.0/0`. Nao iniciar
com VPC privada nesta etapa. Cloud Run Job, conexao privada e modelo de
execucao de migrations em ambiente gerenciado ficam para decisao futura.

## Consequencias positivas
- A infraestrutura de banco dev passa a ser reprodutivel por Terraform.
- O modulo de Cloud SQL fica separado do modulo de Pub/Sub, reduzindo
  acoplamento entre recursos com ciclos de vida diferentes.
- O ambiente dev concentra a composicao dos recursos GCP atuais sem duplicar
  root modules prematuramente.
- O acesso local usa Cloud SQL Auth Proxy, evitando abrir o banco por
  `authorized_networks` amplas.
- A senha do banco nao aparece em outputs Terraform nem em documentacao
  versionada.
- O Docker Compose passa a ter um fluxo claro para smoke manual contra Cloud
  SQL sem usar `localhost` dentro dos containers.

## Consequencias negativas ou trade-offs
- A decisao nao representa a arquitetura final de producao.
- O Cloud SQL pode gerar custo mesmo em ambiente dev.
- O Terraform state pode conter a senha do `google_sql_user`, mesmo que ela nao
  seja exposta em outputs. O state deve ser tratado como artefato sensivel e
  restrito.
- Sem VPC privada, a conexao inicial depende do Auth Proxy e da conectividade
  publica controlada pelo Cloud SQL, sem `authorized_networks` amplas.
- O uso do proxy adiciona um processo local ou servico Compose a ser mantido
  durante o smoke manual.
- A execucao de migrations contra Cloud SQL exige cuidado explicito com projeto,
  instancia, usuario e ambiente alvo.

## Alternativas consideradas
1. Conectar diretamente no IP publico com `authorized_networks`.
   - Rejeitado porque aumenta a superficie de exposicao, tende a incentivar
     liberacoes amplas e contradiz o objetivo de evitar `0.0.0.0/0`.

2. Criar VPC privada ja na primeira etapa.
   - Adiado porque aumenta escopo, custo operacional e dependencias de rede
     antes de validar o caminho minimo de banco gerenciado para dev.

3. Usar PostgreSQL apenas local via Docker Compose.
   - Rejeitado como solucao unica porque nao valida conectividade, IAM, Auth
     Proxy, Terraform e operacao minima com Cloud SQL.

4. Usar Cloud SQL Auth Proxy apenas fora do Compose.
   - Rejeitado porque deixaria sem fluxo documentado a validacao das APIs e
     workers em containers apontando para Cloud SQL.

5. Criar tudo no mesmo modulo do Pub/Sub.
   - Rejeitado porque Pub/Sub e Cloud SQL possuem responsabilidades, riscos,
     custos e ciclos de vida diferentes.

## Impactos em seguranca
- Credenciais reais, chaves JSON, `.env`, `terraform.tfvars`, state local,
  planos binarios e connection strings com senha nao devem ser versionados.
- O proxy no Docker Compose deve publicar porta no host apenas em `127.0.0.1`.
- Dentro da rede Compose, a aplicacao deve usar `cloud-sql-proxy`, nao
  `localhost`.
- `authorized_networks` amplas continuam proibidas para acesso local.
- A identidade usada pelo Auth Proxy deve ter permissao minima, normalmente
  Cloud SQL Client no escopo adequado.
- O state remoto precisa de IAM restrito, versionamento e cuidado operacional,
  pois pode conter dados sensiveis de recursos gerenciados.

## Impactos em testes
- Testes de integracao automatizados devem preferir PostgreSQL local via
  Testcontainers quando o objetivo for validar comportamento da aplicacao.
- CI nao deve exigir Cloud SQL real, `gcloud`, credenciais GCP ou Auth Proxy
  para passar.
- Conexao real com Cloud SQL deve ser smoke test manual/local documentado.
- `terraform validate`, TFLint e Trivy continuam como gates de infraestrutura.
- Validacoes de Compose devem usar `docker compose config` para confirmar a
  composicao efetiva e que a porta do proxy nao foi publicada em `0.0.0.0` no
  host.

## Impactos em operacao local
- O debug local no host usa `127.0.0.1:5432` quando o Auth Proxy roda fora de
  container.
- O Docker Compose com Cloud SQL usa `compose.cloudsql.yaml` e connection
  string com `Host=cloud-sql-proxy;Port=5432`.
- A pessoa desenvolvedora deve manter credenciais locais fora do repositorio e
  montar o arquivo no container do proxy quando usar Compose.
- O PostgreSQL local do Compose continua disponivel para desenvolvimento
  cotidiano e para testes que nao precisam de Cloud SQL.
- Smoke contra Cloud SQL deve confirmar projeto, instancia, database, usuario e
  ambiente antes de aplicar migrations ou executar chamadas de escrita.

## Relacao com decisao futura do Cloud Run Job
Esta ADR cobre apenas desenvolvimento local e smoke manual com Cloud SQL Auth
Proxy. A execucao de migrations ou tarefas operacionais em Cloud Run Job, a
conectividade privada, as service accounts de runtime, Secret Manager, IAM de
menor privilegio em workloads gerenciados e a estrategia de rede para ambientes
nao locais devem ser registradas em ADR futura.
