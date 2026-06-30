# Baseline de evolucao produtiva

Este documento define um baseline recomendado para orientar uma evolucao futura do projeto para um ambiente GCP mais proximo de producao.

Ele e uma referencia arquitetural. Nao significa que o projeto esta pronto para producao, nao declara conformidade produtiva e nao implementa infraestrutura nova nesta etapa. Cada item depende de ambiente, decisao futura, revisao de risco, budget, IAM aprovado e automacao proporcional.

## Escopo

Este baseline cobre requisitos para evolucao produtiva de seguranca, identidade de workload, trafego, Kafka como broker padrao, Pub/Sub real como alternativa explicita se escolhida por decisao futura, Cloud SQL, containers, borda, observabilidade, operacao, compliance e governanca.

Fora do escopo desta etapa:

- criar Terraform novo;
- alterar workflows;
- alterar Dockerfiles;
- alterar codigo;
- criar secrets;
- assumir projeto GCP real configurado;
- declarar que a POC esta pronta para producao.

## Status resumido

| Tema | Status | Evidencia atual | Direcao recomendada |
| --- | --- | --- | --- |
| Stack local de servicos | Ja existe no projeto | `compose.yaml`, `docs/development/local-development.md` | Manter como laboratorio local, sem tratar como ambiente produtivo. |
| IdentityService | Ja existe no projeto | `src/identity`, ADR-0089 a ADR-0095 | Manter cadastro/vinculo local de usuarios separado do IdP; avaliar Outbox/worker para e-mail apenas se entrega duravel virar requisito. |
| Kafka local | Ja existe no projeto | `compose.yaml`, `docs/development/kafka-outbox.md`, ADR-0088 | Tratar Kafka como broker padrao dos workers principais e definir seguranca/operacao antes de qualquer ambiente compartilhado. |
| Pub/Sub emulator | Existe apenas localmente | `docs/operations/pubsub.md` | Usar somente para desenvolvimento e testes locais. |
| Pub/Sub real dev | Documentado, mas nao automatizado como producao | `infra/terraform/environments/dev`, `docs/development/pubsub-infra-app-contract.md` | Usar apenas como alternativa explicita/legada ou estudo GCP; separar configuracao produtiva futura se uma ADR escolher Pub/Sub para algum fluxo. |
| Cloud SQL dev e Auth Proxy local | Documentado, mas nao automatizado como producao | `docs/development/cloudsql-postgres-local-setup.md`, modulo Terraform Cloud SQL | Definir conectividade, usuarios, backups, retention, migrations e pooling por ambiente. |
| Secrets produtivos | Pendente | ADR-0020 e guias locais proibem secrets versionados | Adotar Secret Manager ou equivalente, rotacao e acesso minimo por workload. |
| Identidade de workload | Parcialmente documentado | IAM minimo Pub/Sub dev e sugestao de impersonation local | Adotar service accounts por aplicacao ou funcao, Workload Identity ou equivalente, sem chaves long-lived. |
| TLS externo | Documentado, mas nao automatizado como producao | Nginx HTTPS local simula borda | Usar load balancer, gateway ou plataforma gerenciada com certificados aprovados. |
| TLS interno | Pendente | Nao ha baseline produtivo implementado | Avaliar por servico, rede, runtime e requisitos de compliance. |
| Nginx local | Existe apenas localmente | `compose.nginx.yaml`, `infra/nginx/`, ADRs de borda local | Manter como simulacao local, nao como desenho produtivo final. |
| Scans de filesystem, IaC e secrets | Ja existe no projeto | Trivy em hook e workflow, `docs/development/trivy-security-scan.md` | Complementar com scan de imagem publicada e politica de excecoes. |
| Scan de imagem container | Pendente | Trivy atual nao faz build de imagem | Adicionar etapa futura para imagens geradas e armazenadas em registry. |
| SBOM e assinatura de imagem | Pendente | Nao ha politica versionada | Avaliar como etapa futura junto de supply chain e Artifact Registry. |
| WAF e rate limit por identidade | Pendente | Rate limits locais existem como hardening de API, sem WAF produtivo | Definir protecao por rota, identidade, tenant, origem e abuso. |
| Observabilidade local | Ja existe no projeto | `docs/observability.md`, dashboards, alertas locais | Projetar backend produtivo, alertas e dashboards por SLO operacional futuro. |
| E-mail transacional | Parcialmente documentado | ADR-0092, ADR-0093, ADR-0094, ADR-0095 | Usar Mailpit apenas localmente; usar Resend por secret/configuracao quando envio real for desejado; decidir Outbox/retry/DLQ antes de tratar envio como critico. |
| DLQ, replay e rebuild | Documentado, mas nao automatizado como producao | Runbooks em `docs/operations/` | Criar automacao controlada, auditoria e permissoes antes de uso produtivo. |
| OpenAPI e contratos de eventos | Ja existe no projeto | `docs/openapi/`, `docs/events/`, `contracts/events/` | Manter validacao de contrato como requisito de mudanca. |
| Terraform produtivo | Pendente | Terraform atual e dev/controlado | Criar desenho separado somente depois de decisao de ambiente. |
| Auth.Api legado | Fora de escopo | README e ADRs indicam Keycloak como caminho principal local | Nao recolocar no baseline produtivo. |

