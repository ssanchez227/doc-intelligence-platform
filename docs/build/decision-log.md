# Decision log

One line per decision, with its reason. Add new decisions at the bottom of the right section and never edit history; supersede with a new line.

## Settled (as of 2026-11-11)

**Stack**
- Front end: React with TypeScript and TanStack Query. Chosen for market size.
- Back end: .NET (current LTS), C# minimal APIs, EF Core. Azure SQL, Blob Storage, Azure AI Search, Azure OpenAI, Entra ID, Container Apps, Terraform, GitHub Actions with OIDC, OpenTelemetry with Application Insights, Key Vault.

**Architecture**
- Modular monolith API plus a separate ingestion worker.
- Azure SQL is the source of truth for permissions.
- Permissions are copied into the search index as allowed groups on every chunk. Search filters on them, then SQL re-verifies before any text reaches the model.
- Versions are immutable, with a pointer to the current version.
- Managed identity for service-to-service access.
- Two environments, dev and demo, from the same Terraform.

**Behaviour (from the scenario questions)**
- Check-in starts the review automatically. There is no separate submit step.
- The author assigns the reviewers, who approve in the order assigned. Parallel approval is out of scope; predefined workflows are a future improvement.
- A reviewer can send the document back to the author or let it advance to the next reviewer with feedback attached. After a send-back, the workflow continues at that reviewer; it does not restart.
- Draft versions take minor numbers (0.1, 0.2). Publishing assigns the next major number (1.0).
- File formats: any non-proprietary format for now (for example ODF, PDF, Markdown, plain text). Proprietary formats such as Word are out of the first version.
- No permanent deletion, only archiving.
- Reading checkout has no expiry and does not block other readers. UC-18 is dropped.
- Scope stays whole. If time runs long, the schedule stretches.

**M0 decisions (2026-11-17)**
- Ingestion queue: Service Bus, Basic tier. $0.05 per million operations against $0.40 per million for Storage Queue (Azure Retail Prices API, East US), and it has max-delivery retries and a dead-letter queue built in, where Storage Queue would need hand-written poison-queue handling.
- Azure Functions are not used; the ingestion worker runs in Container Apps.
- The GitHub repo is public: free branch protection and unlimited Actions minutes. Before going public, personal notes were removed from the docs and the git history was replaced with one clean commit. The repo holds engineering material only.
- Local Docker is skipped; container images build in CI.
- The AI Search free-tier vector test moves to M3, the first milestone that indexes anything. Until it passes, M3 assumes the Basic tier may be needed (about $74 a month if left running, so create it only for test and demo sessions).

**Cost estimate, dev environment, per month (2026-11-17)**

| Service | Expected cost | Basis |
|---|---|---|
| Azure SQL (free offer), Container Apps (scale to zero) | $0 | Within the monthly free grants in the plan doc |
| Container Registry, Basic | $0 for 12 months, then about $5 | 12-month offer applies (Free Trial account); $0.1666 a day afterwards |
| Service Bus Basic, Blob Storage, Key Vault | Under $1 | Per-operation and per-GB rates above and in the plan doc |
| Application Insights and Log Analytics | $0 with a daily cap | First 5 GB a month free, then $2.30 per GB |
| Entra ID Free | $0 | Free tier covers app registrations and test groups |
| AI Search | $0 on Free; about $0.10 an hour on Basic | Basic only while testing or demoing, then destroyed |
| Azure OpenAI | Usage only | Embeddings about $0.02 per million tokens; answers are the main variable cost |

The subscription is a Free Trial with a spending limit: $200 of credit for 30 days. To keep the free monthly amounts after that, upgrade to pay-as-you-go, with the budget alert set first.

**Terraform layout (2026-11-22)**
- State lives in its own storage account, created once by `infra/bootstrap` (local state, gitignored). Entra auth only, no shared keys, blob versioning and 30-day soft delete.
- One module, `infra/modules/platform`; `envs/dev` and `envs/demo` differ only in state key and environment name.
- Region Central US for every environment (changed from East US 2 on 2026-11-22): this subscription may not create Azure SQL servers in East US 2, East US, West US 2 or North Central US; Central US, West US 3 and Canada Central are open. Everything moved, not SQL alone, to keep one region. The state storage from the bootstrap stays in East US 2. Check Azure OpenAI model availability in Central US before M4.
- One user-assigned identity per app (web, API, worker), each with only its own roles: AcrPull for all; Blob and Key Vault access for API and worker; Service Bus send for API, receive for worker.
- Azure SQL uses Entra-only authentication and the free offer, created with azapi because azurerm lacks the free-offer settings; it auto-pauses when the free amount runs out. Public access with the Azure-services firewall rule until M8 hardening.
- Log Analytics capped at 0.15 GB a day to stay under the free 5 GB a month.
- Budget per environment resource group: $10 a month, alerts at 50, 80 and 100% actual and 100% forecast. Contact emails come from gitignored tfvars.
- Container Registry is Basic, matching the cost table (about $5 a month once any free offer ends; Standard would be about $20). Chosen 2026-11-22 over Standard.
- Container Apps start on a placeholder image; CI owns the image, so Terraform ignores image changes.

