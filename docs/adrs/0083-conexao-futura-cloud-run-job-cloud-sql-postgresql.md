# ADR-0083: Conexao futura do Cloud Run Job com Cloud SQL PostgreSQL

## Status
Proposto

## Data
2026-06-05

## Contexto
A ADR-0082 registra a etapa inicial de Cloud SQL PostgreSQL para
desenvolvimento local com Cloud SQL Auth Proxy. Essa etapa valida o banco
gerenciado, o Terraform dev e o smoke manual/local sem definir a arquitetura de
execucao gerenciada na GCP.

Quando a POC evoluir para executar tarefas operacionais ou migrations por Cloud
Run Job, o banco deixara de ser acessado apenas pela maquina local e passara a
ser usado por um workload gerenciado na GCP. Essa mudanca exige decisao propria
para identidade de runtime, IAM, secrets, rede, operacao, testes, deploy e
limites de conexao.

Esta ADR separa explicitamente o acesso local com Auth Proxy da direcao futura
para Cloud Run Job. O objetivo e evitar que uma solucao de desenvolvimento seja
tratada como arquitetura final de execucao gerenciada.

## Decisao
O Cloud Run Job devera acessar o Cloud SQL PostgreSQL usando service account
propria do Job, separada de operadores humanos, CI/CD, Terraform e outros
workloads da aplicacao.

A service account do Job devera receber apenas as permissoes necessarias para o
fluxo implementado. Quando aplicavel, isso inclui `roles/cloudsql.client` no
menor escopo viavel para conexao com Cloud SQL. Permissoes administrativas de
Cloud SQL, IAM, Secret Manager ou Terraform nao devem ser concedidas ao Job por
conveniencia.

Senhas, connection strings completas, nomes sensiveis e configuracoes que nao
devam ficar em texto claro no repositorio deverao ser fornecidos por Secret
Manager ou mecanismo equivalente da plataforma. Variaveis de ambiente do Job
podem referenciar esses valores, mas nao devem conter segredos versionados.

A conexao privada devera ser avaliada como caminho preferencial para ambientes
mais maduros. A escolha concreta entre private IP, Direct VPC egress,
Serverless VPC Access, Cloud SQL connector ou outra composicao suportada pela
plataforma devera ser decidida durante a implementacao da etapa do Job, com base
no desenho de rede vigente, custo, operabilidade e requisitos de seguranca.

A configuracao nao deve exigir `authorized_networks` abertas ou exposicao
publica desnecessaria do banco. O acesso via Cloud SQL Auth Proxy local
permanece como solucao de desenvolvimento e smoke manual/local, nao como
definicao final da arquitetura gerenciada.

A infraestrutura do Job e do banco devera deixar claro o ciclo de vida dos
recursos, os limites de responsabilidade entre modulos Terraform e o state
responsavel por cada recurso.

## Consequencias positivas
- Separa a solucao local da arquitetura futura de runtime gerenciado.
- Reduz risco de permissao excessiva ao exigir service account propria para o
  Cloud Run Job.
- Mantem segredos fora do repositorio e centraliza configuracoes sensiveis em
  Secret Manager ou mecanismo equivalente.
- Evita dependencia de `authorized_networks` amplas para workloads gerenciados.
- Preserva a possibilidade de conexao privada quando a rede estiver pronta.
- Torna explicitos os temas que devem ser resolvidos antes de executar carga
  real contra Cloud SQL.

## Consequencias negativas ou trade-offs
- A decisao aumenta o escopo de infraestrutura necessario antes de colocar o Job
  em operacao.
- Conexao privada pode exigir desenho de VPC, rotas, egress, conectores,
  custos adicionais e validacoes operacionais.
- Secret Manager e IAM adicionam dependencias que precisam ser provisionadas,
  auditadas e testadas.
- A estrategia de deploy passa a depender de coordenacao entre imagem do Job,
  variaveis, secrets, banco, migrations e rede.
- Testes automatizados comuns nao exercitarao todos os detalhes de Cloud SQL
  real, para evitar custo, instabilidade e dependencia de credenciais.

