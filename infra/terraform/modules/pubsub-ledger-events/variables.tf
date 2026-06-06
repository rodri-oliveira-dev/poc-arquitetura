variable "project_id" {
  description = "ID do projeto Google Cloud onde recursos Pub/Sub e service accounts sao criados."
  type        = string

  validation {
    condition     = length(trimspace(var.project_id)) > 0
    error_message = "project_id nao deve ficar vazio."
  }
}

variable "pubsub_service_agent_member" {
  description = "IAM member retornado por google_project_service_identity para pubsub.googleapis.com."
  type        = string

  validation {
    condition     = startswith(var.pubsub_service_agent_member, "serviceAccount:")
    error_message = "pubsub_service_agent_member deve usar o formato IAM member serviceAccount:<email>."
  }
}

variable "service_account_token_creator_members" {
  type        = list(string)
  description = "Membros autorizados a impersonar as service accounts dos workers para smoke tests locais. Use apenas em ambientes dev/controlados."
  default     = []
}

variable "region" {
  description = "Regiao de deployment usada como metadado de recurso. Topics e subscriptions Pub/Sub sao recursos globais."
  type        = string

  validation {
    condition     = can(regex("^[a-z]+-[a-z0-9]+[0-9]$", var.region))
    error_message = "region deve ser um nome valido de regiao Google Cloud, como us-central1."
  }
}

variable "allowed_persistence_regions" {
  type        = list(string)
  description = "Regioes em que o conteudo das mensagens Pub/Sub pode ser armazenado/processado. Lista vazia nao configura message_storage_policy explicitamente."
  default     = []
}

variable "enforce_in_transit" {
  type        = bool
  description = "Quando true, rejeita publish/pull/streamingPull recebidos fora das regioes permitidas pela message storage policy."
  default     = false
}

variable "environment" {
  description = "Identificador do ambiente usado em labels e IDs das service accounts dedicadas."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]*$", var.environment)) && length(var.environment) <= 63
    error_message = "environment deve iniciar com letra minuscula, conter apenas letras minusculas, digitos ou hifens e ter no maximo 63 caracteres."
  }
}

variable "app_name" {
  description = "Identificador da aplicacao usado em labels e IDs das service accounts dedicadas."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]*$", var.app_name)) && length(var.app_name) <= 63
    error_message = "app_name deve iniciar com letra minuscula, conter apenas letras minusculas, digitos ou hifens e ter no maximo 63 caracteres."
  }
}

variable "ledger_events_topic_name" {
  description = "Nome do topic principal de eventos Ledger."
  type        = string
}

variable "ledger_events_subscription_name" {
  description = "Nome da pull subscription consumida pelo Balance Worker."
  type        = string
}

variable "application_dlq_topic_name" {
  description = "Nome do topic usado pelo Balance Worker para mensagens classificadas como DLQ de aplicacao."
  type        = string
}

variable "technical_dlq_topic_name" {
  description = "Nome do topic usado pela dead-letter policy nativa para falhas tecnicas de entrega."
  type        = string
}

variable "application_dlq_subscription_name" {
  description = "Nome da pull subscription que retem mensagens da DLQ de aplicacao para inspecao operacional."
  type        = string
}

variable "technical_dlq_subscription_name" {
  description = "Nome da pull subscription que retem mensagens da DLQ tecnica para inspecao operacional."
  type        = string
}

variable "ack_deadline_seconds" {
  description = "Acknowledgement deadline em segundos da pull subscription do Balance Worker."
  type        = number
  default     = 30

  validation {
    condition     = floor(var.ack_deadline_seconds) == var.ack_deadline_seconds && var.ack_deadline_seconds >= 10 && var.ack_deadline_seconds <= 600
    error_message = "ack_deadline_seconds deve ser um inteiro entre 10 e 600."
  }
}

variable "message_retention_duration" {
  description = "Duracao de retencao de mensagens da subscription usando o formato de duracao Google, como 604800s."
  type        = string
  default     = "604800s"

  validation {
    condition = can(regex("^[0-9]+(\\.[0-9]{1,9})?s$", var.message_retention_duration)) && try(
      tonumber(trimsuffix(var.message_retention_duration, "s")) >= 600 &&
      tonumber(trimsuffix(var.message_retention_duration, "s")) <= 2678400,
      false
    )
    error_message = "message_retention_duration deve ficar entre 600s e 2678400s usando o formato de duracao Google."
  }
}

