# Pub/Sub Ledger Events Terraform Module

This module provisions the Google Cloud Pub/Sub resources required for the
`LedgerService.Worker` to `BalanceService.Worker` event flow:

- main Ledger events topic;
- application DLQ topic and inspection subscription;
- technical DLQ topic and inspection subscription;
- Balance Worker pull subscription with retry policy and optional native
  dead-letter policy;
- dedicated Ledger Worker and Balance Worker service accounts;
- resource-level IAM bindings with the minimum permissions required by the flow.

The module does not enable APIs, create secrets, configure credentials, or create
environment-specific root modules. Pub/Sub topics and subscriptions are global
resources. The `region` input is retained as deployment metadata for composition
with regional workloads. Use `allowed_persistence_regions` to configure an
explicit `message_storage_policy` on all three topics when an environment has a
defined data residency requirement.

Review the [Pub/Sub cost and free tier
guide](../../../../docs/development/pubsub-cost-and-free-tier.md) before
provisioning a real environment. It records the standard free tier assumptions,
storage risks for backlog and DLQs, and the data required for a realistic
estimate.

## Prerequisites

- The Pub/Sub API (`pubsub.googleapis.com`) must already be enabled in the target
  project.
- The identity executing Terraform must be allowed to manage Pub/Sub resources,
  service accounts, and the resource-level IAM bindings declared by this module.
- The root module must guarantee the Google-managed Pub/Sub service agent with
  `google_project_service_identity` and pass its `member` attribute to this
  module.

## Usage

```hcl
terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = ">= 7.0.0, < 8.0.0"
    }
    google-beta = {
      source  = "hashicorp/google-beta"
      version = ">= 7.0.0, < 8.0.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

provider "google-beta" {
  project = var.project_id
  region  = var.region
}

resource "google_project_service" "pubsub" {
  project = var.project_id
  service = "pubsub.googleapis.com"

  disable_on_destroy = false
}

resource "google_project_service_identity" "pubsub" {
  provider = google-beta

  project = var.project_id
  service = google_project_service.pubsub.service
}

module "pubsub_ledger_events" {
  source = "../../modules/pubsub-ledger-events"

  project_id                        = var.project_id
  pubsub_service_agent_member       = google_project_service_identity.pubsub.member
  region                            = var.region
  allowed_persistence_regions       = ["southamerica-east1"]
  enforce_in_transit                = false
  service_account_token_creator_members = []
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
  main_subscription_expiration_ttl            = ""
  application_dlq_subscription_expiration_ttl = "2592000s"
  technical_dlq_subscription_expiration_ttl   = "2592000s"
  enable_message_ordering      = true
  enable_exactly_once_delivery = false
  enable_technical_dead_letter = true
  min_retry_backoff            = "10s"
  max_retry_backoff            = "600s"
  max_delivery_attempts        = 5

  labels = {
    managed_by = "terraform"
  }

  depends_on = [google_project_service.pubsub]
}
```

## IAM Model

| Principal | Resource | Role | Purpose |
| --- | --- | --- | --- |
| Ledger Worker service account | Main Ledger events topic | `roles/pubsub.publisher` | Publish Ledger events |
| Balance Worker service account | Balance pull subscription | `roles/pubsub.subscriber` | Consume and acknowledge Ledger events |
| Balance Worker service account | Application DLQ topic | `roles/pubsub.publisher` | Publish application-classified DLQ messages |
| Pub/Sub service agent | Technical DLQ topic | `roles/pubsub.publisher` | Forward messages to the technical DLQ when `enable_technical_dead_letter=true` |
| Pub/Sub service agent | Balance pull subscription | `roles/pubsub.subscriber` | Acknowledge messages forwarded by the native dead-letter policy when `enable_technical_dead_letter=true` |
| Configured local smoke-test members | Ledger Worker and Balance Worker service accounts | `roles/iam.serviceAccountTokenCreator` | Impersonate dedicated worker identities only when explicitly configured in a controlled environment |

The application DLQ is published directly by the Balance Worker. The technical
DLQ is used only by the native dead-letter policy on the main subscription.
Separate inspection subscriptions retain each flow independently for triage,
alerting, and reprocessing decisions.

`service_account_token_creator_members` defaults to an empty list. Use it only
for controlled local smoke tests that need ADC impersonation, and keep real
member values outside versioned files.

## Retention And Expiration

All subscriptions retain unacknowledged messages for seven days by default
(`message_retention_duration = "604800s"`) and do not retain acknowledged
messages (`retain_acked_messages = false`). Backlogs that are not processed and
DLQ messages that accumulate during that window can generate Pub/Sub storage
costs.

Each subscription declares an explicit `expiration_policy`. Set its TTL input to
an empty string (`""`) to keep the subscription regardless of inactivity. Use a
Google duration such as `"2592000s"` to remove an inactive subscription after a
finite period. Any finite expiration TTL must be greater than
`message_retention_duration`.

The reusable module defaults keep the main Balance Worker subscription
persistent and expire inactive application and technical DLQ inspection
subscriptions after 30 days. Permanent environments can override either DLQ TTL
with `""` when preserving inspection subscriptions across long inactive periods
is operationally required.

Set `enable_technical_dead_letter=false` to omit the native dead-letter policy
from the main subscription during incremental rollout or cost-conscious dev
tests. The main subscription, application DLQ, technical DLQ topic, and technical
DLQ inspection subscription remain provisioned. Only the service agent IAM
bindings used by the native technical flow are omitted.

The `moved` blocks preserve the former shared DLQ topic and subscription as the
application DLQ during state migration. The technical DLQ resources are new.

## Message Storage Policy

The module applies the same optional `message_storage_policy` to the main Ledger
events topic, the application DLQ topic, and the technical DLQ topic. The
defaults are:

```hcl
allowed_persistence_regions = []
enforce_in_transit          = false
```

An empty `allowed_persistence_regions` list omits the policy block because the
provider does not accept an explicitly configured empty policy. In that mode,
the `region` input remains a label and does not restrict where Pub/Sub stores or
processes message content.

For an environment with an approved residency decision, configure:

```hcl
allowed_persistence_regions = ["southamerica-east1"]
enforce_in_transit          = false
```

A storage policy can add inter-region data transfer costs when messages need to
leave the publisher or subscriber region. Set `enforce_in_transit=true` only
after reviewing client locations and endpoints: Pub/Sub can reject publish,
pull, and streamingPull requests received outside the allowed regions.

## Validation

Run local, non-destructive validation from this directory:

```bash
terraform fmt -check
terraform init -backend=false
terraform validate
```

Do not run `terraform apply` without an explicit deployment review.
