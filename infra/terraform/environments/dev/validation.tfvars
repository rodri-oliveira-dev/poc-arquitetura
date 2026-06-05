# Non-sensitive values used only by local/CI static validation tools.
project_id                   = "estudos-gcp-498211"
region                       = "us-central1"
cloudsql_database_password   = "validation-only-value"
cloudsql_deletion_protection = true
cloudsql_backup_configuration = {
  enabled                        = true
  start_time                     = "03:00"
  point_in_time_recovery_enabled = true
  transaction_log_retention_days = 7
  location                       = null
}