## Seguranca de secrets

Baseline recomendado:

- nao usar secrets em `appsettings` versionado, Compose versionado, scripts, exemplos reais, documentacao, Terraform state ou outputs sensiveis;
- usar Secret Manager ou mecanismo equivalente aprovado pelo ambiente;
- separar secrets por ambiente, aplicacao e funcao operacional;
- conceder acesso minimo por workload, evitando permissao ampla no projeto;
- definir rotacao, responsavel, janela de troca e plano de rollback por tipo de segredo;
- registrar quais secrets podem ser lidos por API, Worker, processo de migration, operador e CI/CD;
- evitar imprimir connection strings, tokens, senhas e valores de secret em logs, summaries, artifacts ou dashboards.

O projeto ja documenta o uso de `dotnet user-secrets`, variaveis de ambiente locais e arquivos ignorados para desenvolvimento. Isso nao deve ser confundido com governanca produtiva de secrets.

No IdentityService, `IdentityProvider:Keycloak:ClientSecret` e `Resend:ApiKey` sao secrets operacionais. Eles devem continuar fora do repositorio e fora de exemplos com valores reais.

## Identidade de workload e IAM

Baseline recomendado:

- usar service accounts separadas por aplicacao ou funcao, por exemplo API, Worker publisher, Worker subscriber, job de migration e automacao operacional;
- preferir Workload Identity, Workload Identity Federation ou mecanismo equivalente da plataforma;
- evitar chaves JSON long-lived e remover qualquer fluxo que dependa de chave persistida em repositorio, artifact, imagem ou estacao de trabalho;
- aplicar IAM minimo por recurso, nao permissao ampla no projeto;
- conceder ao publisher apenas publish no topic necessario;
- conceder ao subscriber apenas consume ou ack na subscription necessaria;
- conceder publish em DLQ somente ao componente que classifica e publica mensagens na DLQ de aplicacao;
- conceder permissoes Cloud SQL apenas ao workload que precisa conectar;
- conceder permissoes de observabilidade somente para exportar logs, metricas e traces necessarios;
- revisar periodicamente bindings, membros humanos, service accounts inativas e permissoes temporarias.

O contrato atual de Pub/Sub dev modela IAM minimo para publisher, subscriber, DLQ de aplicacao e Pub/Sub service agent no modo alternativo/legado. Um ambiente produtivo futuro deve definir primeiro se usara Kafka, Pub/Sub ou ambos por decisao arquitetural e entao reavaliar nomes, fronteiras, retencao, residencia, impersonation e auditoria antes de reutilizar qualquer desenho.

## Kafka produtivo futuro

Baseline recomendado:

- definir cluster, topicos, particionamento, retencao, replication factor e ownership por ambiente;
- configurar TLS/SASL ou mecanismo equivalente aprovado, sem `Plaintext` fora do local;
- conceder ACLs minimas por producer, consumer group e topico;
- definir DLQ de aplicacao, politica de retry, commit e redrive operacional;
- monitorar lag de consumer, idade de mensagens, erro de publish, erro de commit, DLQ e backlog de Outbox;
- documentar estrategia de schema/compatibilidade antes de novos consumidores.

O Kafka local em KRaft e laboratorio de desenvolvimento. Ele nao substitui decisao produtiva sobre broker gerenciado, operacao, seguranca, multi-AZ, backups de configuracao, capacidade, alertas ou runbooks.

## TLS e trafego

Baseline recomendado:

- expor trafego externo somente por HTTPS/TLS com certificados gerenciados ou processo aprovado de emissao e renovacao;
- definir onde ocorre a terminacao de TLS, por exemplo load balancer, gateway, ingress ou plataforma gerenciada;
- proteger chamadas internas com TLS quando aplicavel ao runtime, rede, regulacao ou fronteira de confianca;
- revisar headers de proxy, `X-Forwarded-*`, `X-Correlation-Id`, host original e logging de origem;
- habilitar HSTS apenas quando o dominio, certificados, rollback e politica de subdominios estiverem maduros;
- manter CORS restrito por ambiente e origem real, sem usar wildcard em ambiente compartilhado ou produtivo;
- documentar certificados, responsaveis, renovacao, monitoramento de expiracao e procedimento de troca.

