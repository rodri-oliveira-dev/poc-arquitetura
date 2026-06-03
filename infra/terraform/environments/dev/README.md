# Terraform Dev Environment

This root module composes the Pub/Sub resources for the dev deployment of
the Ledger events flow. It enables `pubsub.googleapis.com`, guarantees the
Google-managed Pub/Sub service identity with `google_project_service_identity`,
calls the reusable `pubsub-ledger-events` module, and exposes primitive outputs
that feed appsettings or environment variables used by the Pub/Sub adapters.

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

## Terraform State

This root module configures a partial remote backend in Google Cloud Storage.
The state object is separated by environment with the prefix:

```text
poc-arquitetura/pubsub/dev
```

The bucket name is intentionally not hardcoded in `backend.tf`; provide it
during `terraform init` with
`-backend-config="bucket=rodri-terraform-state-bucket"`. The current dev bucket
is `rodri-terraform-state-bucket`; it was created outside this root module and
must not be recreated by the same state that it stores. See
[`docs/adrs/0080-backend-remoto-gcs-terraform-dev.md`](../../../../docs/adrs/0080-backend-remoto-gcs-terraform-dev.md).

Grant bucket access only to authorized Terraform operators, bootstrap/audit
administrators, and a CI identity only if a future workflow runs real
`terraform plan`. Application workload service accounts must not access the
Terraform state bucket.

Do not commit `terraform.tfvars`, state files, plans, or credentials.

## Prerequisites

- Terraform CLI installed.
- Google Cloud Application Default Credentials or impersonation configured
  outside the repository.
- Permission to enable services and manage the Pub/Sub, service account, and
  resource-level IAM resources declared by the module.
- Permission to generate the Pub/Sub service identity through the Service Usage
  API.

## Configure

From this directory, create a local variables file from the tracked example and
replace the placeholder project ID:

```powershell
Copy-Item terraform.tfvars.example terraform.tfvars
```

For a local smoke test against real GCP resources with ADC impersonation, set
`service_account_token_creator_members` only in the ignored local
`terraform.tfvars`. The module grants `roles/iam.serviceAccountTokenCreator`
directly on the two dedicated worker service accounts, never at project level.
After the smoke test, clear the list and reapply Terraform manually to remove
the temporary permission.

For GitHub Actions, the same input can be passed without committing a human
email:

```yaml
env:
  TF_VAR_service_account_token_creator_members: '["user:${{ vars.GCP_IMPERSONATION_USER_EMAIL }}"]'
```

or, when the value is stored as a secret:

```yaml
env:
  TF_VAR_service_account_token_creator_members: '["user:${{ secrets.GCP_IMPERSONATION_USER_EMAIL }}"]'
```

For a mature CI/CD setup, prefer Workload Identity Federation with OIDC instead
of a human email.

## Validate

Run syntax-only local validation without configuring the remote backend:

```powershell
terraform fmt -check
terraform init -backend=false
terraform validate
```

This validation mode is useful for hooks and CI because it does not require GCP
credentials or bucket access. It does not exercise remote state locking and must
not be followed by `terraform plan`, `terraform apply`, or `terraform destroy`.

For validation with the configured backend, initialize with the existing state
bucket first:

```powershell
terraform init -backend-config="bucket=rodri-terraform-state-bucket"
terraform validate
```

## Migrate Local State Manually

If a local `terraform.tfstate` already exists, migrate it only after the bucket
has been created, versioning has been enabled, IAM has been reviewed, and the
operator has confirmed the target GCP project and bucket.

Create a local backup before migration:

```powershell
Copy-Item terraform.tfstate terraform.tfstate.pre-gcs-migration.backup
terraform init -migrate-state -backend-config="bucket=rodri-terraform-state-bucket"
terraform state list
terraform plan -var-file="terraform.tfvars"
```

Review the plan carefully. Do not use `-lock=false`; the GCS backend locking
must protect concurrent Terraform operations. Do not commit the backup, state,
`terraform.tfvars`, binary plans, or credentials.

## Plan And Apply Manually

Review the plan before making any remote change:

```powershell
terraform init -backend-config="bucket=rodri-terraform-state-bucket"
terraform plan -out=tfplan
terraform apply tfplan
```

`terraform apply` is intentionally manual. It enables the Pub/Sub API in the
configured project, guarantees the Google-managed Pub/Sub service identity, and
provisions real Google Cloud resources. In a newly created project, review that
the first plan includes `google_project_service_identity.pubsub`.

Do not use `-lock=false` with the remote backend. Terraform should use the GCS
backend locking behavior for `plan` and `apply`.

Inspect the values available to runtime configuration with:

```powershell
terraform output
terraform output -json
```

Use the output-to-appsettings mapping and the preflight checklist in
[`docs/development/pubsub-infra-app-contract.md`](../../../../docs/development/pubsub-infra-app-contract.md).
