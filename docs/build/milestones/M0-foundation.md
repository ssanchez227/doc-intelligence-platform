# M0 Foundation

**Goal:** an empty but deployable "hello world" for web, API and worker, reaching Azure through CI/CD, with sign-in working and the project board ready. No scenarios; this enables all the others.

## Scope

- GitHub repo, branch rules, and a GitHub Projects board. Create one issue per scenario (UC-01 to UC-46, not UC-18), with its Given/When/Then as acceptance criteria and a label for Must, Should or Could. One GitHub milestone per chat in the index.
- Terraform (azurerm): resource group, Container Apps environment and three apps (web, API, worker), Azure SQL, Storage (Blob and the queue), Key Vault, Container Registry, Application Insights, budget alert. dev and demo environments from the same code.
- GitHub Actions deploying with OIDC, no stored secrets.
- Entra: app registrations for the web app and API, and a few test groups that stand in for real ones. Sign-in with OIDC and PKCE.
- OpenTelemetry wired to Application Insights from the first request.
- A short CLAUDE.md: build and test commands, folder layout, the rules from the decision log, and "plan before building".

## Progress

- Done: repo created, starter files merged (PR #1: CLAUDE.md, .gitignore, folder layout, docs/README.md).
- Done: step 4, local tools installed and recorded in CLAUDE.md; Azure subscription exists and az is signed in; GitHub CLI installed. Docker skipped (images build in CI).
- Done: open decisions settled and logged with a cost estimate (Service Bus Basic, public repo with fresh history, no Functions; AI Search vector test moved to M3).
- Done: acceptance criteria for all 45 scenarios in `docs/acceptance-criteria.md`; labels (UC IDs, Must, Should, Could), milestones M0 to M8 and 45 issues created by `scripts/github/create-issues.ps1`; branch protection on main (PRs only, linear history, no force-push); public Projects board "Document Intelligence Platform" (https://github.com/users/ssanchez227/projects/2) linked to the repo, holding all 45 issues.
- Done: Terraform written and validated, not yet applied: `infra/bootstrap` (state storage), `infra/modules/platform` (all M0 resources including the budget alert) and `infra/envs/dev`, `infra/envs/demo`. Run order in `infra/README.md`.
- Done: bootstrap and dev applied in Central US (SQL is barred in East US 2 for this subscription); web and API placeholders answer; a fresh plan shows no changes.
- In progress: budget alert test. On 2026-11-24 the dev budget was applied at $1 (`terraform apply -var budget_amount=1`) with $0.22 spent, so the 50% alert fires when spend passes $0.50 (about two to three days at the current rate). Azure's minimum budget is $1. When the email arrives, run a plain `terraform apply` in `infra/envs/dev` to restore $10 from `terraform.tfvars`.
- Done: app skeletons (step 5). .NET 10 API with anonymous `/health` and Entra-protected `/api/me`; .NET 10 worker reading the `ingestion` queue with its managed identity (bad messages retry, then dead-letter); Vite React TypeScript app showing the API status through TanStack Query; OpenTelemetry to Application Insights in the API and worker; Dockerfiles for all three. 8 .NET tests and 2 Vitest tests pass.
- Done: Entra (step 6), applied in `infra/identity`: "DocIntel API" (scope `access_as_user`, v2 tokens with group IDs) and "DocIntel Web" (SPA, PKCE, pre-authorized), six test groups and five test users. The web app signs in with MSAL and calls `/api/me` with the access token; the API allows the web origin through CORS. Tests: 5 API, 5 worker, 5 web. Local sign-in needs `scripts/dev/write-local-auth-config.ps1` and a person to sign in as a test user. Checked locally on 2026-11-24: an interactive sign-in (PKCE) reached `/api/me` with 200 and the user's groups, after a CORS preflight; without a token the API answers 401. The deployed check is part of step 7.
- Needed in Terraform before images deploy (step 7): container `target_port` 80 to 8080; a Service Bus scale rule for the worker (it has no ingress and scales to zero, so it never starts otherwise); env vars `ServiceBus__FullyQualifiedNamespace` for the worker and `Auth__TenantId`, `Auth__Audience`, `Cors__AllowedOrigins__0` for the API (values from `infra/identity` outputs and the web URL); web image build args `VITE_API_BASE_URL`, `VITE_ENTRA_TENANT_ID`, `VITE_ENTRA_CLIENT_ID`, `VITE_API_SCOPE`.
- Next: CI/CD with OIDC (step 7).

## Decisions needed first

Settled on 2026-11-17; see "M0 decisions" in the decision log.

## Design references

Architecture diagram and the tools and free-tier tables in the plan doc.

## Done checklist

- [ ] A push to main deploys web, API and worker to dev through CI/CD
- [ ] A signed-in test user reaches a protected API endpoint; an anonymous one is refused
- [ ] A budget alert is set and tested
- [ ] Every scenario has an issue on the board with acceptance criteria and a label
- [ ] CLAUDE.md is committed and the decision log is current
- [ ] The free-tier questions in the decision log are answered or logged with a cost estimate
