variable "project_id" {
  description = "Google Cloud project ID used by the dev environment."
  type        = string

  validation {
    condition     = length(trimspace(var.project_id)) > 0
    error_message = "project_id must not be empty."
  }
}

variable "region" {
  description = "Google Cloud region used by the dev environment and as Pub/Sub resource metadata."
  type        = string

  validation {
    condition     = can(regex("^[a-z]+-[a-z0-9]+[0-9]$", var.region))
    error_message = "region must be a valid Google Cloud region name, such as us-central1."
  }
}

variable "allowed_persistence_regions" {
  type        = list(string)
  description = "Regions where Pub/Sub message content may be stored and processed. An empty list omits message_storage_policy."
  default     = []
}

variable "enforce_in_transit" {
  type        = bool
  description = "Whether Pub/Sub rejects publish, pull, and streamingPull requests received outside the allowed persistence regions."
  default     = false
}

variable "service_account_token_creator_members" {
  type        = list(string)
  description = "Membros autorizados a impersonar as service accounts dos workers para smoke tests locais em dev."
  default     = []
}

variable "enable_technical_dead_letter" {
  description = "Whether the native Pub/Sub dead-letter policy is enabled on the main subscription."
  type        = bool
  default     = true
}

variable "main_subscription_expiration_ttl" {
  description = "Inactivity TTL for the Balance Worker subscription. Set to an empty string to never expire."
  type        = string
  default     = ""
}

variable "application_dlq_subscription_expiration_ttl" {
  description = "Inactivity TTL for the application DLQ inspection subscription. Set to an empty string to never expire."
  type        = string
  default     = "2592000s"
}

variable "technical_dlq_subscription_expiration_ttl" {
  description = "Inactivity TTL for the technical DLQ inspection subscription. Set to an empty string to never expire."
  type        = string
  default     = "2592000s"
}

variable "database_instance_name" {
  description = "Cloud SQL PostgreSQL instance name for dev."
  type        = string
  default     = "poc-ledger-dev-postgres"
}

variable "database_name" {
  description = "Application database name for dev."
  type        = string
  default     = "ledger_dev"
}

variable "database_user" {
  description = "Application database user for dev."
  type        = string
  default     = "ledger_app"
}

variable "database_password" {
  description = "Application database password for dev. Provide only through ignored terraform.tfvars or TF_VAR_database_password."
  type        = string
  sensitive   = true
}

variable "database_version" {
  description = "Cloud SQL PostgreSQL database version for dev."
  type        = string
  default     = "POSTGRES_16"
}

variable "database_tier" {
  description = "Cloud SQL machine tier for dev."
  type        = string
  default     = "db-f1-micro"

  validation {
    condition     = contains(["db-f1-micro", "db-g1-small"], var.database_tier)
    error_message = "database_tier must remain a low-cost shared-core tier for the disposable dev POC."
  }
}

variable "database_availability_type" {
  description = "Cloud SQL availability type for dev."
  type        = string
  default     = "ZONAL"

  validation {
    condition     = var.database_availability_type == "ZONAL"
    error_message = "database_availability_type must remain ZONAL for the disposable dev POC."
  }
}

variable "database_deletion_protection" {
  description = "Whether the dev Cloud SQL instance is protected from deletion."
  type        = bool
  default     = false
}

variable "database_backup_enabled" {
  description = "Whether automated Cloud SQL backups are enabled for dev."
  type        = bool
  default     = false
}

variable "database_backup_start_time" {
  description = "UTC backup start time in HH:MM format."
  type        = string
  default     = "03:00"
}

variable "database_point_in_time_recovery_enabled" {
  description = "Whether Cloud SQL point-in-time recovery is enabled for dev."
  type        = bool
  default     = false
}

variable "database_transaction_log_retention_days" {
  description = "Number of days to retain transaction logs for point-in-time recovery."
  type        = number
  default     = 7
}

variable "database_backup_location" {
  description = "Optional backup location. Use null to let Cloud SQL choose the default."
  type        = string
  default     = null
}

variable "database_disk_size" {
  description = "Initial Cloud SQL disk size in GB for dev."
  type        = number
  default     = 10

  validation {
    condition     = var.database_disk_size == 10
    error_message = "database_disk_size must remain 10 GB for the disposable dev POC."
  }
}

variable "database_disk_autoresize" {
  description = "Whether Cloud SQL can automatically increase disk size in dev."
  type        = bool
  default     = false

  validation {
    condition     = !var.database_disk_autoresize
    error_message = "database_disk_autoresize must remain false for the disposable dev POC."
  }
}
