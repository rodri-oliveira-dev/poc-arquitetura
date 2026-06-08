---
name: test-anti-patterns
description: Use esta skill para auditar qualidade de testes .NET neste repositorio, encontrando anti-patterns como asserts fracos, ausencia de asserts, flakiness, over-mocking, acoplamento a implementacao, dependencia de ordem, sleeps, dados magicos e cobertura artificial. Nao use para escrever testes novos do zero ou migrar framework.
license: MIT
---

# Objetivo

Orientar uma revisao pragmatica de testes automatizados para aumentar confianca, diagnostico e manutenibilidade.

Esta skill nao busca apenas mais testes ou mais cobertura. Ela busca testes que realmente verifiquem comportamento relevante e falhem pelos motivos certos.

# Quando usar

- O usuario pedir auditoria, revisao ou melhoria de qualidade dos testes.
- Testes estiverem passando, mas dando pouca confianca.
- Houver flakiness, dependencia de ordem, sleeps ou instabilidade.
- Houver suspeita de over-mocking, asserts fracos ou testes acoplados a detalhes internos.
- Um PR alterar testes de forma ampla.
- Cobertura parecer artificial ou inflada.

# Quando nao usar

- Escrever testes novos do zero sem foco em auditoria.
- Rodar testes apenas para validar build.
- Medir cobertura pura sem avaliar qualidade.
- Migrar framework de testes.
- Corrigir codigo de producao sem relacao com os testes.

# Regras obrigatorias

- Nao altere testes apenas para faze-los passar.
- Nao aceite teste sem assert significativo como valido.
- Nao torne metodo de producao publico apenas para facilitar teste.
- Nao remova asserts, cenarios ou verificacoes para reduzir flakiness sem corrigir a causa.
- Nao introduza sleeps arbitrarios; prefira sincronizacao deterministica.
- Nao crie dependencia de ordem entre testes.
- Nao use banco, fila, rede, Compose ou Testcontainers quando um teste unitario ou integracao mais simples cobrir o risco real.
- Preserve padroes existentes do projeto e combine com `integration-tests-dotnet` quando o problema for estrategia de integracao.

# Anti-patterns criticos

## Sem assert significativo

Teste executa codigo, mas nao verifica resultado, estado, excecao, publicacao de evento, persistencia ou efeito observavel.

## Assert tautologico

O teste compara uma variavel com ela mesma, replica a implementacao ou valida apenas o mock configurado pelo proprio teste.

## Coverage touching

O teste chama metodos apenas para executar linhas e aumentar cobertura, sem verificar comportamento relevante.

## Assert fraco demais

Exemplos: apenas `NotNull` em objeto complexo, apenas status code sem verificar contrato importante, ou apenas contagem sem validar conteudo quando o conteudo e o comportamento real.

## Swallowed exception

`try/catch` esconde falhas ou so chama `Assert.Fail` no `catch`, fazendo o teste passar quando nada relevante foi validado.

## Over-mocking

O teste mocka quase tudo e passa a validar a propria configuracao dos mocks, nao o comportamento do sistema.

## Acoplamento a implementacao

Teste quebra por renomeacao interna, ordem de chamadas irrelevante, estrutura privada ou detalhes que nao fazem parte do contrato observavel.

## Flakiness por tempo ou ambiente

Teste depende de `Task.Delay`, horario real, ordem de execucao, porta fixa, rede externa, banco compartilhado, data atual ou estado global.

## Dados magicos

Valores importantes aparecem sem nome, sem intencao e sem conexao clara com a regra testada.

# Processo

1. Identifique o projeto de teste e o comportamento que deveria ser protegido.
2. Leia o codigo de producao relacionado quando necessario para entender o contrato observavel.
3. Classifique problemas por severidade: critica, alta, media ou baixa.
4. Separe problema de teste ruim de problema de design no codigo de producao.
5. Sugira menor ajuste seguro para cada achado.
6. Quando a correcao exigir teste de integracao, consulte `integration-tests-dotnet`.
7. Quando houver relacao com cobertura, consulte `coverage-analysis`.
8. Valide com o projeto de teste afetado quando houver mudanca.

# Saida esperada

- Achados agrupados por severidade.
- Explicacao do risco de falso positivo ou falso negativo.
- Sugestao objetiva de correcao.
- Separacao entre melhoria necessaria e melhoria opcional.
- Validacoes executadas ou motivo para nao executar.

# Criterio de qualidade

Um teste bom deve deixar claro qual comportamento protege, preparar dados intencionais, executar uma acao observavel e verificar resultado ou efeito com asserts relevantes.
