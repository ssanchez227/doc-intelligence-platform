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
- Next: apply the bootstrap and dev, test the budget alert, then CI/CD with OIDC, Entra, OpenTelemetry.

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
