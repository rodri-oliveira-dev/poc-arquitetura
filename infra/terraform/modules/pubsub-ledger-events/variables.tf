variable "project_id" {
  description = "Google Cloud project ID where Pub/Sub and service account resources are created."
  type        = string

  validation {
    condition     = length(trimspace(var.project_id)) > 0
    error_message = "project_id must not be empty."
  }
}

variable "region" {
  description = "Deployment region used as resource metadata. Pub/Sub topics and subscriptions are global resources."
  type        = string

  validation {
    condition     = can(regex("^[a-z]+-[a-z0-9]+[0-9]$", var.region))
    error_message = "region must be a valid Google Cloud region name, such as us-central1."
  }
}

variable "environment" {
  description = "Environment identifier used in labels and dedicated service account IDs."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]*$", var.environment)) && length(var.environment) <= 63
    error_message = "environment must start with a lowercase letter, contain only lowercase letters, digits, or hyphens, and have at most 63 characters."
  }
}

variable "app_name" {
  description = "Application identifier used in labels and dedicated service account IDs."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]*$", var.app_name)) && length(var.app_name) <= 63
    error_message = "app_name must start with a lowercase letter, contain only lowercase letters, digits, or hyphens, and have at most 63 characters."
  }
}

variable "ledger_events_topic_name" {
  description = "Name of the main Ledger events topic."
  type        = string
}

variable "ledger_events_subscription_name" {
  description = "Name of the pull subscription consumed by the Balance Worker."
  type        = string
}

variable "application_dlq_topic_name" {
  description = "Name of the topic used by the Balance Worker for application-classified DLQ messages."
  type        = string
}

variable "technical_dlq_topic_name" {
  description = "Name of the topic used by the native dead-letter policy for technical delivery failures."
  type        = string
}

variable "application_dlq_subscription_name" {
  description = "Name of the pull subscription that retains application DLQ messages for operational inspection."
  type        = string
}

variable "technical_dlq_subscription_name" {
  description = "Name of the pull subscription that retains technical DLQ messages for operational inspection."
  type        = string
}

variable "ack_deadline_seconds" {
  description = "Acknowledgement deadline in seconds for the Balance Worker pull subscription."
  type        = number
  default     = 30

  validation {
    condition     = floor(var.ack_deadline_seconds) == var.ack_deadline_seconds && var.ack_deadline_seconds >= 10 && var.ack_deadline_seconds <= 600
    error_message = "ack_deadline_seconds must be an integer between 10 and 600."
  }
}

variable "message_retention_duration" {
  description = "Subscription message retention duration using the Google duration format, such as 604800s."
  type        = string
  default     = "604800s"
}

variable "retain_acked_messages" {
  description = "Whether subscriptions retain acknowledged messages during the retention window."
  type        = bool
  default     = false
}

variable "enable_message_ordering" {
  description = "Whether the Balance Worker subscription enables ordered delivery for messages with the same ordering key."
  type        = bool
  default     = false
}

variable "enable_exactly_once_delivery" {
  description = "Whether the Balance Worker subscription enables Pub/Sub exactly-once delivery."
  type        = bool
  default     = false
}

variable "min_retry_backoff" {
  description = "Minimum retry backoff using the Google duration format, such as 10s."
  type        = string
  default     = "10s"
}

variable "max_retry_backoff" {
  description = "Maximum retry backoff using the Google duration format, such as 600s."
  type        = string
  default     = "600s"
}

variable "max_delivery_attempts" {
  description = "Approximate number of delivery attempts before Pub/Sub forwards a message to the technical DLQ."
  type        = number
  default     = 5

  validation {
    condition     = floor(var.max_delivery_attempts) == var.max_delivery_attempts && var.max_delivery_attempts >= 5 && var.max_delivery_attempts <= 100
    error_message = "max_delivery_attempts must be an integer between 5 and 100."
  }
}

variable "labels" {
  description = "Additional labels merged with app, environment, and region labels."
  type        = map(string)
  default     = {}
}
