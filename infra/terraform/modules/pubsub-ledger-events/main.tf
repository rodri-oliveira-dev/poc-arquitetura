terraform {
  required_version = ">= 1.5.0, < 2.0.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = ">= 7.0.0, < 8.0.0"
    }
  }
}

data "google_project" "current" {
  project_id = var.project_id
}

locals {
  resource_prefix = "${var.app_name}-${var.environment}"

  ledger_worker_service_account_id  = "${substr(local.resource_prefix, 0, 13)}-ledger-${substr(md5(local.resource_prefix), 0, 6)}"
  balance_worker_service_account_id = "${substr(local.resource_prefix, 0, 13)}-balance-${substr(md5(local.resource_prefix), 0, 6)}"
  pubsub_service_agent_member       = "serviceAccount:service-${data.google_project.current.number}@gcp-sa-pubsub.iam.gserviceaccount.com"

  common_labels = merge(var.labels, {
    app         = var.app_name
    environment = var.environment
    region      = var.region
  })
}

resource "google_pubsub_topic" "ledger_events" {
  project = var.project_id
  name    = var.ledger_events_topic_name
  labels  = local.common_labels
}

moved {
  from = google_pubsub_topic.ledger_events_dlq
  to   = google_pubsub_topic.application_dlq
}

resource "google_pubsub_topic" "application_dlq" {
  project = var.project_id
  name    = var.application_dlq_topic_name
  labels  = local.common_labels
}

resource "google_pubsub_topic" "technical_dlq" {
  project = var.project_id
  name    = var.technical_dlq_topic_name
  labels  = local.common_labels
}

resource "google_pubsub_subscription" "balance_ledger_events" {
  project = var.project_id
  name    = var.ledger_events_subscription_name
  topic   = google_pubsub_topic.ledger_events.id
  labels  = local.common_labels

  ack_deadline_seconds         = var.ack_deadline_seconds
  message_retention_duration   = var.message_retention_duration
  retain_acked_messages        = var.retain_acked_messages
  enable_message_ordering      = var.enable_message_ordering
  enable_exactly_once_delivery = var.enable_exactly_once_delivery

  expiration_policy {
    ttl = var.main_subscription_expiration_ttl
  }

  retry_policy {
    minimum_backoff = var.min_retry_backoff
    maximum_backoff = var.max_retry_backoff
  }

  dynamic "dead_letter_policy" {
    for_each = var.enable_technical_dead_letter ? [1] : []

    content {
      dead_letter_topic     = google_pubsub_topic.technical_dlq.id
      max_delivery_attempts = var.max_delivery_attempts
    }
  }

  lifecycle {
    precondition {
      condition = var.main_subscription_expiration_ttl == "" || try(
        tonumber(trimsuffix(var.main_subscription_expiration_ttl, "s")) > tonumber(trimsuffix(var.message_retention_duration, "s")),
        false
      )
      error_message = "main_subscription_expiration_ttl must be empty to never expire or greater than message_retention_duration."
    }
  }
}

moved {
  from = google_pubsub_subscription.ledger_events_dlq
  to   = google_pubsub_subscription.application_dlq
}

resource "google_pubsub_subscription" "application_dlq" {
  project = var.project_id
  name    = var.application_dlq_subscription_name
  topic   = google_pubsub_topic.application_dlq.id
  labels  = local.common_labels

  message_retention_duration = var.message_retention_duration
  retain_acked_messages      = var.retain_acked_messages

  expiration_policy {
    ttl = var.application_dlq_subscription_expiration_ttl
  }

  lifecycle {
    precondition {
      condition = var.application_dlq_subscription_expiration_ttl == "" || try(
        tonumber(trimsuffix(var.application_dlq_subscription_expiration_ttl, "s")) > tonumber(trimsuffix(var.message_retention_duration, "s")),
        false
      )
      error_message = "application_dlq_subscription_expiration_ttl must be empty to never expire or greater than message_retention_duration."
    }
  }
}

resource "google_pubsub_subscription" "technical_dlq" {
  project = var.project_id
  name    = var.technical_dlq_subscription_name
  topic   = google_pubsub_topic.technical_dlq.id
  labels  = local.common_labels

  message_retention_duration = var.message_retention_duration
  retain_acked_messages      = var.retain_acked_messages

  expiration_policy {
    ttl = var.technical_dlq_subscription_expiration_ttl
  }

  lifecycle {
    precondition {
      condition = var.technical_dlq_subscription_expiration_ttl == "" || try(
        tonumber(trimsuffix(var.technical_dlq_subscription_expiration_ttl, "s")) > tonumber(trimsuffix(var.message_retention_duration, "s")),
        false
      )
      error_message = "technical_dlq_subscription_expiration_ttl must be empty to never expire or greater than message_retention_duration."
    }
  }
}

resource "google_service_account" "ledger_worker" {
  project      = var.project_id
  account_id   = local.ledger_worker_service_account_id
  display_name = "${var.app_name} ${var.environment} Ledger Worker"
  description  = "Publishes Ledger events to Pub/Sub."
}

resource "google_service_account" "balance_worker" {
  project      = var.project_id
  account_id   = local.balance_worker_service_account_id
  display_name = "${var.app_name} ${var.environment} Balance Worker"
  description  = "Consumes Ledger events and publishes application DLQ messages."
}

resource "google_pubsub_topic_iam_member" "ledger_worker_publish_ledger_events" {
  project = var.project_id
  topic   = google_pubsub_topic.ledger_events.name
  role    = "roles/pubsub.publisher"
  member  = google_service_account.ledger_worker.member
}

resource "google_pubsub_subscription_iam_member" "balance_worker_consume_ledger_events" {
  project      = var.project_id
  subscription = google_pubsub_subscription.balance_ledger_events.name
  role         = "roles/pubsub.subscriber"
  member       = google_service_account.balance_worker.member
}

resource "google_pubsub_topic_iam_member" "balance_worker_publish_application_dlq" {
  project = var.project_id
  topic   = google_pubsub_topic.application_dlq.name
  role    = "roles/pubsub.publisher"
  member  = google_service_account.balance_worker.member
}

resource "google_pubsub_topic_iam_member" "pubsub_service_agent_publish_technical_dlq" {
  count = var.enable_technical_dead_letter ? 1 : 0

  project = var.project_id
  topic   = google_pubsub_topic.technical_dlq.name
  role    = "roles/pubsub.publisher"
  member  = local.pubsub_service_agent_member
}

resource "google_pubsub_subscription_iam_member" "pubsub_service_agent_ack_ledger_events" {
  count = var.enable_technical_dead_letter ? 1 : 0

  project      = var.project_id
  subscription = google_pubsub_subscription.balance_ledger_events.name
  role         = "roles/pubsub.subscriber"
  member       = local.pubsub_service_agent_member
}
