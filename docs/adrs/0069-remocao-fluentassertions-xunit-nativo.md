# ADR-0069: Remocao do FluentAssertions e padronizacao em xUnit nativo

## Status

Aceito

## Data

2026-05-22

## Contexto

O repositorio usava `FluentAssertions` nos projetos de teste para melhorar a legibilidade de asserts, equivalencia de objetos, validacao de excecoes e verificacoes de colecoes.

A partir da linha 8 do Fluent Assertions, o uso comercial passa a exigir licenca paga. A documentacao oficial informa que a versao 7 permanece open source, mas versoes 8 e superiores sao gratuitas apenas para projetos open source e uso nao comercial. Como esta POC deve permanecer simples, reproduzivel e com baixo risco de governanca de dependencias, manter o pacote fixado indefinidamente na versao 7 criaria uma dependencia sem caminho natural de atualizacao.

O framework de testes do repositorio ja e xUnit v3, que fornece asserts nativos suficientes para a maior parte dos cenarios existentes, incluindo igualdade, nulidade, booleanos, colecoes, tipos, excecoes assincronas e `Assert.Equivalent`.

## Decisao

Remover completamente `FluentAssertions` dos projetos de teste e padronizar novas assertions em APIs nativas do xUnit.

Regras adotadas:

- nao adicionar nova biblioteca de assertion para substituir o FluentAssertions;
- remover `PackageReference` e versao central de `FluentAssertions`;
- migrar `Should()` para `Assert.*` do xUnit;
- usar `Assert.Equivalent(expected, actual)` quando a equivalencia estrutural preservar a intencao do teste;
- preferir asserts explicitos quando a equivalencia estrutural puder esconder diferencas semanticas;
- usar `Assert.ThrowsAny<T>` quando o comportamento anterior de `Should().Throw<T>()` aceitava excecoes derivadas;
- substituir `WithMessage("*texto*")` por `Assert.Contains` ou `Assert.Matches`, conforme a intencao do teste;
- nao introduzir helper interno generico de assertions sem necessidade clara.

## Alternativas consideradas

1. **Manter FluentAssertions 7.x fixado**
   - Rejeitado porque preserva o uso atual no curto prazo, mas congela uma dependencia de teste sem caminho simples para atualizacoes futuras.

2. **Atualizar para FluentAssertions 8+ e aceitar licenca comercial**
   - Rejeitado para esta POC porque adiciona custo e gestao de licenca a uma dependencia que nao e essencial para o comportamento do produto.

3. **Migrar para outra biblioteca fluent/fork**
   - Rejeitado porque manteria uma dependencia externa apenas para sintaxe de assertions e poderia criar novo risco de licenciamento, manutencao ou divergencia futura.

4. **Usar somente xUnit nativo**
   - Aceito por reduzir dependencias, custos e superficie de governanca, mantendo asserts suficientes para a suite atual.

## Consequencias positivas

- Remove risco de custo/licenciamento associado ao caminho de atualizacao do FluentAssertions 8+.
- Reduz dependencias de teste e simplifica `Directory.Packages.props`.
- Mantem Central Package Management sem pacote de assertion adicional.
- Evita warnings/licencas comerciais em execucoes de teste futuras.
- Alinha os testes ao framework ja adotado pelo repositorio.

## Consequencias negativas / trade-offs

- Algumas asserts ficam mais verbosas que a sintaxe fluent.
- O xUnit nao possui equivalentes diretos para alguns encadeamentos como `ContainSingle().Which`, `WithMessage("*...*")`, `BeCloseTo` e `AssertionScope`.
- Falhas de equivalencia ou mensagens de excecao podem ser menos expressivas em alguns casos, exigindo asserts granulares para preservar diagnostico.
- Desenvolvedores precisam evitar `Assert.True(a == b)` quando houver API mais expressiva, como `Assert.Equal`.

## Riscos

- Migracoes mecanicas podem alterar semantica se `Throw<T>` for trocado por `Assert.Throws<T>` em casos que antes aceitavam excecoes derivadas.
- `Assert.Equivalent` nao cobre todas as configuracoes customizadas possiveis de `BeEquivalentTo`; usos futuros devem ser avaliados caso a caso.
- A ausencia de uma API fluent pode reduzir legibilidade em testes com objetos complexos se nao houver cuidado na organizacao dos asserts.

## Validacao aplicada

- Build da solution em Release.
- Testes completos da solution com `coverlet.runsettings`.
- Busca textual por residuos de `FluentAssertions`, `.Should()`, `AssertionScope`, `BeEquivalentTo` e `NotBeEquivalentTo` nos testes e em `Directory.Packages.props`.
- Verificacao de pacotes transitivos com `dotnet list ./LedgerService.slnx package --include-transitive`.

## Referencias

- [Fluent Assertions - Licensing](https://fluentassertions.com/introduction/#licensing)
- [Fluent Assertions - pagina inicial](https://fluentassertions.com/)
