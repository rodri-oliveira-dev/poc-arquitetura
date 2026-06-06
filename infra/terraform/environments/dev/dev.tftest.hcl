mock_provider "google" {}

mock_provider "google-beta" {}

variables {
  project_id                              = "terraform-test-project"
  region                                  = "us-central1"
  database_instance_name                  = "poc-ledger-dev-postgres"
  database_name                           = "ledger_dev"
  database_user                           = "ledger_app"
  database_password                       = "terraform-test-fake-password"
  database_version                        = "POSTGRES_16"
  database_tier                           = "db-f1-micro"
  database_availability_type              = "ZONAL"
  database_disk_size                      = 10
  database_disk_autoresize                = false
  database_deletion_protection            = false
  database_backup_enabled                 = false
  database_backup_start_time              = "03:00"
  database_point_in_time_recovery_enabled = false
  database_transaction_log_retention_days = 7
  database_backup_location                = null
}

run "dev_composes_cloudsql_postgres" {
  command = plan

  assert {
    condition     = google_project_service.sqladmin.service == "sqladmin.googleapis.com"
    error_message = "O root module dev deve habilitar a Cloud SQL Admin API."
  }

  assert {
    condition     = module.cloudsql_postgres.database_name == var.database_name
    error_message = "O root module dev deve passar o nome de database configurado para o modulo Cloud SQL."
  }

  assert {
    condition     = module.cloudsql_postgres.database_user == var.database_user
    error_message = "O root module dev deve passar o usuario de database configurado para o modulo Cloud SQL."
  }

  assert {
    condition     = output.database_name == var.database_name
    error_message = "O root module dev deve expor o nome nao secreto do database Cloud SQL."
  }

  assert {
    condition     = output.database_user == var.database_user
    error_message = "O root module dev deve expor o usuario nao secreto do database Cloud SQL."
  }
}
