# ADR-0085: Separacao de configuracoes locais sensiveis dos arquivos versionados

## Status
Proposta

## Contexto
Os arquivos `appsettings` versionados dos workers chegaram a conter connection strings locais com `Password=local_dev_password` para facilitar a execucao em ambiente de desenvolvimento. Embora esse valor nao represente uma credencial produtiva nem permita acesso a recurso externo real, ele tem formato de segredo e aparece em um arquivo rastreado pelo Git.

O SonarQube apontou esse padrao como hard-coded credential nos arquivos `appsettings.PubSub.json`. O alerta e tecnicamente esperado: ferramentas de analise estatica nao conseguem diferenciar com seguranca uma senha local descartavel de um segredo real, especialmente quando o valor esta em uma connection string versionada.

Mesmo quando a senha e apenas local, manter valores semelhantes a secrets no repositorio enfraquece a politica de nao versionar segredos. Isso cria ruido recorrente em SonarQube, Trivy e revisoes manuais, aumenta o risco de excecoes indevidas e torna menos clara a fronteira entre exemplos seguros e configuracoes privadas de cada maquina.

A separacao tambem precisa preservar a developer experience. O fluxo local deve continuar simples para quem sobe PostgreSQL, Pub/Sub Emulator e workers em debug, mas com um contrato explicito: arquivos versionados documentam as chaves esperadas com placeholders; arquivos locais nao versionados carregam valores sensiveis ou especificos da maquina.

## Decisao
- Manter arquivos `appsettings` versionados sem passwords, tokens, chaves privadas ou outros valores sensiveis reais.
- Usar `DOTNET_ENVIRONMENT=Local` para execucao local quando forem necessarias configuracoes especificas do ambiente de desenvolvimento.
- Usar `appsettings.Local.json` para configuracoes sensiveis locais e especificas de maquina, sem versionar esse arquivo.
- Versionar apenas `appsettings.Local.example.json` com placeholders e estrutura suficiente para orientar o setup local.
- Usar `.env.local` nao versionado para variaveis de Docker Compose local/debug, incluindo valores sensiveis ou especificos da maquina.
- Versionar `.env.local.example` com placeholders seguros e nomes de variaveis esperados.
- Criar ou manter `compose.debug.yml` para subir dependencias locais de desenvolvimento, como PostgreSQL e Pub/Sub Emulator, sem exigir secrets versionados.
- Manter CI, SonarQube e Trivy validando a presenca de secrets no repositorio, tratando achados reais como falhas ou itens de correcao.

## Consequencias positivas
- Reduz ruido do SonarQube causado por credenciais locais com formato de segredo em arquivos versionados.
- Torna mais clara a politica de secrets: arquivos versionados podem conter placeholders, mas nao valores sensiveis reais.
- Padroniza o onboarding local por meio de arquivos `.example` e ambiente `Local`.
- Diminui o risco de vazamento acidental de credenciais reais por copia de padroes locais.
- Mantem CI, SonarQube e Trivy coerentes com a postura de seguranca do repositorio.
- Facilita revisoes futuras, pois alerts de secret passam a ter maior sinal e menor quantidade de falsos positivos conhecidos.

## Consequencias negativas
- Adiciona um passo de setup local para copiar ou criar arquivos nao versionados a partir dos exemplos.
- Exige documentacao clara para evitar duvidas entre `Development`, `Local`, `.env.local` e `appsettings.Local.json`.
- Pode haver divergencia entre arquivos locais de desenvolvedores diferentes se os exemplos ficarem desatualizados.
- Requer manutencao continua dos arquivos `*.example` quando novas configuracoes forem introduzidas.
- Pode causar falhas locais iniciais se `DOTNET_ENVIRONMENT=Local` ou os arquivos locais esperados nao forem configurados corretamente.

## Alternativas consideradas
1. Manter senha local versionada.

   Simples para onboarding, mas mantem alertas de hard-coded credential, enfraquece a politica de secrets e normaliza a presenca de valores com formato sensivel no Git.

2. Usar apenas variaveis de ambiente sem `appsettings.Local`.

   Evita arquivos locais sensiveis, mas piora a ergonomia para debug de aplicacoes .NET no host e espalha configuracao extensa por shell, IDE ou scripts locais.

3. Usar `user-secrets` do .NET.

   E adequado para desenvolvimento local no host, mas nao cobre bem Docker Compose, workers em containers e fluxo compartilhado de dependencias locais. Pode ser usado pontualmente, mas nao como mecanismo unico da POC.

4. Usar somente Docker Compose com `.env`.

   Funciona bem para containers, mas nao atende com a mesma clareza execucoes em debug no host e configuracoes carregadas naturalmente pelo pipeline de configuracao do .NET.

5. Suprimir o alerta no Sonar.

   Reduziria ruido no curto prazo, mas preservaria o padrao que gerou o achado e criaria uma excecao de seguranca dificil de justificar quando ha uma alternativa simples baseada em exemplos e arquivos locais nao versionados.

## Criterios de aceite
- Nenhum `appsettings` versionado contem password real, token, chave privada ou connection string com segredo real.
- `appsettings.Local.json` esta no `.gitignore`.
- `.env.local` esta no `.gitignore`.
- `appsettings.Local.example.json` e versionado com placeholders seguros.
- `.env.local.example` e versionado com placeholders seguros.
- O compose local/debug continua funcionando para subir dependencias como PostgreSQL e Pub/Sub Emulator.
- SonarQube e Trivy continuam ativos e nao reportam segredo real versionado relacionado a essa configuracao.
- A documentacao de desenvolvimento local e atualizada com o fluxo `DOTNET_ENVIRONMENT=Local`, arquivos `.example` e arquivos locais nao versionados.
