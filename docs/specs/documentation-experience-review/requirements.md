# Revisao da experiencia de documentacao - requisitos

## Contexto

A documentacao do repositorio cresceu junto com a POC. Ela contem material valioso, mas parte da experiencia ficou fragmentada: README longo demais, indice documental pouco orientado por jornada, specs e ADRs competindo com guias atuais, e algumas referencias antigas sobre `Auth.Api`, `AuditService` e fluxos futuros.

Esta especificacao registra uma revisao editorial e tecnica da documentacao, sem alterar codigo de producao.

## Objetivos

- Transformar a documentacao em uma jornada de aprendizado.
- Manter a documentacao confiavel em relacao ao codigo atual.
- Separar documentacao atual, ADRs historicas, specs SDD, relatorios e runbooks.
- Reduzir duplicacao no README e no indice documental.
- Ajudar quatro perfis de leitor: iniciante, desenvolvedor .NET, arquiteto experiente e avaliador tecnico.
- Preservar ADRs e specs como historico, sem reescrever decisoes antigas como se sempre tivessem sido atuais.

## Nao objetivos

- Alterar comportamento dos servicos.
- Regenerar OpenAPI sem mudanca de contrato HTTP.
- Remover ADRs historicas.
- Criar documentacao para funcionalidade inexistente.
- Declarar prontidao produtiva.
- Fazer push, merge ou release.

## Requisitos funcionais

### RF-01 - Porta de entrada

O README da raiz deve responder rapidamente:

- o que e o projeto;
- para quem ele e util;
- qual problema demonstra;
- quais conceitos ensina;
- como comecar;
- quais limites da POC precisam ser entendidos.

### RF-02 - Jornada de leitura

A documentacao deve oferecer, no minimo:

- jornada rapida;
- jornada de iniciante;
- jornada de desenvolvedor;
- jornada arquitetural;
- jornada operacional.

### RF-03 - Taxonomia documental

O indice deve distinguir:

- tutorial;
- how-to;
- explicacao conceitual;
- referencia;
- ADR;
- especificacao SDD;
- runbook.

### RF-04 - Inventario documental

A revisao deve inventariar documentos Markdown do repositorio com:

- caminho;
- titulo;
- publico provavel;
- tipo documental;
- objetivo;
- tamanho aproximado;
- dificuldade;
- situacao atual;
- sobreposicao;
- links enviados;
- possivel obsolescencia;
- acao recomendada.

### RF-05 - Consistencia tecnica

Descricoes sobre IdentityService, Keycloak, Kafka, Pub/Sub, Outbox, Inbox, DLQ, replay, PaymentService, AuditService, OpenAPI, CI, cobertura, SonarQube, CodeQL, Trivy, TimeProvider, CORS, forwarded headers e rate limiting devem ser conferidas contra codigo, scripts, workflows e documentos de contrato.

### RF-06 - Runbooks

Runbooks devem deixar claro:

- sintoma;
- diagnostico;
- decisao;
- execucao;
- validacao;
- riscos;
- limites.

### RF-07 - Historico preservado

ADRs e specs historicas podem receber ajustes de navegacao e clareza, mas nao devem ter sua decisao original reescrita silenciosamente.

## Requisitos nao funcionais

- Linguagem profissional, direta e natural.
- Paragrafos curtos.
- Pouca duplicacao.
- Links suficientes para navegar, sem excesso em cada paragrafo.
- Documentos atuais separados de historico de implementacao.
- ASCII em novos arquivos desta revisao, seguindo a regra local de edicao.

## Criterios de aceite

- README mais curto, claro e com jornada de leitura.
- `docs/README.md` reorganizado por jornada e taxonomia.
- Spec SDD criada em `docs/specs/documentation-experience-review/`.
- Relatorio final com inventario, alteracoes, validacoes, riscos e proximos passos.
- Links relativos principais validados.
- Validacoes disponiveis registradas.
