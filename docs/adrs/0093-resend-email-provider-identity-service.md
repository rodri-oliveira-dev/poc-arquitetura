# ADR-0093: Resend como provider de e-mail do IdentityService

## Status
Aceito

## Data
2026-06-26

## Contexto
O `IdentityService` precisa de uma implementacao real de envio de e-mail para
ambientes fora do desenvolvimento local. A escolha deve ser simples para uma POC,
nao deve espalhar detalhes do SDK pela Application e deve preservar a porta
`IEmailSender`.

Mailpit cobre captura local, mas nao representa envio real para usuarios.

## Decisao
Usar Resend como provider real de e-mail do `IdentityService`.

Os motivos da escolha sao:

- API simples para envio transacional de e-mails;
- configuracao pequena para a POC;
- boa aderencia ao caso atual de e-mail de boas-vindas;
- facilidade de encapsular o SDK em um adapter de Infrastructure;
- alternativa clara ao Mailpit local sem mudar Application ou Domain.

O Resend fica encapsulado em `IdentityService.Infrastructure` por
`ResendEmailSender`, `ResendClientFactory` e `ResendOptions`. O restante da
aplicacao usa apenas `IEmailSender`.

O provider concreto e selecionado por `Email:Provider=Resend`. A chave
`Resend:ApiKey` e segredo operacional e nao deve ser versionada.

## Consequencias

### Beneficios
- Mantem a dependencia externa restrita a Infrastructure.
- Preserva testabilidade da Application por meio de `IEmailSender`.
- Permite alternar entre Resend e Mailpit sem alterar o caso de uso.
- Evita expor API key ou modelos do SDK para Domain/Application.

### Custos e limitacoes
- O envio real depende da disponibilidade do servico externo.
- A POC ainda nao implementa retry duravel, Outbox ou DLQ para falhas de envio.
- Dominios, remetentes e limites do Resend precisam ser tratados na configuracao
  do ambiente real.
- Testes automatizados nao devem depender da rede nem do Resend real.

### Impactos operacionais
- Configure `Resend:ApiKey`, `Resend:From`, `Resend:FromName` e opcionalmente
  `Resend:ReplyTo` fora do repositorio.
- Use `Email:Provider=Resend` apenas em ambiente em que o envio real seja
  desejado.
- Falhas retornadas pelo Resend sao registradas em log pelo adapter.

## Fora do escopo
- Comparar provedores de e-mail de forma exaustiva.
- Configurar DNS, SPF, DKIM ou DMARC neste repositorio.
- Implementar fallback automatico para outro provider.
- Usar Resend em testes automatizados.
