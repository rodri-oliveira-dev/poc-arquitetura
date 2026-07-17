# ADR-0090: Cadastro de usuarios no IdentityService

## Status
Aceito

## Data
2026-06-26

## Contexto
O `IdentityService` precisa cadastrar usuarios no provider de identidade
principal e manter um vinculo local com dados necessarios para a POC, em
especial o `MerchantId`.

O Keycloak e o provider principal de identidade do projeto. Ele deve armazenar
credenciais e emitir tokens OIDC/JWT. O banco local do `IdentityService` deve
guardar apenas os dados de dominio necessarios ao contexto e a referencia ao
usuario criado no Keycloak.

A senha informada no cadastro e sensivel e nao deve ser persistida no banco do
`IdentityService`.

## Decisao
O cadastro de usuarios do `IdentityService` integra com Keycloak antes de
persistir o vinculo local.

O fluxo decidido e:

- `IdentityService.Api` recebe `username`, `name`, `email`, `password` e
  `document` em `POST /api/v1/users`;
- `Application` orquestra o caso de uso por `CreateUserCommandHandler`;
- a porta `IIdentityProviderUserService` cria o usuario no Keycloak;
- a porta `IMerchantIdGenerator` gera automaticamente o `MerchantId`;
- o aggregate `User` e criado no dominio com `UserId`, `Email`, `Username`,
  `MerchantId` e `KeycloakUserId`;
- `IUserRepository` persiste o usuario local no schema `identity`;
- a senha e enviada somente ao Keycloak e nao e salva localmente;
- se qualquer operacao apos a criacao confirmada no Keycloak e antes da
  confirmacao local falhar, o fluxo tenta remover o usuario recem-criado do
  provider para evitar vinculo orfao.

O banco local do `IdentityService` persiste a identidade de dominio e o
`KeycloakUserId`, mas nao persiste senha, hash de senha ou segredo equivalente.

## Consequencias

### Beneficios
- Mantem Keycloak como dono das credenciais e autenticacao.
- Mantem `MerchantId` sob controle do bounded context de identidade.
- Evita armazenamento local de senha.
- Permite rastrear o vinculo entre usuario local e usuario do Keycloak.
- Reduz risco de usuario criado no provider sem registro local por meio de
  compensacao imediata quando a modelagem ou persistencia local falha.

### Custos e limitacoes
- O cadastro depende da disponibilidade do Keycloak Admin API.
- A criacao no Keycloak ocorre antes do commit local, portanto a compensacao de
  falha ainda e uma chamada externa best effort.
- O `MerchantId` gerado automaticamente precisa continuar unico e validado pela
  persistencia local.
- O campo `document` faz parte do contrato atual, mas nao deve forcar
  persistencia se nao houver regra de dominio implementada para ele.

### Impactos operacionais
- O client `identity-service-admin` deve possuir permissoes `manage-users` e
  `view-users` no realm `poc`.
- A configuracao local usa `IdentityProvider:Keycloak:*` e o segredo do client
  deve vir de variavel de ambiente, user secrets ou secret store.
- O endpoint exige token com scope `identity.write`.

## Fora do escopo
- Autenticacao de usuario final.
- Reset de senha, troca de senha, recuperacao de conta ou MFA.
- Sincronizacao retroativa de usuarios existentes no Keycloak.
- Cadastro de merchants como aggregate separado.
