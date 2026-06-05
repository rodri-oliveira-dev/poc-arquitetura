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
  database_deletion_protection            = true
  database_backup_enabled                 = true
  database_backup_start_time              = "03:00"
  database_point_in_time_recovery_enabled = true
  database_transaction_log_retention_days = 7
  database_backup_location                = null
}

run "dev_composes_cloudsql_postgres" {
  command = plan

  assert {
    condition     = google_project_service.sqladmin.service == "sqladmin.googleapis.com"
    error_message = "The dev root module must enable the Cloud SQL Admin API."
  }

  assert {
    condition     = module.cloudsql_postgres.database_name == var.database_name
    error_message = "The dev root module must pass the configured database name to the Cloud SQL module."
  }

  assert {
    condition     = module.cloudsql_postgres.database_user == var.database_user
    error_message = "The dev root module must pass the configured database user to the Cloud SQL module."
  }

  assert {
    condition     = output.database_name == var.database_name
    error_message = "The dev root module must expose the non-secret Cloud SQL database name."
  }

  assert {
    condition     = output.database_user == var.database_user
    error_message = "The dev root module must expose the non-secret Cloud SQL database user."
  }
}
