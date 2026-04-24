# ADR-0022: Padronizar higiene de dependencias e containers

## Status
Proposto

## Contexto

O CI ja executa build, testes, coverage, CodeQL e dependency review. A analise local com `dotnet list ... package --vulnerable --include-transitive` encontrou avisos NuGet conhecidos: `OpenTelemetry.Api 1.15.0` com severidade moderada e `System.Security.Cryptography.Xml 9.0.0` transitivo com severidade alta nos projetos de infraestrutura.

Os Dockerfiles usam imagens base por tag (`mcr.microsoft.com/dotnet/*:10.0`) e nao declaram usuario nao-root na imagem final. O compose usa `postgres:16` e `apache/kafka:3.7.0`, expoe portas no host e nao define limites de recursos.

## Decisao proposta

Definir uma politica de higiene de supply chain e containers:

- adicionar verificacao NuGet vulneravel ao CI, com criterio de bloqueio ou excecao documentada;
- avaliar scan de imagens;
- preferir usuario nao-root em containers finais;
- definir estrategia de tags/digests para imagens base;
- documentar limites minimos de recursos e exposicao de portas por ambiente.

## Alternativas consideradas

- Confiar apenas em dependency review e CodeQL.
- Corrigir vulnerabilidades manualmente sob demanda, sem gate.
- Tratar hardening de container apenas na plataforma de producao.

## Consequencias positivas

- Reduz riscos OWASP de Vulnerable and Outdated Components e Security Misconfiguration.
- Aumenta previsibilidade de builds e imagens.
- Cria criterio objetivo para aceitar ou bloquear CVEs.

## Consequencias negativas / trade-offs

- Pode gerar falhas de CI por CVEs transitivas sem correcao imediata.
- Digests aumentam manutencao de atualizacao de imagens.
- Usuario nao-root pode exigir ajustes de permissao em arquivos e volumes.

## Riscos

- Criar gate muito restritivo para uma POC e travar experimentacao.
- Aceitar excecoes sem prazo e normalizar vulnerabilidades.
- Divergir politica NuGet da politica de imagens.

## Proximos passos sugeridos

- Registrar baseline atual de vulnerabilidades e excecoes temporarias.
- Definir severidade minima que bloqueia PR.
- Testar containers com usuario nao-root.
- Avaliar scan de imagem no CI ou em pipeline separado.
