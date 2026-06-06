output "ledger_events_topic_id" {
  description = "ID totalmente qualificado do topic principal de eventos Ledger."
  value       = google_pubsub_topic.ledger_events.id
}

output "ledger_events_topic_name" {
  description = "Nome do topic principal de eventos Ledger."
  value       = google_pubsub_topic.ledger_events.name
}

output "ledger_events_topic_map" {
  description = "Mapeamento de tipo de evento para nome de topic usado pelo producer Pub/Sub do Ledger Worker."
  value = {
    "LedgerEntryCreated.v1" = google_pubsub_topic.ledger_events.name
  }
}

output "ledger_events_subscription_id" {
  description = "ID totalmente qualificado da pull subscription do Balance Worker."
  value       = google_pubsub_subscription.balance_ledger_events.id
}

output "ledger_events_subscription_name" {
  description = "Nome da pull subscription do Balance Worker."
  value       = google_pubsub_subscription.balance_ledger_events.name
}

output "application_dlq_topic_id" {
  description = "ID totalmente qualificado do topic da DLQ de aplicacao."
  value       = google_pubsub_topic.application_dlq.id
}

output "application_dlq_topic_name" {
  description = "Nome do topic da DLQ de aplicacao."
  value       = google_pubsub_topic.application_dlq.name
}

output "technical_dlq_topic_id" {
  description = "ID totalmente qualificado do topic da DLQ tecnica."
  value       = google_pubsub_topic.technical_dlq.id
}

output "technical_dlq_topic_name" {
  description = "Nome do topic da DLQ tecnica."
  value       = google_pubsub_topic.technical_dlq.name
}

output "application_dlq_subscription_id" {
  description = "ID totalmente qualificado da pull subscription que retem mensagens da DLQ de aplicacao."
  value       = google_pubsub_subscription.application_dlq.id
}

output "application_dlq_subscription_name" {
  description = "Nome da pull subscription que retem mensagens da DLQ de aplicacao."
  value       = google_pubsub_subscription.application_dlq.name
}

output "technical_dlq_subscription_id" {
  description = "ID totalmente qualificado da pull subscription que retem mensagens da DLQ tecnica."
  value       = google_pubsub_subscription.technical_dlq.id
}

output "technical_dlq_subscription_name" {
  description = "Nome da pull subscription que retem mensagens da DLQ tecnica."
  value       = google_pubsub_subscription.technical_dlq.name
}

output "ledger_worker_service_account_email" {
  description = "E-mail da service account dedicada ao Ledger Worker."
  value       = google_service_account.ledger_worker.email
}

output "balance_worker_service_account_email" {
  description = "E-mail da service account dedicada ao Balance Worker."
  value       = google_service_account.balance_worker.email
}

output "enable_message_ordering" {
  description = "Define se entrega ordenada fica habilitada na subscription do Balance Worker."
  value       = google_pubsub_subscription.balance_ledger_events.enable_message_ordering
}

output "enable_exactly_once_delivery" {
  description = "Define se exactly-once delivery fica habilitado na subscription do Balance Worker."
  value       = google_pubsub_subscription.balance_ledger_events.enable_exactly_once_delivery
}

output "enable_technical_dead_letter" {
  description = "Define se a dead-letter policy tecnica nativa fica habilitada na subscription do Balance Worker."
  value       = var.enable_technical_dead_letter
}

output "ack_deadline_seconds" {
  description = "Acknowledgement deadline configurado na subscription do Balance Worker."
  value       = google_pubsub_subscription.balance_ledger_events.ack_deadline_seconds
}

output "message_retention_duration" {
  description = "Duracao de retencao de mensagens configurada em todas as subscriptions."
  value       = var.message_retention_duration
}

output "retain_acked_messages" {
  description = "Define se mensagens confirmadas permanecem retidas em todas as subscriptions."
  value       = var.retain_acked_messages
}

output "main_subscription_expiration_ttl" {
  description = "TTL de inatividade configurado na subscription do Balance Worker. String vazia significa que nunca expira."
  value       = var.main_subscription_expiration_ttl
}

output "application_dlq_subscription_expiration_ttl" {
  description = "TTL de inatividade configurado na subscription de inspecao da DLQ de aplicacao. String vazia significa que nunca expira."
  value       = var.application_dlq_subscription_expiration_ttl
}

output "technical_dlq_subscription_expiration_ttl" {
  description = "TTL de inatividade configurado na subscription de inspecao da DLQ tecnica. String vazia significa que nunca expira."
  value       = var.technical_dlq_subscription_expiration_ttl
}
