output "instance_name" {
  description = "Nome da instancia Cloud SQL."
  value       = google_sql_database_instance.postgres.name
}

output "instance_connection_name" {
  description = "Nome de conexao da instancia Cloud SQL usado pelo Cloud SQL Auth Proxy."
  value       = google_sql_database_instance.postgres.connection_name
}

output "database_name" {
  description = "Nome do database da aplicacao."
  value       = google_sql_database.database.name
}

output "database_user" {
  description = "Nome do usuario do database da aplicacao."
  value       = google_sql_user.application.name
}

output "public_ip_address" {
  description = "Endereco IPv4 publico atribuido a instancia Cloud SQL, quando disponivel."
  value       = try(google_sql_database_instance.postgres.public_ip_address, null)
}

output "connection_metadata" {
  description = "Metadados nao secretos de conexao Cloud SQL para configuracao da aplicacao e uso do Cloud SQL Auth Proxy."
  value = {
    instance_name            = google_sql_database_instance.postgres.name
    instance_connection_name = google_sql_database_instance.postgres.connection_name
    database_name            = google_sql_database.database.name
    database_user            = google_sql_user.application.name
    public_ip_address        = try(google_sql_database_instance.postgres.public_ip_address, null)
  }
}