## Alternativas consideradas
1. Cloud Run Job conectando no IP publico do Cloud SQL.
   - Possivel tecnicamente, mas deve ser evitado quando criar exposicao publica
     desnecessaria ou incentivar `authorized_networks` amplas.

2. Cloud Run Job usando private IP.
   - Preferivel para ambientes mais maduros quando a VPC, o roteamento e o
     modelo de egress estiverem definidos.

3. Cloud Run Job usando Cloud SQL connector.
   - Opcao valida para simplificar autenticacao e conectividade, desde que seja
     compatibilizada com o desenho de rede, IAM minimo e operacao do runtime.

4. Manter acesso apenas local e nao conectar o Job ao banco.
   - Insuficiente para tarefas gerenciadas na GCP, migrations operacionais ou
     rotinas que precisem rodar fora da maquina da pessoa desenvolvedora.

5. Usar outro banco gerenciado ou PostgreSQL externo.
   - Fora da direcao atual da POC. Deve exigir ADR propria caso altere a
     estrategia de persistencia, operacao ou responsabilidade do banco.

## Impactos em seguranca
- O Cloud Run Job deve usar service account dedicada e com menor privilegio.
- O papel Cloud SQL Client deve ser concedido apenas quando necessario e no
  menor escopo viavel.
- Segredos devem vir de Secret Manager ou mecanismo equivalente, sem senhas em
  arquivos versionados, imagens, argumentos de comando ou logs.
- O banco nao deve depender de `authorized_networks` abertas para atender o Job.
- O acesso ao Terraform state deve permanecer separado da identidade do Job.
- Logs devem registrar falhas de conectividade sem expor connection strings,
  senhas, tokens ou valores sensiveis.

## Impactos em rede
- Conexao privada deve ser avaliada como caminho preferencial para ambientes
  mais maduros.
- A implementacao devera escolher entre private IP, Direct VPC egress,
  Serverless VPC Access, Cloud SQL connector ou combinacao suportada.
- A decisao de rede deve considerar latencia, custo, limites de throughput,
  roteamento, observabilidade e isolamento.
- A configuracao local com Auth Proxy continua separada da conectividade do
  runtime gerenciado.
- Exposicao publica do Cloud SQL deve ser evitada quando nao for estritamente
  necessaria.

## Impactos em deploy
- O deploy do Cloud Run Job devera declarar service account, variaveis de
  ambiente, referencias a secrets, configuracao de rede e limites operacionais.
- A infraestrutura deve deixar claro quais recursos pertencem ao ciclo de vida
  do Job, do banco e do state Terraform.
- A configuracao do Job nao deve depender de arquivos locais, credenciais de
  pessoa desenvolvedora ou Cloud SQL Auth Proxy local.
- A politica de migrations, quando houver EF Core migrations, deve ser definida
  antes de usar o Job para alteracoes de schema.
- Rollback deve considerar imagem do Job, configuracao, secrets, migrations,
  conectividade e compatibilidade com o schema.

## Impactos em testes
- Testes automatizados de integracao em CI devem priorizar PostgreSQL via
  Testcontainers, sem dependencia de Cloud SQL real.
- Pull requests comuns nao devem exigir recurso real na GCP, credenciais GCP ou
  conectividade com Cloud SQL, para evitar custo, instabilidade e falhas por
  ambiente.
- Smoke tests em GCP devem ser previstos como execucao manual ou pipeline
  controlado para validar conectividade do Cloud Run Job com Cloud SQL.
- Quando a etapa for implementada, o Job deve considerar health ou preflight de
  conectividade antes de processar carga real.
- Testes que usam Cloud SQL real devem ser explicitamente separados dos testes
  de integracao locais e ter dono, ambiente alvo e criterios de limpeza.

## Pendencias para implementacao
- Definir estrategia de rede.
- Definir uso de Secret Manager ou mecanismo equivalente.
- Definir service account dedicada do Cloud Run Job.
- Definir variaveis de ambiente e referencias a secrets do Job.
- Definir politica de migracao de banco, se houver EF Core migrations.
- Definir estrategia de rollback.
- Definir observabilidade e logs para falhas de conexao.
- Definir limites de conexao, pooling e concorrencia do Job.
- Definir ownership e ciclo de vida dos recursos Terraform do Job, do banco e
  dos secrets.
