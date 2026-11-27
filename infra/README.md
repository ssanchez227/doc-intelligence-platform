# Infrastructure

Terraform (azurerm 5, azapi 2) for the platform. dev and demo are built from the same module.

## Layout

- `bootstrap/`: one-time setup of the storage account that holds Terraform state. Its own state stays local and gitignored.
- `modules/platform/`: everything an environment needs: resource group, Log Analytics and Application Insights, one managed identity per app, Container Registry, Storage, Service Bus (ingestion queue), Key Vault, Azure SQL (free offer), the Container Apps environment with web, API and worker, and a budget alert.
- `envs/dev`, `envs/demo`: thin roots that call the module. They differ only in the state key and the environment name.
- `identity/`: Entra objects for the tenant (azuread provider), shared by all environments: the API and web app registrations, test groups and test users. Same state storage, key `identity.tfstate`; copy `backend.hcl` and `terraform.tfvars` from their `.example` files, then `terraform init -backend-config=backend.hcl` and `terraform apply`. Needs a directory role that can create apps, groups and users. Add each new environment's web URL to `web_redirect_uris`. Test user passwords: `terraform output -json test_user_passwords`.

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

Set `ARM_SUBSCRIPTION_ID` first (`export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)`); azurerm 5 needs it.

## Troubleshooting

- `MissingSubscriptionRegistration` for a namespace (seen for `microsoft.insights` on the first apply): register it with `az provider register -n Microsoft.Insights --wait` and re-apply.
- `ProvisioningDisabled` for SQL: the subscription is barred from that region. List open regions with the `Microsoft.Sql/locations/<region>/capabilities` API (`status` is `Available`).
- 404 or "Root object was present, but now absent" right after creating something, especially in a resource group deleted and recreated minutes earlier: Azure reads lag behind writes. The resource usually exists; import it (`terraform import`) and plan again. If a plan says a resource "has been deleted", check with `az` before applying, and re-plan.
- In Git Bash, set `MSYS_NO_PATHCONV=1` before `terraform import`, or the `/subscriptions/...` ID is rewritten as a Windows path.

## Notes

- No shared keys anywhere: storage and Service Bus use Entra auth only; SQL uses Entra-only authentication. Apps reach services through their user-assigned identities (`AZURE_CLIENT_ID` is set in each container).
- The apps start on a placeholder image. CI deploys the real images, and Terraform ignores image changes.
- Each environment has a deploy identity (`id-dip-<env>-deploy`) that GitHub Actions signs in as through OIDC, trusted only for the GitHub environment of the same name. The credential's subject uses GitHub's immutable form with the owner and repository IDs (`github_repository`, default in each environment root); read it with `gh api repos/<owner>/<repo>/actions/oidc/customization/sub`. After applying a new environment, create that GitHub environment (main only) and set its variables from the `deploy` and `app_urls` outputs here and the `infra/identity` outputs; `ci-cd.yml` lists the names. The environment roots also need `entra_tenant_id` and `api_audience` in tfvars, from `infra/identity`.
- Terraform is applied by hand, not by CI.
- The SQL database is created with azapi because the free-offer settings (`useFreeLimit`, `freeLimitExhaustionBehavior`) are not in the azurerm resource. It pauses when the monthly free amount runs out instead of billing.
- SQL allows Azure services through its firewall; private networking is an M8 hardening item.
- Budget alert emails come from `budget_contact_emails` in the gitignored tfvars, so no address is committed.
- Check: `terraform fmt -recursive -check` from `infra/`, and `terraform validate` in each root (after `terraform init -backend=false`).
