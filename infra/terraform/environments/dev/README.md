# Terraform Dev Environment

This root module composes the Pub/Sub resources for the dev deployment of
the Ledger events flow. It enables `pubsub.googleapis.com`, calls the reusable
`pubsub-ledger-events` module, and exposes primitive outputs that feed
appsettings or environment variables used by the Pub/Sub adapters.

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
