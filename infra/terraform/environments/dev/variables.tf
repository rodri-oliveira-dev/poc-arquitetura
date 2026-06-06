variable "project_id" {
  description = "ID do projeto Google Cloud usado pelo ambiente dev."
  type        = string

  validation {
    condition     = length(trimspace(var.project_id)) > 0
    error_message = "project_id nao deve ficar vazio."
  }
}

variable "region" {
  description = "Regiao Google Cloud usada pelo ambiente dev e como metadado dos recursos Pub/Sub."
  type        = string

  validation {
    condition     = can(regex("^[a-z]+-[a-z0-9]+[0-9]$", var.region))
    error_message = "region deve ser um nome valido de regiao Google Cloud, como us-central1."
  }
}

variable "allowed_persistence_regions" {
  type        = list(string)
  description = "Regioes em que o conteudo das mensagens Pub/Sub pode ser armazenado e processado. Lista vazia omite message_storage_policy."
  default     = []
}

variable "enforce_in_transit" {
  type        = bool
  description = "Define se o Pub/Sub rejeita requests publish, pull e streamingPull recebidos fora das regioes de persistencia permitidas."
  default     = false
}

variable "service_account_token_creator_members" {
  type        = list(string)
  description = "Membros autorizados a impersonar as service accounts dos workers para smoke tests locais em dev."
  default     = []
}

variable "enable_technical_dead_letter" {
  description = "Define se a dead-letter policy nativa do Pub/Sub fica habilitada na subscription principal."
  type        = bool
  default     = true
}

variable "main_subscription_expiration_ttl" {
  description = "TTL de inatividade da subscription do Balance Worker. Use string vazia para nunca expirar."
  type        = string
  default     = ""
}

variable "application_dlq_subscription_expiration_ttl" {
  description = "TTL de inatividade da subscription de inspecao da DLQ de aplicacao. Use string vazia para nunca expirar."
  type        = string
  default     = "2592000s"
}

variable "technical_dlq_subscription_expiration_ttl" {
  description = "TTL de inatividade da subscription de inspecao da DLQ tecnica. Use string vazia para nunca expirar."
  type        = string
  default     = "2592000s"
}

variable "database_instance_name" {
  description = "Nome da instancia Cloud SQL PostgreSQL para dev."
  type        = string
  default     = "poc-ledger-dev-postgres"
}

variable "database_name" {
  description = "Nome do database da aplicacao para dev."
  type        = string
  default     = "ledger_dev"
}

variable "database_user" {
  description = "Usuario do database da aplicacao para dev."
  type        = string
  default     = "ledger_app"
}

variable "database_password" {
  description = "Senha do database da aplicacao para dev. Forneca somente por terraform.tfvars ignorado ou TF_VAR_database_password."
  type        = string
  sensitive   = true
}

variable "database_version" {
  description = "Versao do database Cloud SQL PostgreSQL para dev."
  type        = string
  default     = "POSTGRES_16"
}

variable "database_tier" {
  description = "Tier da maquina Cloud SQL para dev."
  type        = string
  default     = "db-f1-micro"

  validation {
    condition     = contains(["db-f1-micro", "db-g1-small"], var.database_tier)
    error_message = "database_tier deve permanecer um tier shared-core de baixo custo para a POC dev descartavel."
  }
}

variable "database_availability_type" {
  description = "Tipo de disponibilidade do Cloud SQL para dev."
  type        = string
  default     = "ZONAL"

  validation {
    condition     = var.database_availability_type == "ZONAL"
    error_message = "database_availability_type deve permanecer ZONAL para a POC dev descartavel."
  }
}

variable "database_deletion_protection" {
  description = "Define se a instancia Cloud SQL dev fica protegida contra exclusao."
  type        = bool
  default     = false
}

variable "database_backup_enabled" {
  description = "Define se backups automaticos do Cloud SQL ficam habilitados para dev."
  type        = bool
  default     = false
}

variable "database_backup_start_time" {
  description = "Horario UTC de inicio do backup no formato HH:MM."
  type        = string
  default     = "03:00"
}

variable "database_point_in_time_recovery_enabled" {
  description = "Define se point-in-time recovery do Cloud SQL fica habilitado para dev."
  type        = bool
  default     = false
}

variable "database_transaction_log_retention_days" {
  description = "Numero de dias para reter logs de transacao para point-in-time recovery."
  type        = number
  default     = 7
}

variable "database_backup_location" {
  description = "Local opcional do backup. Use null para deixar o Cloud SQL escolher o default."
  type        = string
  default     = null
}

variable "database_disk_size" {
  description = "Tamanho inicial do disco Cloud SQL em GB para dev."
  type        = number
  default     = 10

  validation {
    condition     = var.database_disk_size == 10
    error_message = "database_disk_size deve permanecer 10 GB para a POC dev descartavel."
  }
}

variable "database_disk_autoresize" {
  description = "Define se o Cloud SQL pode aumentar automaticamente o tamanho do disco em dev."
  type        = bool
  default     = false

  validation {
    condition     = !var.database_disk_autoresize
    error_message = "database_disk_autoresize deve permanecer false para a POC dev descartavel."
  }
}