variable "retain_acked_messages" {
  description = "Define se as subscriptions retem mensagens confirmadas durante a janela de retencao."
  type        = bool
  default     = false

  validation {
    condition     = !var.retain_acked_messages
    error_message = "retain_acked_messages deve permanecer false salvo requisito operacional documentado que justifique reter mensagens confirmadas."
  }
}

variable "main_subscription_expiration_ttl" {
  description = "TTL de inatividade da subscription do Balance Worker usando o formato de duracao Google. Use string vazia para nunca expirar."
  type        = string
  default     = ""

  validation {
    condition = var.main_subscription_expiration_ttl == "" || (
      can(regex("^[0-9]+(\\.[0-9]{1,9})?s$", var.main_subscription_expiration_ttl)) &&
      try(tonumber(trimsuffix(var.main_subscription_expiration_ttl, "s")) >= 86400, false)
    )
    error_message = "main_subscription_expiration_ttl deve ser vazio para nunca expirar ou ter pelo menos 86400s usando o formato de duracao Google."
  }
}

variable "application_dlq_subscription_expiration_ttl" {
  description = "TTL de inatividade da subscription de inspecao da DLQ de aplicacao usando o formato de duracao Google. Use string vazia para nunca expirar."
  type        = string
  default     = "2592000s"

  validation {
    condition = var.application_dlq_subscription_expiration_ttl == "" || (
      can(regex("^[0-9]+(\\.[0-9]{1,9})?s$", var.application_dlq_subscription_expiration_ttl)) &&
      try(tonumber(trimsuffix(var.application_dlq_subscription_expiration_ttl, "s")) >= 86400, false)
    )
    error_message = "application_dlq_subscription_expiration_ttl deve ser vazio para nunca expirar ou ter pelo menos 86400s usando o formato de duracao Google."
  }
}

variable "technical_dlq_subscription_expiration_ttl" {
  description = "TTL de inatividade da subscription de inspecao da DLQ tecnica usando o formato de duracao Google. Use string vazia para nunca expirar."
  type        = string
  default     = "2592000s"

  validation {
    condition = var.technical_dlq_subscription_expiration_ttl == "" || (
      can(regex("^[0-9]+(\\.[0-9]{1,9})?s$", var.technical_dlq_subscription_expiration_ttl)) &&
      try(tonumber(trimsuffix(var.technical_dlq_subscription_expiration_ttl, "s")) >= 86400, false)
    )
    error_message = "technical_dlq_subscription_expiration_ttl deve ser vazio para nunca expirar ou ter pelo menos 86400s usando o formato de duracao Google."
  }
}

variable "enable_message_ordering" {
  description = "Define se a subscription do Balance Worker habilita entrega ordenada para mensagens com a mesma ordering key."
  type        = bool
  default     = false
}

variable "enable_exactly_once_delivery" {
  description = "Define se a subscription do Balance Worker habilita Pub/Sub exactly-once delivery."
  type        = bool
  default     = false
}

variable "min_retry_backoff" {
  description = "Retry backoff minimo usando o formato de duracao Google, como 10s."
  type        = string
  default     = "10s"
}

variable "max_retry_backoff" {
  description = "Retry backoff maximo usando o formato de duracao Google, como 600s."
  type        = string
  default     = "600s"
}

variable "enable_technical_dead_letter" {
  type        = bool
  description = "Habilita dead-letter policy tecnica do Pub/Sub na subscription principal."
  default     = true
}

variable "max_delivery_attempts" {
  description = "Numero aproximado de tentativas de entrega antes de o Pub/Sub encaminhar uma mensagem para a DLQ tecnica."
  type        = number
  default     = 5

  validation {
    condition     = floor(var.max_delivery_attempts) == var.max_delivery_attempts && var.max_delivery_attempts >= 5 && var.max_delivery_attempts <= 100
    error_message = "max_delivery_attempts deve ser um inteiro entre 5 e 100."
  }
}

variable "labels" {
  description = "Labels adicionais combinadas com as labels app, environment e region."
  type        = map(string)
  default     = {}
}
