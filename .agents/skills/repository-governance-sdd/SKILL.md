---
name: repository-governance-sdd
description: Use esta skill para tarefas SDD de governanca do repositorio, criacao ou revisao de skills Codex, AGENTS.md, ADRs, prompts e documentacao de processo. Nao use para implementar codigo de producao ou testes de aplicacao.
---

# Objetivo

Conduzir tarefas de governanca do repositorio usando SDD: Specification, Discovery, Decision, Delivery, Validation e Semantic Commit.
Esta skill ajuda o Codex a separar regras globais, fluxos especificos e documentacao auxiliar sem aumentar desnecessariamente o contexto.
Ela deve ser usada para evoluir orientacoes de agentes, skills, ADRs e convencoes operacionais do projeto.

# Quando usar

- Criar, revisar ou reorganizar skills em `.agents/skills/`.
- Ajustar `AGENTS.md` para orientacoes globais, roteamento e convencoes compartilhadas.
- Escrever ou revisar prompts SDD, politicas de uso do Codex ou governanca de automacao assistida.
- Avaliar se uma decisao deve virar ADR ou ficar apenas em documentacao operacional.
- Revisar documentacao de processo, standards, onboarding ou convencoes do repositorio.

# Quando nao usar

- Implementar comportamento nos servicos .NET.
- Alterar testes de aplicacao.
- Corrigir pipelines, releases ou GitVersion sem trabalho de governanca/documentacao.
- Criar scripts de automacao sem criterio deterministico, seguro e repetivel.
- Fazer refactors de codigo de producao.

# Entradas esperadas

- Intencao da mudanca de governanca ou documentacao.
- Arquivos de orientacao permitidos ou escopo documental informado pelo usuario.
- Criterios de decisao, restricoes e formato esperado.
- Necessidade ou nao de commit semantico.

# Saidas esperadas

- Decisao explicita sobre o que criar, alterar, fundir ou nao criar.
- Skills focadas, com `SKILL.md` e frontmatter `name` e `description` quando aplicavel.
- `AGENTS.md` ajustado apenas para regras globais, sem duplicar fluxos longos.
- Relatorio em portugues com diagnostico, decisoes, validacoes, riscos e commit.

# Passos

1. Especifique a intencao da mudanca em uma frase objetiva.
2. Descubra o estado atual lendo primeiro `AGENTS.md` e `.agents/`; depois consulte arquivos do repo necessarios ao diagnostico.
3. Identifique tarefas recorrentes, riscos de contexto, pontos de sobreposicao e regras que pertencem ao nivel global.
4. Classifique candidatas como criar agora, nao criar agora, fundir, colocar no `AGENTS.md`, colocar em skill ou manter apenas como referencia.
5. Decida antes de editar e comunique a decisao.
6. Crie poucas skills, cada uma com um unico proposito claro.
7. Use `references/` somente para material auxiliar realmente util e `scripts/` somente para automacao deterministica e segura.
8. Ajuste `AGENTS.md` apenas quando houver lacuna global ou conflito com skills.
9. Evite duplicar no `AGENTS.md` fluxos longos que pertencem ao `description` ou ao corpo de uma skill.
10. Valide estrutura, frontmatter, nomes, descricoes, escopo, idioma e ausencia de referencias proibidas.
11. Revise diff, execute validacoes proporcionais e faca commit semantico quando solicitado.
12. Para comandos Git que alteram indice ou historico (`git add`, `git commit`, `git restore --staged` etc.), use execucao fora do sandbox quando o sandbox nao conseguir criar `.git/index.lock`.

# Validacao

- Verifique existencia de `.agents/skills/<nome>/SKILL.md`.
- Confirme frontmatter com `name` e `description`.
- Confirme nomes em kebab-case e descricoes curtas, especificas e com limites.
- Procure sobreposicao excessiva entre skills.
- Confirme que o conteudo esta em portugues e sem segredos.
- Confirme ausencia de referencias a skills inexistentes ou arquivos de orientacao externos ao Codex.
- Execute `git status` e revise o diff antes de commitar.

# Restricoes

- Nao criar skills genericas demais.
- Nao criar muitas skills para cobrir possibilidades raras.
- Nao duplicar em `AGENTS.md` conteudo detalhado que pertence a skills.
- Nao alterar codigo de producao nem testes de aplicacao.
- Nao alterar pipeline salvo necessidade explicita e justificada.
- Nao fazer push nem criar branch sem necessidade do fluxo em execucao.
