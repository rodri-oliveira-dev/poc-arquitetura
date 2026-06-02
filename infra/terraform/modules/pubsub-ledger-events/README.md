# Pub/Sub Ledger Events Terraform Module

This module provisions the Google Cloud Pub/Sub resources required for the
`LedgerService.Worker` to `BalanceService.Worker` event flow:

- main Ledger events topic;
- application DLQ topic and inspection subscription;
- technical DLQ topic and inspection subscription;
- Balance Worker pull subscription with retry and dead-letter policies;
- dedicated Ledger Worker and Balance Worker service accounts;
- resource-level IAM bindings with the minimum permissions required by the flow.

The module does not enable APIs, create secrets, configure credentials, or create
environment-specific root modules. Pub/Sub topics and subscriptions are global
resources. The `region` input is retained as deployment metadata for composition
with regional workloads.

## Prerequisites

- The Pub/Sub API (`pubsub.googleapis.com`) must already be enabled in the target
  project.
- The identity executing Terraform must be allowed to manage Pub/Sub resources,
  service accounts, and the resource-level IAM bindings declared by this module.
- The Google-managed Pub/Sub service agent must exist in the target project. Its
  address is derived from the project number as
  `service-{project_number}@gcp-sa-pubsub.iam.gserviceaccount.com`.

## Usage

```hcl
terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = ">= 7.0.0, < 8.0.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

module "pubsub_ledger_events" {
  source = "../../modules/pubsub-ledger-events"

  project_id                        = var.project_id
  region                            = var.region
  environment                       = "dev"
  app_name                          = "ledger"
  ledger_events_topic_name          = "ledger-entry-created-v1"
  ledger_events_subscription_name   = "balance-ledger-entry-created-v1"
  application_dlq_topic_name        = "ledger-entry-created-v1-dlq"
  technical_dlq_topic_name          = "ledger-entry-created-v1-technical-dlq"
  application_dlq_subscription_name = "ledger-entry-created-v1-dlq-inspection"
  technical_dlq_subscription_name   = "ledger-entry-created-v1-technical-dlq-inspection"

  ack_deadline_seconds         = 30
  message_retention_duration   = "604800s"
  retain_acked_messages        = false
  enable_message_ordering      = true
  enable_exactly_once_delivery = false
  min_retry_backoff            = "10s"
  max_retry_backoff            = "600s"
  max_delivery_attempts        = 5

  labels = {
    managed_by = "terraform"
  }
}
```

## IAM Model

| Principal | Resource | Role | Purpose |
| --- | --- | --- | --- |
| Ledger Worker service account | Main Ledger events topic | `roles/pubsub.publisher` | Publish Ledger events |
| Balance Worker service account | Balance pull subscription | `roles/pubsub.subscriber` | Consume and acknowledge Ledger events |
| Balance Worker service account | Application DLQ topic | `roles/pubsub.publisher` | Publish application-classified DLQ messages |
| Pub/Sub service agent | Technical DLQ topic | `roles/pubsub.publisher` | Forward messages to the technical DLQ |
| Pub/Sub service agent | Balance pull subscription | `roles/pubsub.subscriber` | Acknowledge messages forwarded by the native dead-letter policy |

The application DLQ is published directly by the Balance Worker. The technical
DLQ is used only by the native dead-letter policy on the main subscription.
Separate inspection subscriptions retain each flow independently for triage,
alerting, and reprocessing decisions.

The `moved` blocks preserve the former shared DLQ topic and subscription as the
application DLQ during state migration. The technical DLQ resources are new.

## Validation

Run local, non-destructive validation from this directory:

```bash
terraform fmt -check
terraform init -backend=false
terraform validate
```

Do not run `terraform apply` without an explicit deployment review.
