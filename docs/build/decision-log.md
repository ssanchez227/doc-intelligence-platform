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
- The federated credential matches GitHub's immutable subject claim, `repo:<owner>@<owner id>/<repo>@<repo id>:environment:<env>` (superseding the `repo:<owner>/<repo>` form above, which the first deploy was refused with). This repository uses immutable subjects (`actions/oidc/customization/sub`), and the IDs mean a renamed or re-created repository with the same name cannot inherit the trust.

**M1 decisions (2026-11-27)**
- Checkouts use optimistic concurrency, not an exclusive lock (closes the open question). Checking out records who, when and the starting version, and is shown to others; nobody is blocked. A check-in whose starting version is no longer the latest is refused (409) with who checked in which version and when, and no version is created; the checkout stays open. A unique index on (document, version number) and a row version on the document decide simultaneous check-ins. Chosen over the lock to match the plan doc's versioning lean. UC-02, UC-03 and UC-04 criteria rewritten; UC-29 becomes "clear a stale checkout".
- Publishing creates a new version row with the next major number that points at the approved draft's file; the draft keeps its number, and the previously published version's status becomes Archived. Content of a version never changes; only its lifecycle status does.
- Each check-in takes the next minor number after the highest existing version (1.0 then 1.1, 1.2); a restore is a check-in of the old file.
- Until M5, publishing is a service method covered by tests plus an Administrators-only endpoint enabled only by configuration in dev, so the screens can show published state. M5 calls the same method and the endpoint is removed.
- M1 permissions are minimal and M2 extends them: a document copies its category's default groups at check-in; reading needs one of those groups or Administrators; changes need Contributors; retiring needs Administrators. Role group object IDs come from configuration. Categories are seeded by migration until UC-30 (M6).
- The ordered reviewer list is saved at check-in; versions stay Draft until M5 wires the review.
- Retiring (UC-08) is a reversible flag on the document; the library hides retired documents by default and M3 search must honour it.
- Azure SQL with EF Core migrations in Azure; SQLite for tests and local runs (no LocalDB or Docker on the workstation). A CI job runs the same tests against a SQL Server service container to catch provider differences.
- The API applies migrations at startup when a configuration flag is on; its identity gets a database user from a one-time script run by the Entra admin. Revisit before M8.
- Files go to Blob Storage through the API's managed identity in Azure, and to a local folder for local runs. Accepted formats: pdf, txt, md, csv, html, xml, json, odt, ods, odp, odg, up to 25 MB.
- UC-07 PDF conversion: plain text and Markdown are rendered with PDFsharp/MigraDoc (MIT), a PDF is linked as-is, and ODF records a "not supported yet" failure against the version. It runs in a background service in the API after publish, and on startup picks up published versions still missing a PDF (the API scales to zero). It moves to the worker once the worker can reach the database (M3). ODF through LibreOffice is left over.
- Every action writes an audit event in the same transaction as the change, including refused check-ins and opening or downloading a version.
- Permissions in the database use group keys (readers, contributors, reviewers, administrators, finance, hr), not Entra object IDs; configuration `Groups:<key>` maps each key to its group ID. The IDs are tenant-specific and stay out of the repo and the seed data.
- Without `Database:Provider` configured the API still starts (health answers, data endpoints fail), so dev keeps deploying until Azure SQL is wired.
- The owner of a document can always read it, even in a category whose groups they are not in (a contributor checking in to Finance would otherwise lose sight of their own document).
- Drafts are visible to contributors and administrators only; readers see published and archived versions. A document with no published version does not appear in a reader's library. This is the "version they have no access to" in UC-05 until M2 adds finer permissions.
- A document or version the caller may not read answers 404, the same as one that does not exist, so nothing is revealed. M2 decides the denied-access experience (UC-16).
- Checking out again while holding an out-of-date checkout moves it to the latest version; that is how a user starts again after a refused check-in.
- A restore needs no checkout. It creates the next minor draft referencing the old version's file (same immutable blob, no copy), so other open checkouts become out of date.
- Retiring is for administrators. A retired document leaves the library (shown with `?retired=true`), keeps its versions, PDF and audit trail readable, and refuses checkouts, check-ins, restores and publishing until it is reversed.
- Files are always served as downloads (`Content-Disposition: attachment`, `nosniff`) so an uploaded HTML file can never run on the API's origin; the web app must not render HTML, XML or JSON files inline either.
- UC-07 uses a small built-in PDF writer with the standard PDF fonts (Courier, Helvetica-Bold) instead of PDFsharp/MigraDoc (supersedes that part of the UC-07 line above). PDFsharp 6 on Linux needs font files supplied to it, and the container has none; the standard fonts need no embedding. Limits: Latin-1 only (other characters print as "?"), simple Markdown (headings, lists, code blocks, paragraphs). Each rendition gets a unique blob name, so a retried conversion never collides with a partial one.

## Open

| Question | Needed by | Notes |
|---|---|---|
| Does the AI Search free tier hold the vector index we need | M3 | Moved from M0 on 2026-11-17; decides whether M3 stays free |
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
- 2026-11-27: OIDC subject switched to GitHub's immutable form after the first deploy's sign-in was refused.
- 2026-11-28: M0 closed; reviewed for completeness against the M0 session log. No new decisions.
- 2026-11-27: M1 decisions logged; the edit lock question closed in favour of optimistic concurrency, and UC-02, UC-03, UC-04 and UC-29 rewritten to match.
- 2026-11-28: M1 decisions made while building steps 1 to 5 (group keys, owner and draft visibility, 404 for no access, restore, retire, downloads only, built-in PDF writer).
