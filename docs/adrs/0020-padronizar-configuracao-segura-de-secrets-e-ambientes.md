# ADR-0020: Padronizar configuracao segura de secrets e ambientes

## Status
Proposto

## Contexto

O repositorio contem valores de POC em `appsettings.json`, `compose.yaml` e README, incluindo usuarios e senhas de banco, credenciais fixas do Auth.Api e caminhos de chave RSA. A `.gitignore` evita versionar a chave gerada, mas os valores padrao ainda podem ser reutilizados por engano fora do ambiente local.

Como a POC avalia microservicos, autenticacao, Kafka e bancos, a separacao clara entre configuracao local e configuracao sensivel e um pre-requisito para evoluir o projeto.

## Decisao proposta

Adotar uma politica unica de configuracao:

- manter somente placeholders ou valores explicitamente locais em arquivos versionados;
- usar `.env.example` sem segredos para compose;
- usar user-secrets ou variaveis de ambiente para desenvolvimento no host;
- usar secret manager externo em ambientes compartilhados/produtivos;
- documentar quais valores sao obrigatorios por servico.

## Alternativas consideradas

- Manter segredos de POC versionados pela simplicidade local.
- Mover todos os valores para README.
- Exigir secret manager tambem para desenvolvimento local.

## Consequencias positivas

- Reduz risco OWASP de Security Misconfiguration e Cryptographic Failures.
- Evita promocao acidental de credenciais locais para ambientes reais.
- Facilita futura integracao com Aspire parameters/secrets.

## Consequencias negativas / trade-offs

- Aumenta passos iniciais de setup local.
- Exige atualizar scripts e documentacao.
- Pode quebrar automacoes se defaults forem removidos sem transicao.

## Riscos

- Criar configuracao mais segura, mas menos reprodutivel.
- Deixar valores obrigatorios sem validacao clara no startup.
- Duplicar segredo entre compose, Aspire e scripts.

## Proximos passos sugeridos

- Criar inventario de configuracoes por servico.
- Separar `.env.example` de `.env` local ignorado.
- Validar options no startup para falhar cedo quando faltar configuracao.
- Atualizar README com fluxo local sem versionar segredos reais.
