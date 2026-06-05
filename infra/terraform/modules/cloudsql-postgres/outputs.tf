output "instance_name" {
  description = "Cloud SQL instance name."
  value       = google_sql_database_instance.postgres.name
}

output "instance_connection_name" {
  description = "Cloud SQL instance connection name used by Cloud SQL Auth Proxy."
  value       = google_sql_database_instance.postgres.connection_name
}

output "database_name" {
  description = "Application database name."
  value       = google_sql_database.database.name
}

output "database_user" {
  description = "Application database user name."
  value       = google_sql_user.application.name
}

output "public_ip_address" {
  description = "Public IPv4 address assigned to the Cloud SQL instance, when available."
  value       = try(google_sql_database_instance.postgres.public_ip_address, null)
}

output "connection_metadata" {
  description = "Non-secret Cloud SQL connection metadata for application configuration and Cloud SQL Auth Proxy usage."
  value = {
    instance_name            = google_sql_database_instance.postgres.name
    instance_connection_name = google_sql_database_instance.postgres.connection_name
    database_name            = google_sql_database.database.name
    database_user            = google_sql_user.application.name
    public_ip_address        = try(google_sql_database_instance.postgres.public_ip_address, null)
  }
}
