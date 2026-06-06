# Valores nao sensiveis usados apenas por ferramentas de validacao estatica local/CI.
# database_password e um placeholder falso intencional exigido pela validacao
# estatica do Terraform; nunca substitua por um segredo real neste arquivo.
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
