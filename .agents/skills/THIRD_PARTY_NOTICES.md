# Third-party notices for Codex skills

Este arquivo documenta as fontes externas usadas como base para skills adicionadas em `.agents/skills/`.

## ciembor/agent-rules-books

Repositorio: `ciembor/agent-rules-books`

Arquivos usados como base:

- `implementing-domain-driven-design/implementing-domain-driven-design.mini.md`
- `domain-driven-design-distilled/domain-driven-design-distilled.mini.md`

Licenca declarada no projeto de origem: MIT License.

Copyright declarado no projeto de origem:

```text
MIT License

Copyright (c) 2026 Maciej Ciemborowicz
```

Uso neste repositorio:

- Adaptacao em portugues para orientar o Codex em tarefas de implementacao, revisao e refatoracao DDD.
- O conteudo foi ajustado ao contexto deste repositorio: .NET, Clean Architecture, Domain/Application/Infrastructure, EF Core, PostgreSQL, Outbox, Pub/Sub e testes automatizados.
- As skills nao substituem os livros originais nem representam material oficial de Vaughn Vernon.

## tango238/distill-ddd

Repositorio: `tango238/distill-ddd`

Arquivos usados como base:

- `SKILL.md`
- `README.md`

Licenca declarada no projeto de origem: MIT License.

Copyright declarado no projeto de origem:

```text
MIT License

Copyright (c) 2026 Go Tanaka
```

Uso neste repositorio:

- Adaptacao em portugues do fluxo de modelagem para discovery, Event Storming, bounded contexts, context map, aggregates, domain events, glossary, workflows, tipos e simulacao.
- O conteudo foi ajustado para produzir artefatos em `docs/domain/` e apoiar decisoes de implementacao neste repositorio.
- A skill nao substitui os livros originais nem representa material oficial de Vaughn Vernon ou Scott Wlaschin.

## Aviso sobre os livros citados

As referencias conceituais citadas pelos projetos de origem incluem:

- `Implementing Domain-Driven Design`, Vaughn Vernon.
- `Domain-Driven Design Distilled`, Vaughn Vernon.
- `Domain Modeling Made Functional`, Scott Wlaschin.

Nenhum trecho dos livros foi copiado para este repositorio. As skills adicionadas sao adaptacoes operacionais em portugues, derivadas de materiais open source MIT e ajustadas para orientar agentes de codigo neste projeto.

## Texto da licenca MIT

```text
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```