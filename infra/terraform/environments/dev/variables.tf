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

variable "cloudsql_instance_name" {
  description = "Cloud SQL PostgreSQL instance name for dev."
  type        = string
  default     = "poc-ledger-dev-postgres"
}

variable "cloudsql_postgres_version" {
  description = "Cloud SQL PostgreSQL database version for dev."
  type        = string
  default     = "POSTGRES_16"
}

variable "cloudsql_tier" {
  description = "Cloud SQL machine tier for dev."
  type        = string
  default     = "db-f1-micro"
}

variable "cloudsql_availability_type" {
  description = "Cloud SQL availability type for dev."
  type        = string
  default     = "ZONAL"
}

variable "cloudsql_database_name" {
  description = "Application database name for dev."
  type        = string
  default     = "ledger_dev"
}

variable "cloudsql_database_user" {
  description = "Application database user for dev."
  type        = string
  default     = "ledger_app"
}

variable "cloudsql_database_password" {
  description = "Application database password for dev. Provide only through ignored terraform.tfvars or TF_VAR_cloudsql_database_password."
  type        = string
  sensitive   = true
}

variable "cloudsql_deletion_protection" {
  description = "Whether the dev Cloud SQL instance is protected from deletion."
  type        = bool
  default     = true
}

variable "cloudsql_backup_configuration" {
  description = "Cloud SQL backup configuration for dev."
  type = object({
    enabled                        = bool
    start_time                     = string
    point_in_time_recovery_enabled = bool
    transaction_log_retention_days = number
    location                       = optional(string)
  })
  default = {
    enabled                        = true
    start_time                     = "03:00"
    point_in_time_recovery_enabled = true
    transaction_log_retention_days = 7
    location                       = null
  }
}
