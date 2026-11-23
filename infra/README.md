# Infrastructure

Terraform (azurerm 5, azapi 2) for the platform. dev and demo are built from the same module.

## Layout

- `bootstrap/`: one-time setup of the storage account that holds Terraform state. Its own state stays local and gitignored.
- `modules/platform/`: everything an environment needs: resource group, Log Analytics and Application Insights, one managed identity per app, Container Registry, Storage, Service Bus (ingestion queue), Key Vault, Azure SQL (free offer), the Container Apps environment with web, API and worker, and a budget alert.
- `envs/dev`, `envs/demo`: thin roots that call the module. They differ only in the state key and the environment name.

## First run

Sign in first with `az login`. The bootstrap creates the state storage and gives you Storage Blob Data Contributor on it:

```bash
cd infra/bootstrap
terraform init
terraform apply
terraform output backend_config
```

Then, for each environment (dev shown):

1. Copy `backend.hcl.example` to `backend.hcl` and fill in the storage account name from `backend_config`.
2. Copy `terraform.tfvars.example` to `terraform.tfvars` and fill it in. Both files are gitignored.
3. Run:

```bash
cd infra/envs/dev
terraform init -backend-config=backend.hcl
terraform plan -out=dev.tfplan
terraform apply dev.tfplan
```

The role assignment on the state account can take a minute or two to take effect, so the first `init` may fail with 403. Wait and retry.

## Notes

- No shared keys anywhere: storage and Service Bus use Entra auth only; SQL uses Entra-only authentication. Apps reach services through their user-assigned identities (`AZURE_CLIENT_ID` is set in each container).
- The apps start on a placeholder image. CI deploys the real images, and Terraform ignores image changes.
- The SQL database is created with azapi because the free-offer settings (`useFreeLimit`, `freeLimitExhaustionBehavior`) are not in the azurerm resource. It pauses when the monthly free amount runs out instead of billing.
- SQL allows Azure services through its firewall; private networking is an M8 hardening item.
- Budget alert emails come from `budget_contact_emails` in the gitignored tfvars, so no address is committed.
- Check: `terraform fmt -recursive -check` from `infra/`, and `terraform validate` in each root (after `terraform init -backend=false`).
