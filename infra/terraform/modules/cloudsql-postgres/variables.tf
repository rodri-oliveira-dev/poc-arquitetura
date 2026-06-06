variable "project_id" {
  description = "ID do projeto Google Cloud onde os recursos Cloud SQL sao criados."
  type        = string

  validation {
    condition     = length(trimspace(var.project_id)) > 0
    error_message = "project_id nao deve ficar vazio."
  }
}

variable "region" {
  description = "Regiao Google Cloud onde a instancia Cloud SQL e criada."
  type        = string

  validation {
    condition     = can(regex("^[a-z]+-[a-z0-9]+[0-9]$", var.region))
    error_message = "region deve ser um nome valido de regiao Google Cloud, como us-central1."
  }
}

variable "environment" {
  description = "Identificador do ambiente usado em labels."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]*$", var.environment)) && length(var.environment) <= 63
    error_message = "environment deve iniciar com letra minuscula, conter apenas letras minusculas, digitos ou hifens e ter no maximo 63 caracteres."
  }
}

variable "app_name" {
  description = "Identificador da aplicacao usado em labels."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]*$", var.app_name)) && length(var.app_name) <= 63
    error_message = "app_name deve iniciar com letra minuscula, conter apenas letras minusculas, digitos ou hifens e ter no maximo 63 caracteres."
  }
}

variable "instance_name" {
  description = "Nome da instancia Cloud SQL."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{0,61}[a-z0-9]$", var.instance_name))
    error_message = "instance_name deve iniciar com letra minuscula, conter apenas letras minusculas, digitos ou hifens, terminar com letra minuscula ou digito e ter no maximo 63 caracteres."
  }
}

variable "postgres_version" {
  description = "Versao do database Cloud SQL PostgreSQL."
  type        = string
  default     = "POSTGRES_16"

  validation {
    condition     = can(regex("^POSTGRES_[0-9]+$", var.postgres_version))
    error_message = "postgres_version deve usar o formato Cloud SQL PostgreSQL, como POSTGRES_16."
  }
}

variable "tier" {
  description = "Tier da maquina Cloud SQL."
  type        = string
  default     = "db-f1-micro"

  validation {
    condition     = length(trimspace(var.tier)) > 0
    error_message = "tier nao deve ficar vazio."
  }
}

variable "edition" {
  description = "Edicao do Cloud SQL. ENTERPRISE e exigida para tiers shared-core de baixo custo, como db-f1-micro."
  type        = string
  default     = "ENTERPRISE"

  validation {
    condition     = contains(["ENTERPRISE", "ENTERPRISE_PLUS"], var.edition)
    error_message = "edition deve ser ENTERPRISE ou ENTERPRISE_PLUS."
  }
}

variable "availability_type" {
  description = "Tipo de disponibilidade do Cloud SQL. Use ZONAL para dev de baixo custo e REGIONAL somente quando HA for explicitamente exigido."
  type        = string
  default     = "ZONAL"

  validation {
    condition     = contains(["ZONAL", "REGIONAL"], var.availability_type)
    error_message = "availability_type deve ser ZONAL ou REGIONAL."
  }
}

variable "disk_size" {
  description = "Tamanho inicial do disco de dados Cloud SQL em GB."
  type        = number
  default     = 10

  validation {
    condition     = floor(var.disk_size) == var.disk_size && var.disk_size >= 10
    error_message = "disk_size deve ser um inteiro maior ou igual ao minimo de 10 GB do Cloud SQL."
  }
}

variable "disk_autoresize" {
  description = "Define se o Cloud SQL pode aumentar automaticamente o tamanho do disco."
  type        = bool
  default     = false
}

variable "database_name" {
  description = "Nome do database da aplicacao criado dentro da instancia Cloud SQL."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9_]{0,62}$", var.database_name))
    error_message = "database_name deve iniciar com letra minuscula, conter apenas letras minusculas, digitos ou underscores e ter no maximo 63 caracteres."
  }
}

variable "database_user" {
  description = "Usuario do database da aplicacao criado dentro da instancia Cloud SQL."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9_]{0,62}$", var.database_user))
    error_message = "database_user deve iniciar com letra minuscula, conter apenas letras minusculas, digitos ou underscores e ter no maximo 63 caracteres."
  }
}

variable "database_password" {
  description = "Senha do usuario do database da aplicacao. Forneca por um arquivo terraform.tfvars ignorado ou por TF_VAR_database_password."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.database_password) >= 16
    error_message = "database_password deve ter pelo menos 16 caracteres."
  }
}

variable "deletion_protection" {
  description = "Define se o Terraform e a Cloud SQL API protegem a instancia contra exclusao."
  type        = bool
  default     = false
}

variable "backup_configuration" {
  description = "Configuracao de backup do Cloud SQL. Os defaults favorecem uma baseline dev descartavel e de baixo custo, com backups e point-in-time recovery desabilitados."
  type = object({
    enabled                        = bool
    start_time                     = string
    point_in_time_recovery_enabled = bool
    transaction_log_retention_days = number
    location                       = optional(string)
  })
  default = {
    enabled                        = false
    start_time                     = "03:00"
    point_in_time_recovery_enabled = false
    transaction_log_retention_days = 7
    location                       = null
  }

  validation {
    condition     = can(regex("^([01][0-9]|2[0-3]):[0-5][0-9]$", var.backup_configuration.start_time))
    error_message = "backup_configuration.start_time deve usar o formato HH:MM."
  }

  validation {
    condition     = var.backup_configuration.transaction_log_retention_days >= 1 && var.backup_configuration.transaction_log_retention_days <= 7
    error_message = "backup_configuration.transaction_log_retention_days deve ficar entre 1 e 7."
  }
}

variable "labels" {
  description = "Labels adicionais combinadas com as labels app, environment e region."
  type        = map(string)
  default     = {}

  validation {
    condition = alltrue([
      for key, value in var.labels :
      can(regex("^[a-z][a-z0-9_-]{0,62}$", key)) &&
      can(regex("^[a-z0-9_-]{0,63}$", value))
    ])
    error_message = "As chaves de labels devem iniciar com letra minuscula, e chaves/valores podem conter apenas letras minusculas, digitos, underscores ou hifens."
  }
}
