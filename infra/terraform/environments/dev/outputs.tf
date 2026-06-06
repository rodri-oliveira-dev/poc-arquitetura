output "project_id" {
  description = "ID do projeto Google Cloud usado pelo ambiente dev."
  value       = var.project_id
}

output "region" {
  description = "Regiao Google Cloud usada pelo ambiente dev."
  value       = var.region
}

output "ledger_events_topic_name" {
  description = "Nome do topic Pub/Sub dos eventos LedgerEntryCreated."
  value       = module.pubsub_ledger_events.ledger_events_topic_name
}

output "ledger_events_topic_map" {
  description = "Mapeamento de tipo de evento para nome de topic Pub/Sub usado pelo producer do Ledger Worker."
  value       = module.pubsub_ledger_events.ledger_events_topic_map
}

output "ledger_events_subscription_name" {
  description = "Nome da subscription Pub/Sub consumida pelo Balance Worker."
  value       = module.pubsub_ledger_events.ledger_events_subscription_name
}

output "application_dlq_topic_id" {
  description = "ID totalmente qualificado do topic da DLQ de aplicacao."
  value       = module.pubsub_ledger_events.application_dlq_topic_id
}

output "application_dlq_topic_name" {
  description = "Nome do topic Pub/Sub usado pelo Balance Worker para mensagens classificadas como DLQ de aplicacao."
  value       = module.pubsub_ledger_events.application_dlq_topic_name
}

output "technical_dlq_topic_id" {
  description = "ID totalmente qualificado do topic da DLQ tecnica."
  value       = module.pubsub_ledger_events.technical_dlq_topic_id
}

output "technical_dlq_topic_name" {
  description = "Nome do topic Pub/Sub usado pela dead-letter policy nativa para falhas tecnicas de entrega."
  value       = module.pubsub_ledger_events.technical_dlq_topic_name
}

output "application_dlq_subscription_id" {
  description = "ID totalmente qualificado da subscription de inspecao da DLQ de aplicacao."
  value       = module.pubsub_ledger_events.application_dlq_subscription_id
}

output "application_dlq_subscription_name" {
  description = "Nome da subscription Pub/Sub usada para inspecao operacional da DLQ de aplicacao."
  value       = module.pubsub_ledger_events.application_dlq_subscription_name
}

output "technical_dlq_subscription_id" {
  description = "ID totalmente qualificado da subscription de inspecao da DLQ tecnica."
  value       = module.pubsub_ledger_events.technical_dlq_subscription_id
}

output "technical_dlq_subscription_name" {
  description = "Nome da subscription Pub/Sub usada para inspecao operacional da DLQ tecnica."
  value       = module.pubsub_ledger_events.technical_dlq_subscription_name
}

output "ledger_worker_service_account_email" {
  description = "E-mail da service account dedicada ao adapter Pub/Sub do Ledger Worker."
  value       = module.pubsub_ledger_events.ledger_worker_service_account_email
}

output "balance_worker_service_account_email" {
  description = "E-mail da service account dedicada ao adapter Pub/Sub do Balance Worker."
  value       = module.pubsub_ledger_events.balance_worker_service_account_email
}

output "enable_message_ordering" {
  description = "Define se o producer do Ledger e a subscription do Balance devem habilitar ordenacao de mensagens."
  value       = module.pubsub_ledger_events.enable_message_ordering
}

output "enable_exactly_once_delivery" {
  description = "Define se exactly-once delivery fica habilitado na subscription do Balance."
  value       = module.pubsub_ledger_events.enable_exactly_once_delivery
}

output "enable_technical_dead_letter" {
  description = "Define se a dead-letter policy tecnica nativa fica habilitada na subscription do Balance."
  value       = module.pubsub_ledger_events.enable_technical_dead_letter
}

output "ack_deadline_seconds" {
  description = "Acknowledgement deadline configurado na subscription do Balance."
  value       = module.pubsub_ledger_events.ack_deadline_seconds
}

output "message_retention_duration" {
  description = "Duracao de retencao de mensagens configurada em todas as subscriptions Pub/Sub."
  value       = module.pubsub_ledger_events.message_retention_duration
}

output "retain_acked_messages" {
  description = "Define se mensagens confirmadas permanecem retidas em todas as subscriptions Pub/Sub."
  value       = module.pubsub_ledger_events.retain_acked_messages
}

output "main_subscription_expiration_ttl" {
  description = "TTL de inatividade configurado na subscription do Balance. String vazia significa que nunca expira."
  value       = module.pubsub_ledger_events.main_subscription_expiration_ttl
}

output "application_dlq_subscription_expiration_ttl" {
  description = "TTL de inatividade configurado na subscription de inspecao da DLQ de aplicacao. String vazia significa que nunca expira."
  value       = module.pubsub_ledger_events.application_dlq_subscription_expiration_ttl
}

output "technical_dlq_subscription_expiration_ttl" {
  description = "TTL de inatividade configurado na subscription de inspecao da DLQ tecnica. String vazia significa que nunca expira."
  value       = module.pubsub_ledger_events.technical_dlq_subscription_expiration_ttl
}

output "database_instance_name" {
  description = "Nome da instancia Cloud SQL PostgreSQL para dev."
  value       = module.cloudsql_postgres.instance_name
}

output "database_instance_connection_name" {
  description = "Nome de conexao da instancia Cloud SQL usado pelo Cloud SQL Auth Proxy."
  value       = module.cloudsql_postgres.instance_connection_name
}

output "database_name" {
  description = "Nome do database da aplicacao no Cloud SQL."
  value       = module.cloudsql_postgres.database_name
}

output "database_user" {
  description = "Usuario do database da aplicacao no Cloud SQL."
  value       = module.cloudsql_postgres.database_user
}

output "database_public_ip_address" {
  description = "Endereco IPv4 publico do Cloud SQL, quando disponivel."
  value       = module.cloudsql_postgres.public_ip_address
}