**Entra sign-in (2026-11-24)**
- Entra objects are Terraform too (azuread provider), in their own root `infra/identity` with its own state key. They belong to the tenant, not an environment, so dev and demo share one pair of app registrations; each environment's web URL is added to the SPA redirect URIs.
- Two app registrations: "DocIntel API" exposes one delegated scope, `access_as_user`, at `api://<client id>`, and issues v2 tokens (the audience is the client ID); "DocIntel Web" is a single-page app using the authorization code flow with PKCE (MSAL) and is pre-authorized for that scope, so users see no consent prompt.
- Permissions use security groups, sent as group object IDs in the token (`groupMembershipClaims = SecurityGroup`). No app roles for now; group overage (users in more than about 200 groups) is handled when permissions are built in M1/M2.
- Test groups: Readers, Contributors, Reviewers, Administrators (the scenario actors) and Finance, HR (stand-ins for groups that documents are shared with). Five test users: reader, contributor, reviewer, admin, and an outsider in no group for the access-denied scenarios.
- Test user passwords are generated by Terraform and kept only in state (read with `terraform output -json test_user_passwords`). Acceptable for test accounts in a personal tenant; nothing reaches the repo.
- Tenant and client IDs are not secrets but are tenant-specific, so they stay out of the repo: locally, `scripts/dev/write-local-auth-config.ps1` writes them to the gitignored `src/web/.env.development.local` and the API's user secrets; in Azure, CI and Terraform supply them (step 7).
- The API allows the web origin through CORS, configured per environment (`Cors__AllowedOrigins__0`).
- Sign in always shows the account picker (`prompt=select_account`), so a different test user can sign in while Entra still holds another account's session.

**CI/CD (2026-11-27)**
- One workflow, `.github/workflows/ci-cd.yml`: every pull request and push to main runs .NET build and test, web lint, test and build, and Terraform format and validate. A push to main then deploys to dev.
- GitHub reaches Azure through OIDC: a user-assigned identity per environment (`id-dip-<env>-deploy`, in the platform module) with a federated credential for `repo:<owner>/<repo>:environment:<env>`. No client secret exists anywhere. Only main may deploy to the GitHub `dev` environment (branch policy), so other branches cannot get a token for it.
- The deploy identity has the least it needs: AcrPush on the registry, Container Apps Contributor on each of the three apps, and Reader on the Container Apps environment. It cannot change anything else in the resource group.
- Images are built with Docker on the GitHub runner and pushed to ACR, tagged with the commit SHA; `az containerapp update` rolls each app to its new tag. No ACR Tasks (they bill per build second).
- The GitHub environment holds only IDs and URLs (as variables, not secrets): deploy client ID, tenant, subscription, registry, resource group, app URLs, web client ID and API scope.
- After deploying, the workflow checks that each app's latest revision is ready on the new tag, `/health` answers, the web page loads, and `/api/me` refuses a call without a token (401).
- Terraform stays a manual step (`terraform apply` from a workstation) for now: giving CI rights to change infrastructure would need a much broader identity. Revisit before M8.
- App settings come from Terraform: the API gets the Entra tenant and audience, the CORS origin and the Service Bus namespace; the worker gets the namespace and queue. The worker scales on queue length (KEDA `azure-servicebus` rule using the worker's identity), since it has no ingress to wake it.

## Open

| Question | Needed by | Notes |
|---|---|---|
| Does the AI Search free tier hold the vector index we need | M3 | Moved from M0 on 2026-11-17; decides whether M3 stays free |
| Edit lock (UC-02) or optimistic concurrency | M1 | The plan doc's versioning lean says optimistic concurrency instead of locks, but UC-02 requires an exclusive edit lock |
| Free Trial end date | Before the trial ends | Check it in the portal and upgrade to pay-as-you-go before then |
| UC-46 details: who approves, does the copy expire, can the recipient be outside the system | M7 | Assumed: owner or designated approver, no stated expiry, undecided |
| Restart point of a send-back when several reviewers have already approved | M5 | Current answer: continue at the reviewer who sent it back |

## Changelog

- 2026-11-11: log created from the plan doc and the scenario decisions.
- 2026-11-17: moved into the repo under `docs/build/`; the repo is now the working copy. The Doc Mgt Project pages are a read-only mirror.
- 2026-11-17: M0 decisions logged (queue, public repo, Functions, Docker, AI Search test timing) with a cost estimate. Personal positioning notes removed from this log and the other docs before the repo went public; the queue, public repo and free-tier questions closed; two new open questions added.
- 2026-11-22: Terraform layout decisions logged; Container Registry tier added as an open question.
- 2026-11-22: Container Registry tier settled as Basic; open question closed.
- 2026-11-22: region changed to Central US after SQL provisioning was refused in East US 2; bootstrap and dev applied.
- 2026-11-24: Entra sign-in decisions logged (`infra/identity`, shared app registrations, groups in tokens, test users).
- 2026-11-27: CI/CD decisions logged (one workflow, OIDC deploy identity per environment, manual Terraform).
