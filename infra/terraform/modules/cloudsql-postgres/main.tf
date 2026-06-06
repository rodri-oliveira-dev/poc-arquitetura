terraform {
  required_version = ">= 1.5.0, < 2.0.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = ">= 7.0.0, < 8.0.0"
    }
  }
}

locals {
  common_labels = merge(var.labels, {
    app         = var.app_name
    environment = var.environment
    region      = var.region
  })
}

resource "google_sql_database_instance" "postgres" {
  project          = var.project_id
  name             = var.instance_name
  region           = var.region
  database_version = var.postgres_version

  deletion_protection = var.deletion_protection

  settings {
    tier              = var.tier
    edition           = var.edition
    availability_type = var.availability_type
    disk_size         = var.disk_size
    disk_autoresize   = var.disk_autoresize
    user_labels       = local.common_labels

    backup_configuration {
      enabled                        = var.backup_configuration.enabled
      start_time                     = var.backup_configuration.start_time
      point_in_time_recovery_enabled = var.backup_configuration.point_in_time_recovery_enabled
      transaction_log_retention_days = var.backup_configuration.transaction_log_retention_days
      location                       = var.backup_configuration.location
    }

    # Public IPv4 fica habilitado intencionalmente na primeira iteracao dev
    # porque o acesso local deve passar pelo Cloud SQL Auth Proxy.
    ip_configuration {
      #trivy:ignore:GCP-0017 Public IPv4 e necessario nesta iteracao dev com Auth Proxy; authorized_networks nao e configurado.
      ipv4_enabled = true
      ssl_mode     = "ENCRYPTED_ONLY"
    }
  }
}

resource "google_sql_database" "database" {
  project  = var.project_id
  instance = google_sql_database_instance.postgres.name
  name     = var.database_name
}

resource "google_sql_user" "application" {
  project  = var.project_id
  instance = google_sql_database_instance.postgres.name
  name     = var.database_user
  password = var.database_password
}
