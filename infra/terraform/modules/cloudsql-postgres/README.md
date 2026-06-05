# Cloud SQL PostgreSQL Terraform Module

This module provisions a Google Cloud SQL for PostgreSQL database for a single
application environment:

- one Cloud SQL PostgreSQL instance;
- one application database;
- one application database user;
- standardized labels merged with environment metadata;
- public IPv4 enabled for Cloud SQL Auth Proxy access in this first dev
  iteration.

The module does not enable APIs, create Secret Manager entries, configure VPC
private connectivity, create IAM bindings, or manage application connection
strings. Keep passwords and connection strings outside versioned files.

## Connectivity

The expected local access path for dev is Cloud SQL Auth Proxy using the
`instance_connection_name` output. Public IPv4 is enabled, but the module does
not configure `authorized_networks` and must not be used to allow
`0.0.0.0/0`.

Example local proxy command after a reviewed manual apply from the dev root
module:

```powershell
cloud-sql-proxy "$(terraform output -raw database_instance_connection_name)" --port 5432
```

Configure the application locally with host `127.0.0.1`, port `5432`, the
database name, the database user, and the password from the ignored local secret
source used to run Terraform. The password is never exposed as a module output.

## Prerequisites

- The Cloud SQL Admin API (`sqladmin.googleapis.com`) must already be enabled in
  the target project.
- The identity executing Terraform must be allowed to manage Cloud SQL
  instances, databases, and users.
- The caller must provide `database_password` through an ignored
  `terraform.tfvars` file or a secure environment variable such as
  `TF_VAR_database_password`.

## Usage

```hcl
module "cloudsql_postgres" {
  source = "../../modules/cloudsql-postgres"

  project_id        = var.project_id
  region            = var.region
  environment       = "dev"
  app_name          = "poc-ledger"
  instance_name     = "poc-ledger-dev-postgres"
  postgres_version  = "POSTGRES_16"
  tier              = "db-f1-micro"
  database_name     = "ledger_dev"
  database_user     = "ledger_app"
  database_password = var.database_password

  deletion_protection = true

  backup_configuration = {
    enabled                        = true
    start_time                     = "03:00"
    point_in_time_recovery_enabled = true
    transaction_log_retention_days = 7
    location                       = null
  }

  labels = {
    managed_by = "terraform"
  }
}
```

## Backup Defaults

The default backup configuration keeps backups and point-in-time recovery
enabled in dev. This is safer for migration testing and accidental data loss,
but it can generate cost. Disposable experiments can override the backup object
only after an explicit review of the risk.

## Outputs

The module exposes the instance name, instance connection name, database name,
database user, public IP address when assigned, and a non-secret metadata object.
It does not output the database password.

## Validation

Run local, non-destructive validation from this directory:

```bash
terraform fmt -check
terraform init -backend=false
terraform validate
```

Do not run `terraform apply` without an explicit deployment review.
