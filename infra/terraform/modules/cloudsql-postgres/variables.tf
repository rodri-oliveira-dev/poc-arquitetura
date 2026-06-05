variable "project_id" {
  description = "Google Cloud project ID where the Cloud SQL resources are created."
  type        = string

  validation {
    condition     = length(trimspace(var.project_id)) > 0
    error_message = "project_id must not be empty."
  }
}

variable "region" {
  description = "Google Cloud region where the Cloud SQL instance is created."
  type        = string

  validation {
    condition     = can(regex("^[a-z]+-[a-z0-9]+[0-9]$", var.region))
    error_message = "region must be a valid Google Cloud region name, such as us-central1."
  }
}

variable "environment" {
  description = "Environment identifier used in labels."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]*$", var.environment)) && length(var.environment) <= 63
    error_message = "environment must start with a lowercase letter, contain only lowercase letters, digits, or hyphens, and have at most 63 characters."
  }
}

variable "app_name" {
  description = "Application identifier used in labels."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]*$", var.app_name)) && length(var.app_name) <= 63
    error_message = "app_name must start with a lowercase letter, contain only lowercase letters, digits, or hyphens, and have at most 63 characters."
  }
}

variable "instance_name" {
  description = "Cloud SQL instance name."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{0,61}[a-z0-9]$", var.instance_name))
    error_message = "instance_name must start with a lowercase letter, contain only lowercase letters, digits, or hyphens, end with a lowercase letter or digit, and have at most 63 characters."
  }
}

variable "postgres_version" {
  description = "Cloud SQL PostgreSQL database version."
  type        = string
  default     = "POSTGRES_16"

  validation {
    condition     = can(regex("^POSTGRES_[0-9]+$", var.postgres_version))
    error_message = "postgres_version must use the Cloud SQL PostgreSQL format, such as POSTGRES_16."
  }
}

variable "tier" {
  description = "Cloud SQL machine tier."
  type        = string
  default     = "db-f1-micro"

  validation {
    condition     = length(trimspace(var.tier)) > 0
    error_message = "tier must not be empty."
  }
}

variable "availability_type" {
  description = "Cloud SQL availability type. Use ZONAL for cost-conscious dev and REGIONAL only when HA is explicitly required."
  type        = string
  default     = "ZONAL"

  validation {
    condition     = contains(["ZONAL", "REGIONAL"], var.availability_type)
    error_message = "availability_type must be either ZONAL or REGIONAL."
  }
}

variable "database_name" {
  description = "Application database name created inside the Cloud SQL instance."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9_]{0,62}$", var.database_name))
    error_message = "database_name must start with a lowercase letter, contain only lowercase letters, digits, or underscores, and have at most 63 characters."
  }
}

variable "database_user" {
  description = "Application database user created inside the Cloud SQL instance."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9_]{0,62}$", var.database_user))
    error_message = "database_user must start with a lowercase letter, contain only lowercase letters, digits, or underscores, and have at most 63 characters."
  }
}

variable "database_password" {
  description = "Application database user password. Provide it through an ignored terraform.tfvars file or TF_VAR_database_password."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.database_password) >= 16
    error_message = "database_password must have at least 16 characters."
  }
}

variable "deletion_protection" {
  description = "Whether Terraform and the Cloud SQL API protect the instance from deletion."
  type        = bool
  default     = true
}

variable "backup_configuration" {
  description = "Cloud SQL backup configuration. Defaults favor a safe dev baseline with backups and point-in-time recovery enabled."
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

  validation {
    condition     = can(regex("^([01][0-9]|2[0-3]):[0-5][0-9]$", var.backup_configuration.start_time))
    error_message = "backup_configuration.start_time must use HH:MM format."
  }

  validation {
    condition     = var.backup_configuration.transaction_log_retention_days >= 1 && var.backup_configuration.transaction_log_retention_days <= 7
    error_message = "backup_configuration.transaction_log_retention_days must be between 1 and 7."
  }
}

variable "labels" {
  description = "Additional labels merged with app, environment, and region labels."
  type        = map(string)
  default     = {}

  validation {
    condition = alltrue([
      for key, value in var.labels :
      can(regex("^[a-z][a-z0-9_-]{0,62}$", key)) &&
      can(regex("^[a-z0-9_-]{0,63}$", value))
    ])
    error_message = "labels keys must start with a lowercase letter and labels keys/values may contain only lowercase letters, digits, underscores, or hyphens."
  }
}
