# ADR-0025: Gestao de dependencias vulneraveis

## Status
Aceito

## Contexto

O projeto usa Central Package Management em `Directory.Packages.props` e valida dependencias no CI com restore, build, testes, CodeQL e dependency review. A execucao de `dotnet list .\LedgerService.slnx package --vulnerable --include-transitive` identificou `System.Security.Cryptography.Xml 9.0.0` como dependencia transitiva vulneravel de severidade alta nos projetos de infraestrutura e em testes que os referenciam.

A origem transitiva observada nos assets de restore foi a cadeia de tooling/design do EF Core, em especial pacotes `Microsoft.Build`/`Microsoft.Build.Tasks.Core` trazidos por `Microsoft.EntityFrameworkCore.Design`. Nao ha alteracao de contrato HTTP, schema de banco, evento Kafka ou regra de dominio envolvida.

## Decisao

Adotar a seguinte politica para vulnerabilidades NuGet:

- vulnerabilidades `High` e `Critical` devem bloquear o CI;
- vulnerabilidades `Low` e `Moderate` devem ser registradas e priorizadas conforme impacto, sem bloqueio automatico nesta POC;
- overrides de versao devem ser centralizados em `Directory.Packages.props`;
- quando a vulnerabilidade for transitiva e o pacote raiz nao puder ser atualizado isoladamente sem ampliar o escopo, a versao corrigida pode ser promovida por `PackageReference` explicito sem `Version=`, usando a versao central;
- excecoes temporarias para `High` ou `Critical` devem ter ADR ou issue vinculada, justificativa, dono e prazo maximo de 30 dias;
- o CI deve executar `dotnet list ... package --vulnerable --include-transitive --format json` e falhar quando encontrar severidade `High` ou `Critical`.

Para corrigir o caso atual, `System.Security.Cryptography.Xml` foi fixado centralmente em versao corrigida e referenciado explicitamente apenas nos projetos Infrastructure afetados, com `PrivateAssets="all"`, preservando Central Package Management e evitando `Version=` nos `.csproj`.

## Alternativas consideradas

- Atualizar todos os pacotes EF Core/Microsoft.Extensions: rejeitado por ampliar o escopo e tocar pacotes nao relacionados.
- Habilitar pinagem transitiva global do NuGet: rejeitado para evitar mudancas amplas de resolucao em toda a solucao.
- Aceitar a vulnerabilidade temporariamente: rejeitado por haver versao corrigida disponivel.

## Consequencias positivas

- Remove a vulnerabilidade alta conhecida de `System.Security.Cryptography.Xml 9.0.0`.
- Mantem a versao corrigida centralizada e rastreavel.
- Cria gate objetivo para novas vulnerabilidades `High` e `Critical`.

## Consequencias negativas / trade-offs

- Os projetos Infrastructure passam a declarar uma referencia explicita a um pacote usado para corrigir uma dependencia transitiva.
- O gate pode falhar por vulnerabilidades transitivas futuras antes de haver atualizacao do pacote raiz.

## Riscos

- Dependencias moderadas continuam permitidas pelo gate e precisam de acompanhamento separado.
- Excecoes temporarias podem perder validade se nao forem acompanhadas por issue/ADR com prazo.
