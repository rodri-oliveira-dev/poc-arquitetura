# ADR-0026: Atualizar OpenTelemetry.Api vulneravel

## Status
Aceito

## Data
2026-04-25

## Contexto

`OpenTelemetry.Api` 1.15.0 aparece como pacote top-level vulneravel moderado nos projetos `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`, com propagacao transitiva para os testes que referenciam essas APIs. A vulnerabilidade `GHSA-g94r-2vxg-569j` / `CVE-2026-40894` afeta o processamento de headers de propagacao e pode causar alocacao excessiva de memoria.

O repositorio usa Central Package Management em `Directory.Packages.props`, e o CI ja executa `dotnet list ... package --vulnerable --include-transitive --format json`. Antes desta decisao, o gate bloqueava apenas vulnerabilidades `High` e `Critical`, deixando vulnerabilidades `Moderate` como acompanhamento manual.

## Decisao

Atualizar somente `OpenTelemetry.Api` de 1.15.0 para 1.15.3 em `Directory.Packages.props`, mantendo os demais pacotes OpenTelemetry nas versoes atuais para evitar atualizacao ampla sem necessidade.

Elevar a protecao de supply chain para bloquear vulnerabilidades NuGet `Moderate`, `High` e `Critical` no workflow `.github/workflows/dotnet.yml`. Ajustar tambem `.github/workflows/dependency-review.yml` para falhar em `moderate` ou superior.

Adicionar teste de politica em `DependencyPolicyTests` para garantir que a versao corrigida de `OpenTelemetry.Api` continue centralizada e que o workflow .NET bloqueie vulnerabilidades NuGet moderadas ou superiores.

## Consequencias

Nao ha alteracao de contrato HTTP, schema de banco, evento Kafka, autenticacao/autorizacao ou regra de dominio.

O CI passa a falhar para vulnerabilidades moderadas futuras em pacotes NuGet. Isso reduz risco de supply chain, mas pode exigir correcao mais rapida ou excecao documentada quando uma vulnerabilidade moderada transitiva nao tiver versao corrigida disponivel.

## Beneficios

- Remove a vulnerabilidade conhecida em `OpenTelemetry.Api` 1.15.0.
- Mantem a atualizacao pequena e centralizada.
- Reduz a chance de reintroducao de vulnerabilidades moderadas em dependencias NuGet.
- Alinha o dependency review com o gate NuGet do workflow principal.

## Trade-offs / custos

- O gate mais restritivo pode bloquear PRs por vulnerabilidades moderadas transitivas.
- Excecoes futuras devem ser tratadas explicitamente, em vez de passarem silenciosamente pelo CI.

## Alternativas consideradas

- Atualizar todo o ecossistema OpenTelemetry: rejeitado por ampliar o escopo sem exigencia de compatibilidade.
- Manter o CI bloqueando apenas `High` e `Critical`: rejeitado porque o caso atual demonstrou vulnerabilidade moderada top-level nos projetos de API.
- Remover `OpenTelemetry.Api` top-level dos projetos de API: rejeitado porque o pacote ja faz parte da configuracao de observabilidade existente e a correcao compativel esta disponivel.
