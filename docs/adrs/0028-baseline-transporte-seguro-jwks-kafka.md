# ADR-0028: Baseline de transporte seguro para JWKS e Kafka

## Status
Aceito

## Data
2026-04-25

## Contexto
`LedgerService.Api` e `BalanceService.Api` consomem JWKS do `Auth.Api` para validar JWTs. A execucao local da POC usa HTTP para JWKS, `RequireHttpsMetadata=false`, Kafka `PLAINTEXT` e portas de banco/Kafka expostas no host para facilitar desenvolvimento, migrations e load tests.

Essas configuracoes sao aceitaveis no ambiente local isolado, mas nao devem ser aceitas por padrao em ambientes compartilhados, homologacao, staging ou producao. Nesses ambientes, transporte inseguro amplia risco de interceptacao de chaves publicas, downgrade operacional e exposicao indevida de trafego Kafka.

## Decisão
Padronizar a linha de base de transporte seguro:

- `Jwt:JwksUrl` deve usar HTTPS fora de `Development`/`Local`;
- `Jwt:RequireHttpsMetadata=false` e JWKS via HTTP sao permitidos apenas em `Development`/`Local`;
- `Test` tambem e aceito como excecao tecnica para `WebApplicationFactory` e testes automatizados em memoria;
- Kafka `Plaintext` e compose com Kafka/bancos expostos no host ficam restritos ao ambiente local da POC;
- `LedgerService.Infrastructure` e `BalanceService.Infrastructure` rejeitam Kafka `Plaintext` fora de `Development`/`Local`/`Test`;
- os clientes Kafka passam a mapear `SecurityProtocol`, `SslCaLocation`, `SaslMechanism`, `SaslUsername` e `SaslPassword` para permitir `SSL` ou `SASL_SSL` em ambientes nao locais;
- readiness Kafka usa a mesma configuracao de seguranca dos clientes de producer/consumer.

Nao sera implementada nesta ADR uma infraestrutura completa de certificados, CA, secrets ou broker seguro. Esses itens devem ser fornecidos pela plataforma/operacao do ambiente compartilhado.

## Consequências

### Benefícios
- Evita subir APIs em ambientes nao locais com JWKS HTTP ou `RequireHttpsMetadata=false`.
- Evita Kafka `PLAINTEXT` fora da excecao local.
- Mantem a ergonomia da POC local, compose, scripts e load tests.
- Abre caminho simples para `SSL`/`SASL_SSL` sem adicionar novos pacotes.

### Trade-offs / custos
- Ambientes compartilhados precisam configurar JWKS HTTPS e Kafka seguro antes de iniciar os servicos.
- O compose local continua inseguro por desenho e nao deve ser promovido como base produtiva.
- A configuracao de certificados e secrets permanece externa ao repositorio.

### Riscos aceitos
- O ambiente local continua usando HTTP/JWKS, Kafka `Plaintext` e portas expostas no host.
- O ambiente `Test` permite as mesmas excecoes para evitar dependencias externas nos testes de integracao.
- Load tests e scripts continuam assumindo execucao local/compose e URLs HTTP locais.
- Erros de configuracao de certificados ou credenciais Kafka serao detectados em runtime pelo cliente Kafka/plataforma, nao provisionados pelo projeto.

## Alternativas consideradas

1) **Exigir HTTPS/JWKS e Kafka SSL tambem em Development**
   - Pros: politica uniforme.
   - Contras: aumenta muito o custo da POC local e exigiria infraestrutura de certificados fora do escopo.

2) **Apenas documentar a restricao local**
   - Pros: nenhuma mudanca de codigo.
   - Contras: nao impede execucao acidental insegura em ambiente compartilhado.

3) **Implementar provisionamento completo de certificados e SASL**
   - Pros: ambiente seguro de ponta a ponta no repositorio.
   - Contras: extrapola o escopo, adiciona complexidade operacional e nao e necessario para a POC atual.
