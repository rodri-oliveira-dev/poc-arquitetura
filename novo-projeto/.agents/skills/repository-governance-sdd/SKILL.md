---
name: repository-governance-sdd
description: Use esta skill para governança do repositório, criação ou revisão de AGENTS.md, skills, ADRs, specs SDD, prompts e documentação de processo. Não use para implementar código de produção ou testes de aplicação.
---

# Objetivo

Conduzir tarefas de governança usando um fluxo simples de especificação, descoberta, decisão, entrega, validação e commit semântico.

## Quando usar

- Criar, revisar ou reorganizar skills em `.agents/skills/`.
- Ajustar regras globais em `AGENTS.md`.
- Criar ou revisar ADRs, specs, padrões de documentação e onboarding.
- Avaliar se uma decisão deve virar ADR, documentação atual ou apenas registro de tarefa.
- Revisar automações de agentes e limites de contexto.

## Quando não usar

- Implementar comportamento no backend.
- Corrigir testes de aplicação.
- Refatorar código de produção.
- Criar scripts sem comportamento determinístico e seguro.

## Processo

1. Especifique a intenção da mudança em uma frase.
2. Leia `AGENTS.md` e somente os documentos relevantes.
3. Identifique regras globais, fluxos especializados e referências auxiliares.
4. Decida o local correto de cada orientação:
   - `AGENTS.md` para regras globais;
   - `SKILL.md` para procedimento especializado;
   - ADR para decisão arquitetural relevante;
   - documentação para estado atual e operação;
   - spec para requisitos, design, tarefas e resultado de uma mudança.
5. Evite duplicação entre arquivos.
6. Crie poucas skills, cada uma com propósito claro.
7. Use `references/` somente quando o material auxiliar for realmente necessário.
8. Use `scripts/` somente para automação determinística, repetível e segura.
9. Valide nomes, frontmatter, links e referências.
10. Revise o diff e use commit semântico.

## Validação

- Skills devem existir em `.agents/skills/<nome>/SKILL.md`.
- `name` deve estar em kebab-case.
- `description` deve dizer quando usar e quando não usar.
- Não deve haver referências a arquivos inexistentes.
- Não deve haver segredos, URLs inventadas ou regras específicas de um projeto antigo.

## Restrições

- Não criar skills genéricas demais.
- Não criar uma skill para uma tarefa rara e pontual.
- Não duplicar fluxos longos no `AGENTS.md`.
- Não alterar produção, testes ou pipeline fora do escopo explícito.
