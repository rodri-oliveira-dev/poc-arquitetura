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

  project_id                        = var.project_id
  region                            = var.region
  environment                       = "dev"
  app_name                          = "poc-ledger"
  ledger_events_topic_name          = "ledger.ledgerentry.created.dev"
  ledger_events_subscription_name   = "balance-service-ledger-events-dev"
  application_dlq_topic_name        = "ledger.ledgerentry.created.dlq.dev"
  technical_dlq_topic_name          = "ledger.ledgerentry.created.technical.dlq.dev"
  application_dlq_subscription_name = "balance-service-ledger-events-dlq-dev"
  technical_dlq_subscription_name   = "balance-service-ledger-events-technical-dlq-dev"

  enable_message_ordering      = true
  enable_technical_dead_letter = var.enable_technical_dead_letter

  labels = {
    managed_by = "terraform"
  }

  depends_on = [google_project_service.pubsub]
}