O Nginx local com HTTPS simula borda para desenvolvimento e testes. Ele nao representa por si so uma arquitetura produtiva de gateway, WAF, certificado, balanceamento global ou protecao contra abuso.

## Pub/Sub real alternativo

Baseline recomendado:

- criar topics e subscriptions por ambiente, com nomes, labels e ownership claros;
- configurar DLQ de aplicacao e DLQ tecnica conforme a decisao operacional do ambiente;
- definir retry policy, ack deadline, retention, expiration policy e exatamente uma estrategia de redelivery;
- habilitar ordering key apenas quando o fluxo exigir ordenacao por agregado e quando publishers e subscriptions estiverem alinhados;
- conceder IAM por publisher e subscriber, sem permissao compartilhada ampla;
- separar permissao de inspecao de DLQ da permissao de redrive;
- definir alerta para backlog, idade da mensagem mais antiga, crescimento de DLQ, erro de publish, erro de ack e falhas de schema;
- documentar diferencas entre emulator local e Pub/Sub real.

O emulator local nao usa credenciais GCP, nao reproduz todos os limites do servico real e nao configura a dead-letter policy nativa usada por recursos reais. Para GCP real, remova `PUBSUB_EMULATOR_HOST`, use identidade de workload e alinhe options dos workers aos outputs aprovados da infraestrutura.

## Cloud SQL

Baseline recomendado:

- conectar por Cloud SQL Auth Proxy, connector ou mecanismo equivalente aprovado;
- usar private IP quando fizer sentido para rede, seguranca, latencia e custo;
- evitar `authorized_networks` amplo e nunca liberar `0.0.0.0/0`;
- separar bancos, schemas, usuarios e permissoes por servico conforme o desenho de banco por microservico;
- armazenar senhas e credenciais no mecanismo de secrets aprovado;
- definir backups, retention, PITR quando necessario, janela de manutencao e procedimento de restore testado;
- executar migrations como etapa operacional controlada, com identidade propria e rollback planejado;
- configurar pooling de conexoes conforme runtime, Cloud SQL, EF Core, numero de instancias e workers;
- monitorar conexoes, CPU, memoria, storage, locks, queries lentas, erros de autenticacao e saturacao de pool.

O guia atual de Cloud SQL e voltado a smoke local e desenvolvimento controlado com Auth Proxy. Ele nao substitui desenho produtivo de rede, backup, HA, usuarios, migracoes e operacao.

## Containers e imagens

Baseline recomendado:

- garantir build reprodutivel, com versoes fixadas pelo repositorio e sem depender de estado local do desenvolvedor;
- publicar imagens com tags imutaveis associadas a commit SHA, release ou digest;
- evitar tag mutavel como unica referencia de deploy;
- executar scan de imagem depois do build e antes do rollout;
- manter Trivy ou ferramenta equivalente para filesystem, IaC, secrets e imagem;
- revisar base images, ciclo de atualizacao e vulnerabilidades conhecidas;
- rodar containers como usuario nao root sempre que a imagem e o runtime permitirem;
- remover ferramentas desnecessarias da imagem runtime;
- avaliar SBOM como etapa futura de supply chain;
- avaliar assinatura de imagem e verificacao por policy como etapa futura.

O Trivy atual valida configuracoes, filesystem e secrets versionados. Ele nao comprova que uma imagem publicada em registry foi escaneada, assinada ou aprovada para deploy produtivo.

## WAF e borda

Baseline recomendado:

- usar gateway, load balancer ou servico de borda aprovado para TLS externo, roteamento e politicas;
- aplicar rate limits por identidade quando houver token valido, por exemplo subject, client, merchant ou tenant;
- aplicar fallback por IP ou origem apenas como camada complementar, considerando NATs e clientes compartilhados;
- definir protecao por rota, metodo, escopo e criticidade;
- proteger endpoints administrativos, replay, rebuild, DLQ e requeue com politica separada;
- revisar headers de seguranca, CORS, HSTS, tamanho de request, timeouts e limites de body;
- registrar bloqueios, throttling e anomalias com baixa cardinalidade;
- criar procedimento de excecao temporaria com prazo e responsavel.

Rate limit local e Nginx local ajudam a exercitar conceitos, mas nao substituem WAF, protecao distribuida, politicas por identidade e observabilidade de abuso em ambiente real.

## Observabilidade

Baseline recomendado:

- manter logs estruturados com `CorrelationId`, servico, ambiente, status e causa operacional;
- evitar IDs de alta cardinalidade como labels de metricas ou labels de Loki;
- exportar metricas de HTTP, runtime, banco, Pub/Sub, Outbox, DLQ, retries, backlog e processamento de workers;
- exportar traces distribuidos quando houver OpenTelemetry habilitado, preservando contexto HTTP, Outbox e mensageria;
- manter `X-Correlation-Id` como identificador operacional separado de `TraceId`;
- criar alertas minimos para indisponibilidade, erro HTTP, readiness, falha de publish, backlog, DLQ, erro de migration, saturacao de conexoes e falha de exportacao critica;
- criar dashboards para APIs, workers, banco, Pub/Sub, Outbox, DLQ, consumo, latencia e recursos;
- tratar DLQ e Outbox como sinais operacionais de confiabilidade, nao apenas tabelas ou topicos auxiliares.

