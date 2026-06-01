---
name: gcp-cloud-sql-postgres
description: Use esta skill para desenhar, revisar ou documentar Cloud SQL for PostgreSQL para esta POC .NET. Cobre instancia, database, conectividade segura, migrations EF Core, backups, HA, IAM, secrets e custos.
---

# Objetivo

Orientar o uso de Cloud SQL for PostgreSQL como alternativa gerenciada aos bancos PostgreSQL locais da POC, preservando seguranca, migrations EF Core e operacao previsivel.

# Quando usar

- Planejar ou revisar Cloud SQL para `LedgerService` ou `BalanceService`.
- Revisar Terraform relacionado a instancia, databases, flags, backups, HA ou conectividade.
- Ajustar documentacao de connection strings, migrations, readiness, secrets ou operacao de banco no GCP.
- Diagnosticar conectividade, permissao, timeout, pool de conexoes ou Auth Proxy.

# Quando nao usar

- Alterar modelos EF Core, mappings ou migrations por regra de negocio. Use skills .NET apropriadas.
- Otimizar queries EF Core sem relacao com Cloud SQL. Use `optimizing-ef-core-queries`.
- Executar criacao real de banco ou mudanca remota sem pedido explicito.

# Passos

1. Leia `README.md`, `docs/architecture/`, docs de desenvolvimento, ADRs de persistencia e connection strings atuais.
2. Identifique os bancos necessarios, donos, permissoes e migrations correspondentes.
3. Separe configuracao por ambiente e evite compartilhar credenciais privilegiadas entre workloads.
4. Prefira secrets externos ao repositorio para senhas e connection strings.
5. Avalie conectividade a partir de Cloud Run, GKE, CI e ambiente local.
6. Defina politica de backup, retencao, HA, janela de manutencao, flags e tamanho inicial conforme criticidade.
7. Preserve readiness e health checks sem transformar readiness em carga excessiva no banco.
8. Revise pool de conexoes, timeouts e limites para evitar exaustao de conexoes.
9. Documente como migrations EF Core serao executadas por ambiente.
10. Use Terraform para mudancas persistentes de infraestrutura.
11. Atualize ADR quando a decisao afetar estrategia de persistencia, operacao, conectividade ou seguranca.

# Validacao

- Revisar Terraform ou documentacao sem segredos.
- Validar build/teste apenas se a mudanca afetar codigo ou migrations.
- Validar que nomes de banco, usuarios e variaveis nao acoplam ambiente real ao repositorio.
- Registrar riscos de custo, disponibilidade, permissao e conectividade.

# Restricoes

- Nao alterar configuracao remota sem aprovacao explicita.
- Nao versionar senha, connection string real, certificado privado, chave ou arquivo de credencial.
- Nao usar credencial administrativa como identidade padrao da aplicacao.
- Nao assumir HA, IP publico, rede privada ou Auth Proxy sem decisao explicita.
