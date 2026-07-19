---
name: dotnet-service-change
description: Use esta skill ao alterar código de backend .NET, incluindo APIs, Application, Domain, Infrastructure, EF Core, autenticação, configuração e testes relacionados. Não use para mudanças apenas em governança, CI/CD puro ou documentação sem impacto funcional.
---

# Objetivo

Orientar alterações pequenas e seguras no backend .NET, preservando fronteiras arquiteturais, contratos, Central Package Management e validação proporcional.

## Quando usar

- Endpoints HTTP, controllers, minimal APIs, middlewares, binds, mappers ou OpenAPI.
- Handlers, casos de uso, validators e serviços de aplicação.
- Entidades, Value Objects, aggregates, políticas e eventos de domínio.
- EF Core, DbContext, mappings, migrations, transações e repositories.
- Autenticação, autorização, policies e headers de segurança.
- Workers, tarefas agendadas, idempotência ou integrações externas.
- Testes ligados diretamente ao comportamento alterado.

## Processo

1. Identifique módulo, caso de uso e camada afetada.
2. Consulte `AGENTS.md`, documentação e ADRs relevantes.
3. Localize contratos, DI, persistência, observabilidade e testes relacionados.
4. Verifique impacto em:
   - contrato HTTP;
   - segurança;
   - schema e migrations;
   - concorrência e idempotência;
   - frontend e OpenAPI;
   - logs, traces e métricas;
   - documentação.
5. Aplique a menor alteração coerente.
6. Preserve as fronteiras:
   - API trata transporte;
   - Application coordena casos de uso;
   - Domain protege regras e invariantes;
   - Infrastructure concentra detalhes técnicos.
7. Propague `CancellationToken` em operações assíncronas relevantes.
8. Use `TimeProvider` quando o comportamento temporal precisar ser controlável em testes.
9. Revise o diff para evitar refatoração ou formatação fora do escopo.
10. Execute testes proporcionais ao risco.

## Validação

```bash
dotnet tool restore
dotnet restore ./<Solution>.slnx
dotnet build ./<Solution>.slnx --configuration Release --no-restore
dotnet test ./<Solution>.slnx --configuration Release --no-build --no-restore --settings ./coverlet.runsettings
```

Substitua `<Solution>` pelo arquivo real encontrado no repositório.

## Restrições

- Não adicione `Version=` em `PackageReference`.
- Não mova regra de negócio para a borda HTTP ou infraestrutura.
- Não coloque EF Core, SQL, HTTP ou mensageria no Domain.
- Não altere migrations antigas sem necessidade explícita.
- Não altere testes apenas para fazê-los passar.
- Não introduza segredos, URLs, portas ou contratos inventados.
- Não crie microsserviço ou mensageria sem requisito real.
