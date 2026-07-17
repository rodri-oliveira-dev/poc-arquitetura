# Politica de Seguranca

Este repositorio e uma POC publica de arquitetura. Ele demonstra praticas de seguranca, mas nao deve ser tratado como sistema produtivo ou como promessa de operacao segura em ambiente real.

## Como reportar vulnerabilidades

Reporte vulnerabilidades de forma privada pelo recurso de security advisory do GitHub, quando disponivel no repositorio, ou por um canal privado acordado com o mantenedor `@rodri-oliveira-dev`.

Nao publique em issues, pull requests ou discussions:

- tokens, senhas, connection strings, certificados privados ou chaves;
- payloads com dados pessoais, financeiros ou de clientes reais;
- detalhes exploraveis antes de haver mitigacao ou decisao de divulgacao;
- logs completos que contenham headers de autorizacao, cookies ou secrets.

## Versoes suportadas

Por ser uma POC, apenas a branch `main` recebe triagem de seguranca. Branches de estudo, PRs antigos, tags historicas e forks nao sao mantidos como linhas suportadas.

## Prazo de resposta

O mantenedor tentara responder em ate 7 dias corridos. Esse prazo e uma meta de triagem, nao um SLA. A correcao depende de escopo, severidade, disponibilidade e impacto na POC.

## Limites da POC

Esta POC usa configuracoes locais, emuladores, containers e dados sinteticos. Antes de uso produtivo, revise identidade de workload, secrets, TLS, hardening de rede, rotacao de credenciais, gestao de vulnerabilidades, backups, observabilidade, resposta a incidentes e requisitos legais do ambiente alvo.
