output "ledger_events_topic_id" {
  description = "Fully qualified ID of the main Ledger events topic."
  value       = google_pubsub_topic.ledger_events.id
}

output "ledger_events_topic_name" {
  description = "Name of the main Ledger events topic."
  value       = google_pubsub_topic.ledger_events.name
}

output "ledger_events_subscription_id" {
  description = "Fully qualified ID of the Balance Worker pull subscription."
  value       = google_pubsub_subscription.balance_ledger_events.id
}

output "ledger_events_subscription_name" {
  description = "Name of the Balance Worker pull subscription."
  value       = google_pubsub_subscription.balance_ledger_events.name
}

output "ledger_events_dlq_topic_id" {
  description = "Fully qualified ID of the Ledger events DLQ topic."
  value       = google_pubsub_topic.ledger_events_dlq.id
}

output "ledger_events_dlq_topic_name" {
  description = "Name of the Ledger events DLQ topic."
  value       = google_pubsub_topic.ledger_events_dlq.name
}

output "ledger_events_dlq_subscription_id" {
  description = "Fully qualified ID of the pull subscription that retains Ledger events DLQ messages."
  value       = google_pubsub_subscription.ledger_events_dlq.id
}

output "ledger_events_dlq_subscription_name" {
  description = "Name of the pull subscription that retains Ledger events DLQ messages."
  value       = google_pubsub_subscription.ledger_events_dlq.name
}

output "ledger_worker_service_account_email" {
  description = "Email of the dedicated Ledger Worker service account."
  value       = google_service_account.ledger_worker.email
}

output "balance_worker_service_account_email" {
  description = "Email of the dedicated Balance Worker service account."
  value       = google_service_account.balance_worker.email
}
