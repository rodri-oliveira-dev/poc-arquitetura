terraform {
  required_version = ">= 1.5.0, < 2.0.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = ">= 7.0.0, < 8.0.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

resource "google_project_service" "pubsub" {
  project = var.project_id
  service = "pubsub.googleapis.com"

  disable_on_destroy = false
}

module "pubsub_ledger_events" {
  source = "../../modules/pubsub-ledger-events"

  project_id                          = var.project_id
  region                              = var.region
  environment                         = "dev"
  app_name                            = "poc-ledger"
  ledger_events_topic_name            = "ledger.ledgerentry.created.dev"
  ledger_events_subscription_name     = "balance-service-ledger-events-dev"
  ledger_events_dlq_topic_name        = "ledger.ledgerentry.created.dlq.dev"
  ledger_events_dlq_subscription_name = "balance-service-ledger-events-dlq-dev"

  enable_message_ordering = true

  labels = {
    managed_by = "terraform"
  }

  depends_on = [google_project_service.pubsub]
}
