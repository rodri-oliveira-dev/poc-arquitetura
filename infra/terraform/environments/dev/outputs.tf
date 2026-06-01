output "project_id" {
  description = "Google Cloud project ID used by the dev environment."
  value       = var.project_id
}

output "region" {
  description = "Google Cloud region used by the dev environment."
  value       = var.region
}

output "ledger_events_topic_name" {
  description = "Pub/Sub topic name for LedgerEntryCreated events."
  value       = module.pubsub_ledger_events.ledger_events_topic_name
}

output "ledger_events_subscription_name" {
  description = "Pub/Sub subscription name consumed by the Balance Worker."
  value       = module.pubsub_ledger_events.ledger_events_subscription_name
}

output "ledger_events_dlq_topic_name" {
  description = "Pub/Sub topic name for technical and application DLQ messages."
  value       = module.pubsub_ledger_events.ledger_events_dlq_topic_name
}

output "ledger_events_dlq_subscription_name" {
  description = "Pub/Sub subscription name used for DLQ operational inspection."
  value       = module.pubsub_ledger_events.ledger_events_dlq_subscription_name
}

output "ledger_worker_service_account_email" {
  description = "Dedicated service account email for the future Ledger Worker Pub/Sub adapter."
  value       = module.pubsub_ledger_events.ledger_worker_service_account_email
}

output "balance_worker_service_account_email" {
  description = "Dedicated service account email for the future Balance Worker Pub/Sub adapter."
  value       = module.pubsub_ledger_events.balance_worker_service_account_email
}
