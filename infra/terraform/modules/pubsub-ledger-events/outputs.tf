output "ledger_events_topic_id" {
  description = "Fully qualified ID of the main Ledger events topic."
  value       = google_pubsub_topic.ledger_events.id
}

output "ledger_events_topic_name" {
  description = "Name of the main Ledger events topic."
  value       = google_pubsub_topic.ledger_events.name
}

output "ledger_events_topic_map" {
  description = "Event type to topic name mapping used by the Ledger Worker Pub/Sub producer."
  value = {
    "LedgerEntryCreated.v1" = google_pubsub_topic.ledger_events.name
  }
}

output "ledger_events_subscription_id" {
  description = "Fully qualified ID of the Balance Worker pull subscription."
  value       = google_pubsub_subscription.balance_ledger_events.id
}

output "ledger_events_subscription_name" {
  description = "Name of the Balance Worker pull subscription."
  value       = google_pubsub_subscription.balance_ledger_events.name
}

output "application_dlq_topic_id" {
  description = "Fully qualified ID of the application DLQ topic."
  value       = google_pubsub_topic.application_dlq.id
}

output "application_dlq_topic_name" {
  description = "Name of the application DLQ topic."
  value       = google_pubsub_topic.application_dlq.name
}

output "technical_dlq_topic_id" {
  description = "Fully qualified ID of the technical DLQ topic."
  value       = google_pubsub_topic.technical_dlq.id
}

output "technical_dlq_topic_name" {
  description = "Name of the technical DLQ topic."
  value       = google_pubsub_topic.technical_dlq.name
}

output "application_dlq_subscription_id" {
  description = "Fully qualified ID of the pull subscription that retains application DLQ messages."
  value       = google_pubsub_subscription.application_dlq.id
}

output "application_dlq_subscription_name" {
  description = "Name of the pull subscription that retains application DLQ messages."
  value       = google_pubsub_subscription.application_dlq.name
}

output "technical_dlq_subscription_id" {
  description = "Fully qualified ID of the pull subscription that retains technical DLQ messages."
  value       = google_pubsub_subscription.technical_dlq.id
}

output "technical_dlq_subscription_name" {
  description = "Name of the pull subscription that retains technical DLQ messages."
  value       = google_pubsub_subscription.technical_dlq.name
}

output "ledger_worker_service_account_email" {
  description = "Email of the dedicated Ledger Worker service account."
  value       = google_service_account.ledger_worker.email
}

output "balance_worker_service_account_email" {
  description = "Email of the dedicated Balance Worker service account."
  value       = google_service_account.balance_worker.email
}

output "enable_message_ordering" {
  description = "Whether ordered delivery is enabled on the Balance Worker subscription."
  value       = google_pubsub_subscription.balance_ledger_events.enable_message_ordering
}

output "enable_exactly_once_delivery" {
  description = "Whether exactly-once delivery is enabled on the Balance Worker subscription."
  value       = google_pubsub_subscription.balance_ledger_events.enable_exactly_once_delivery
}

output "enable_technical_dead_letter" {
  description = "Whether the native technical dead-letter policy is enabled on the Balance Worker subscription."
  value       = var.enable_technical_dead_letter
}

output "ack_deadline_seconds" {
  description = "Acknowledgement deadline configured on the Balance Worker subscription."
  value       = google_pubsub_subscription.balance_ledger_events.ack_deadline_seconds
}

output "message_retention_duration" {
  description = "Message retention duration configured on all subscriptions."
  value       = var.message_retention_duration
}

output "retain_acked_messages" {
  description = "Whether acknowledged messages remain retained on all subscriptions."
  value       = var.retain_acked_messages
}

output "main_subscription_expiration_ttl" {
  description = "Inactivity TTL configured on the Balance Worker subscription. An empty string means that it never expires."
  value       = var.main_subscription_expiration_ttl
}

output "application_dlq_subscription_expiration_ttl" {
  description = "Inactivity TTL configured on the application DLQ inspection subscription. An empty string means that it never expires."
  value       = var.application_dlq_subscription_expiration_ttl
}

output "technical_dlq_subscription_expiration_ttl" {
  description = "Inactivity TTL configured on the technical DLQ inspection subscription. An empty string means that it never expires."
  value       = var.technical_dlq_subscription_expiration_ttl
}
