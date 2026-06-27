# ADR-0092: Envio de e-mail no IdentityService

## Status
Aceito

## Data
2026-06-26

## Contexto
O cadastro de usuario deve disparar e-mail de boas-vindas sem acoplar o caso de
uso de cadastro a um provedor concreto de e-mail. O conteudo do e-mail tambem
precisa ser mantido fora do codigo do handler para permitir evolucao do template.

O envio e uma reacao ao cadastro, nao uma regra que define se o usuario pode ou
nao existir. Por isso, ele deve ser tratado como side effect posterior ao commit
local.

## Decisao
Enviar e-mail de boas-vindas por handler de `UserRegisteredDomainEvent`.

O fluxo decidido e:

- `User.Register` emite `UserRegisteredDomainEvent`;
- o dispatcher executa
  `SendWelcomeEmailOnUserRegisteredDomainEventHandler` depois do commit local;
- o handler renderiza o template HTML configurado em `Email:TemplatePath`;
- o template versionado fica em
  `src/identity/IdentityService.Infrastructure/Email/Templates/WelcomeEmail.html`;
- o handler monta um `EmailMessage` com destinatario, assunto e corpo HTML;
- o envio ocorre pela porta `IEmailSender`;
- implementacoes concretas de envio ficam desacopladas na Infrastructure.

`Application` conhece a abstracao `IEmailSender` e o modelo `EmailMessage`, mas
nao conhece Resend, SMTP, Mailpit, templates fisicos ou detalhes de transporte.

## Consequencias

### Beneficios
- O caso de uso de cadastro nao depende de provedor de e-mail.
- O template HTML pode evoluir sem alterar o contrato do caso de uso.
- Resend, Mailpit ou outro provider podem ser trocados por configuracao e DI.
- O envio fica testavel por fake de `IEmailSender`.
- Falhas de e-mail nao desfazem o cadastro ja persistido.

### Custos e limitacoes
- Sem Outbox, o envio de e-mail nao possui retry duravel.
- Falha de renderizacao ou envio aparece em log e precisa de acao operacional
  manual.
- O template precisa continuar compativel com os placeholders usados pelo
  renderer.
- O link de autenticacao depende de `Email:AuthenticationUrl` corretamente
  configurado.

### Impactos operacionais
- Em ambiente local, `Email:Provider=Mailpit` captura o e-mail sem envio real.
- Em ambientes reais, `Email:Provider=Resend` exige `Resend:ApiKey` por secret
  store, user secrets ou variavel de ambiente.
- Testes automatizados nao devem usar provider externo real.

## Fora do escopo
- Criar fila de e-mails.
- Reenvio administrativo de e-mail.
- Templates multi-idioma.
- Tracking de abertura, clique ou bounce.
