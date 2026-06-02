terraform {
  required_version = ">= 1.5.0, < 2.0.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = ">= 7.0.0, < 8.0.0"
    }
    google-beta = {
      source  = "hashicorp/google-beta"
      version = ">= 7.0.0, < 8.0.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

provider "google-beta" {
  project = var.project_id
  region  = var.region
}

resource "google_project_service" "pubsub" {
  project = var.project_id
  service = "pubsub.googleapis.com"

  disable_on_destroy = false
}

resource "google_project_service_identity" "pubsub" {
  provider = google-beta

  project = var.project_id
  service = google_project_service.pubsub.service
}

module "pubsub_ledger_events" {
  source = "../../modules/pubsub-ledger-events"

  project_id                            = var.project_id
  pubsub_service_agent_member           = google_project_service_identity.pubsub.member
  region                                = var.region
  allowed_persistence_regions           = var.allowed_persistence_regions
  enforce_in_transit                    = var.enforce_in_transit
  service_account_token_creator_members = var.service_account_token_creator_members
  environment                           = "dev"
  app_name                              = "poc-ledger"
  ledger_events_topic_name              = "ledger.ledgerentry.created.dev"
  ledger_events_subscription_name       = "balance-service-ledger-events-dev"
  application_dlq_topic_name            = "ledger.ledgerentry.created.dlq.dev"
  technical_dlq_topic_name              = "ledger.ledgerentry.created.technical.dlq.dev"
  application_dlq_subscription_name     = "balance-service-ledger-events-dlq-dev"
  technical_dlq_subscription_name       = "balance-service-ledger-events-technical-dlq-dev"

  enable_message_ordering                     = true
  enable_technical_dead_letter                = var.enable_technical_dead_letter
  main_subscription_expiration_ttl            = var.main_subscription_expiration_ttl
  application_dlq_subscription_expiration_ttl = var.application_dlq_subscription_expiration_ttl
  technical_dlq_subscription_expiration_ttl   = var.technical_dlq_subscription_expiration_ttl

  labels = {
    managed_by = "terraform"
  }

  depends_on = [google_project_service.pubsub]
}
