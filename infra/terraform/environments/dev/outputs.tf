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

output "ledger_events_topic_map" {
  description = "Event type to Pub/Sub topic name mapping for the Ledger Worker producer."
  value       = module.pubsub_ledger_events.ledger_events_topic_map
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
  description = "Dedicated service account email for the Ledger Worker Pub/Sub adapter."
  value       = module.pubsub_ledger_events.ledger_worker_service_account_email
}

output "balance_worker_service_account_email" {
  description = "Dedicated service account email for the Balance Worker Pub/Sub adapter."
  value       = module.pubsub_ledger_events.balance_worker_service_account_email
}

output "enable_message_ordering" {
  description = "Whether the Ledger producer and Balance subscription must enable message ordering."
  value       = module.pubsub_ledger_events.enable_message_ordering
}

output "enable_exactly_once_delivery" {
  description = "Whether exactly-once delivery is enabled on the Balance subscription."
  value       = module.pubsub_ledger_events.enable_exactly_once_delivery
}

output "ack_deadline_seconds" {
  description = "Acknowledgement deadline configured on the Balance subscription."
  value       = module.pubsub_ledger_events.ack_deadline_seconds
}
