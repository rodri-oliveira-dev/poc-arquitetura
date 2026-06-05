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

output "application_dlq_topic_id" {
  description = "Fully qualified ID of the application DLQ topic."
  value       = module.pubsub_ledger_events.application_dlq_topic_id
}

output "application_dlq_topic_name" {
  description = "Pub/Sub topic name used by the Balance Worker for application-classified DLQ messages."
  value       = module.pubsub_ledger_events.application_dlq_topic_name
}

output "technical_dlq_topic_id" {
  description = "Fully qualified ID of the technical DLQ topic."
  value       = module.pubsub_ledger_events.technical_dlq_topic_id
}

output "technical_dlq_topic_name" {
  description = "Pub/Sub topic name used by the native dead-letter policy for technical delivery failures."
  value       = module.pubsub_ledger_events.technical_dlq_topic_name
}

output "application_dlq_subscription_id" {
  description = "Fully qualified ID of the application DLQ inspection subscription."
  value       = module.pubsub_ledger_events.application_dlq_subscription_id
}

output "application_dlq_subscription_name" {
  description = "Pub/Sub subscription name used for application DLQ operational inspection."
  value       = module.pubsub_ledger_events.application_dlq_subscription_name
}

output "technical_dlq_subscription_id" {
  description = "Fully qualified ID of the technical DLQ inspection subscription."
  value       = module.pubsub_ledger_events.technical_dlq_subscription_id
}

output "technical_dlq_subscription_name" {
  description = "Pub/Sub subscription name used for technical DLQ operational inspection."
  value       = module.pubsub_ledger_events.technical_dlq_subscription_name
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

output "enable_technical_dead_letter" {
  description = "Whether the native technical dead-letter policy is enabled on the Balance subscription."
  value       = module.pubsub_ledger_events.enable_technical_dead_letter
}

output "ack_deadline_seconds" {
  description = "Acknowledgement deadline configured on the Balance subscription."
  value       = module.pubsub_ledger_events.ack_deadline_seconds
}

output "message_retention_duration" {
  description = "Message retention duration configured on all Pub/Sub subscriptions."
  value       = module.pubsub_ledger_events.message_retention_duration
}

output "retain_acked_messages" {
  description = "Whether acknowledged messages remain retained on all Pub/Sub subscriptions."
  value       = module.pubsub_ledger_events.retain_acked_messages
}

output "main_subscription_expiration_ttl" {
  description = "Inactivity TTL configured on the Balance subscription. An empty string means that it never expires."
  value       = module.pubsub_ledger_events.main_subscription_expiration_ttl
}

output "application_dlq_subscription_expiration_ttl" {
  description = "Inactivity TTL configured on the application DLQ inspection subscription. An empty string means that it never expires."
  value       = module.pubsub_ledger_events.application_dlq_subscription_expiration_ttl
}

output "technical_dlq_subscription_expiration_ttl" {
  description = "Inactivity TTL configured on the technical DLQ inspection subscription. An empty string means that it never expires."
  value       = module.pubsub_ledger_events.technical_dlq_subscription_expiration_ttl
}

output "database_instance_name" {
  description = "Cloud SQL PostgreSQL instance name for dev."
  value       = module.cloudsql_postgres.instance_name
}

output "database_instance_connection_name" {
  description = "Cloud SQL instance connection name used by Cloud SQL Auth Proxy."
  value       = module.cloudsql_postgres.instance_connection_name
}

output "database_name" {
  description = "Cloud SQL application database name."
  value       = module.cloudsql_postgres.database_name
}

output "database_user" {
  description = "Cloud SQL application database user."
  value       = module.cloudsql_postgres.database_user
}

output "database_public_ip_address" {
  description = "Cloud SQL public IPv4 address, when available."
  value       = module.cloudsql_postgres.public_ip_address
}
