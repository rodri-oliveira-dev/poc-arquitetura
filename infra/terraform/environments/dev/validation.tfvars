# Non-sensitive values used only by local/CI static validation tools.
# database_password is an intentionally fake placeholder required by static
# Terraform validation; never replace it with a real secret in this file.
project_id                              = "estudos-gcp-498211"
region                                  = "us-central1"
database_password                       = "validation-only-fake-password"
database_disk_size                      = 10
database_disk_autoresize                = false
database_deletion_protection            = false
database_backup_enabled                 = false
database_backup_start_time              = "03:00"
database_point_in_time_recovery_enabled = false
database_transaction_log_retention_days = 7
database_backup_location                = null