A stack atual de observabilidade e local. Um ambiente produtivo futuro deve decidir backend gerenciado ou auto hospedado, retencao, custo, acesso, alertas, on-call e mascaramento de dados sensiveis.

## Operacao

Baseline recomendado:

- separar rollout de API e Worker para reduzir risco de duplicidade de processamento ou incompatibilidade de contrato;
- definir estrategia de rollback para API, Worker, migrations, configuracao e imagem;
- executar migrations de forma controlada, com verificacao de compatibilidade entre versoes antigas e novas;
- garantir que mudancas de contrato sejam backward compatible enquanto houver mensagens antigas, backlog ou replay esperado;
- documentar replay, redrive, descarte de DLQ e rebuild de projecao com autorizacao, dry-run, limites e auditoria;
- validar readiness e health checks por tipo de processo;
- criar runbooks para incidentes de banco, Pub/Sub, DLQ, Outbox, saturacao de workers, queda de IdP, certificados e secrets expirados;
- definir criterios de congelamento, comunicacao, janela de manutencao e validacao pos rollout.

Os runbooks atuais cobrem investigacao e decisao operacional para replay, DLQ e rebuild em nivel de POC. Para uso produtivo, eles precisam de automacao controlada, auditoria persistente, permissoes e validacao operacional em ambiente real.

## Compliance e governanca

Baseline recomendado:

- registrar ADRs para decisoes relevantes de ambiente, rede, secrets, IAM, Pub/Sub, Cloud SQL, observabilidade, WAF, supply chain e operacao;
- revisar permissoes periodicamente, incluindo membros humanos, CI/CD e workloads;
- revisar dependencias, imagens base, vulnerabilidades e excecoes aprovadas;
- validar contratos HTTP e eventos antes de rollout;
- manter estrategia de ambientes, por exemplo local, dev, staging e producao, com dados, secrets, IAM, state Terraform e politicas separados;
- definir ownership de recursos, tags, labels, budget, alertas de custo e ciclo de vida;
- documentar processo de aprovacao para mudancas de infraestrutura, seguranca e operacao.

Este baseline nao cria uma nova ADR porque nao escolhe uma implementacao produtiva especifica. Quando uma decisao concreta for tomada, por exemplo WAF escolhido, mecanismo de Workload Identity, topologia Cloud SQL ou estrategia de deploy, registre ADR propria.

## Checklist para evolucao futura

Antes de tratar qualquer ambiente como candidato a producao, confirme:

- secrets fora do repositorio, com rotacao e acesso minimo;
- service accounts por workload, sem chaves long-lived;
- IAM minimo para Pub/Sub, Cloud SQL, observabilidade e operacao;
- TLS externo definido e certificados monitorados;
- decisao explicita sobre TLS interno;
- Pub/Sub real com topics, subscriptions, DLQ, retry, retention, ordering key e alertas;
- Cloud SQL com conexao segura, backup, retention, restore testado, migrations e pooling;
- imagens com tags imutaveis, scan, base image revisada e usuario nao root quando aplicavel;
- WAF ou gateway com rate limit por identidade e protecao por rota;
- logs, metricas, traces, dashboards e alertas minimos;
- runbooks de rollback, rollout, replay, DLQ, rebuild, readiness e health checks;
- contratos HTTP e eventos validados;
- ADRs e estrategia de ambientes atualizadas.

## Referencias internas

- [Documentacao arquitetural](README.md)
- [Roadmap arquitetural consolidado](../roadmap.md)
- [Maturidade tecnica](../maturity.md)
- [Operacao do Pub/Sub](../operations/pubsub.md)
- [Contrato Pub/Sub entre infraestrutura e aplicacao](../development/pubsub-infra-app-contract.md)
- [Cloud SQL PostgreSQL local com Auth Proxy](../development/cloudsql-postgres-local-setup.md)
- [Validacao de seguranca com Trivy](../development/trivy-security-scan.md)
- [Observabilidade e operacao minima](../observability.md)
- [Runbook de recuperacao de eventos](../operations/event-recovery-runbook.md)
- [Estrategia operacional de DLQ](../operations/dlq-strategy.md)
- [Estrategia operacional de replay seguro](../operations/replay-strategy.md)
- [Rebuild de projecao do Balance](../operations/projection-rebuild.md)
- [ADRs](../adrs/README.md)
