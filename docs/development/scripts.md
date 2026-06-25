# Política de wrappers de scripts

Os scripts operacionais foram reorganizados em subpastas de `scripts/` por finalidade. Novas documentações, automações, comandos de CI, scripts npm e tasks do VS Code devem usar os caminhos novos.

Os arquivos antigos diretamente em `scripts/` continuam existindo como wrappers de compatibilidade. Eles podem atender onboarding, snippets antigos, branches antigas e uso manual, mas não devem aparecer como caminho recomendado em documentação nova.

Alguns wrappers serão mantidos indefinidamente. Outros são elegíveis para remoção futura, sempre em tarefa explícita e depois de busca global confirmar que não há dependência documental ou automatizada fora dos próprios wrappers.

## Caminhos atuais

- `scripts/local/`: onboarding, certificados locais, stack local, Kafka/Pub/Sub, full stack e stop.
- `scripts/docker/`: diagnóstico e limpeza segura Docker.
- `scripts/quality/`: Sonar, mutation testing e validações de qualidade.
- `scripts/quality/terraform/`: validação Terraform.
- `scripts/security/`: OWASP ZAP.
- `scripts/performance/`: k6/load tests.
- `scripts/contracts/openapi/`: geração e diff de contratos OpenAPI.
- `scripts/contracts/events/`: validação de contratos de eventos.
- `scripts/validation/`: tokens e fluxos manuais/smoke.
- `scripts/lib/`: helpers compartilhados.

## Classificação

### Manter indefinidamente

Wrappers de alto valor operacional, onboarding ou compatibilidade externa:

- `check-openapi-breaking-changes.sh`
- `generate-openapi.ps1`
- `generate-openapi.sh`
- `run-loadtests.ps1`
- `run-loadtests.sh`
- `run-owasp-zap.ps1`
- `run-owasp-zap.sh`
- `start-local-stack.ps1`
- `start-local-stack.sh`
- `start-local-stack-kafka.ps1`
- `start-local-stack-kafka.sh`
- `start-local-stack-pubsub.ps1`
- `start-local-stack-pubsub.sh`
- `validate-event-contracts.mjs`
- `validate-terraform.ps1`
- `validate-terraform.sh`

### Manter por enquanto

Wrappers temporarios mantidos por compatibilidade silenciosa e baixo custo:

- `docker-clean-safe.ps1` e `docker-clean-safe.sh`: diagnostico/limpeza Docker local quando o ambiente fica preso.
- `docker-disk-report.ps1` e `docker-disk-report.sh`: diagnostico Docker local sem custo relevante de manutencao.
- `generate-local-certs.ps1` e `generate-local-certs.sh`: nome antigo ainda ajuda descoberta manual de certificados locais.
- `get-token.ps1` e `get-token.sh`: util para fluxos autenticados manuais e snippets antigos.
- `init-env-local.ps1` e `init-env-local.sh`: onboarding local e descoberta de `.env.local`.
- `start-full-stack.ps1` e `start-full-stack.sh`: comando operacional para Nginx/full stack.
- `stop-full-stack.ps1` e `stop-full-stack.sh`: par operacional simples para encerrar a full stack.

### Removidos nesta revisao

- `sonar-analyze.sh`: sem referencia ao caminho antigo, sem papel de onboarding e duplicava apenas `scripts/quality/sonar-analyze.sh`.

### Elegiveis para remocao futura

No momento, nao ha wrappers pendentes nessa classificacao.
