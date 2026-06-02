# Terraform Dev Environment

This root module composes the Pub/Sub resources for the dev deployment of
the Ledger events flow. It enables `pubsub.googleapis.com`, calls the reusable
`pubsub-ledger-events` module, and exposes primitive outputs that feed
appsettings or environment variables used by the Pub/Sub adapters.

The module provisions separate application and technical DLQ topics with
dedicated inspection subscriptions. Configure the Balance Worker
`PubSub:Consumer:DeadLetterTopicId` only with the `application_dlq_topic_name`
output. The technical DLQ is reserved for the native Pub/Sub dead-letter policy.
The existing shared DLQ topic and inspection subscription are retained as the
application DLQ during state migration.

The tracked `terraform.tfvars.example` explicitly enables the native technical
policy with `enable_technical_dead_letter=true`. Set it to `false` in the local
`terraform.tfvars` for incremental rollout or dev tests that do not need native
forwarding. The technical DLQ topic and inspection subscription remain created,
but the native policy and its Pub/Sub service agent IAM bindings are omitted.
The Balance Worker subscription and application DLQ remain available.

The tracked example also declares the subscription expiration policies:

| Subscription | Dev expiration TTL | Rationale |
| --- | --- | --- |
| Balance Worker main subscription | `""` (never expire) | Preserve the production-like consumer backlog independently of inactivity. |
| Application DLQ inspection | `"2592000s"` (30 days) | Remove an inactive inspection subscription in disposable dev environments. |
| Technical DLQ inspection | `"2592000s"` (30 days) | Remove an inactive inspection subscription in disposable dev environments. |

All subscriptions keep the module default of seven days for unacknowledged
message retention and use `retain_acked_messages=false`. Finite expiration TTLs
must remain greater than message retention. Backlogs and accumulated DLQ
messages can generate Pub/Sub storage costs. For permanent environments,
override either DLQ expiration TTL with `""` when the inspection subscription
must survive long inactivity.

## Message Residency

Dev does not restrict Pub/Sub message residency by default:

```hcl
allowed_persistence_regions = []
enforce_in_transit          = false
```

With an empty list, the reusable module omits `message_storage_policy` from the
main topic, the application DLQ topic, and the technical DLQ topic. The `region`
input remains deployment metadata and a label; it does not constrain where
Pub/Sub stores or processes message content.

When a real environment has an approved residency requirement, configure:

```hcl
allowed_persistence_regions = ["southamerica-east1"]
enforce_in_transit          = false
```

Review transfer costs and workload locations before enabling the policy.
Use `enforce_in_transit=true` with care because Pub/Sub can reject publish,
pull, and streamingPull requests received outside the allowed regions.

No remote backend is configured at this stage. Terraform uses local state by
default. Do not commit `terraform.tfvars`, state files, plans, or credentials.

## Prerequisites

- Terraform CLI installed.
- Google Cloud Application Default Credentials or impersonation configured
  outside the repository.
- Permission to enable services and manage the Pub/Sub, service account, and
  resource-level IAM resources declared by the module.

## Configure

From this directory, create a local variables file from the tracked example and
replace the placeholder project ID:

```powershell
Copy-Item terraform.tfvars.example terraform.tfvars
```

## Validate

Run local validation without configuring a backend:

```powershell
terraform fmt -check
terraform init -backend=false
terraform validate
```

## Plan And Apply Manually

Review the plan before making any remote change:

```powershell
terraform init
terraform plan -out=tfplan
terraform apply tfplan
```

`terraform apply` is intentionally manual. It enables the Pub/Sub API in the
configured project and provisions real Google Cloud resources.

Inspect the values available to runtime configuration with:

```powershell
terraform output
terraform output -json
```

Use the output-to-appsettings mapping and the preflight checklist in
[`docs/development/pubsub-infra-app-contract.md`](../../../docs/development/pubsub-infra-app-contract.md).
