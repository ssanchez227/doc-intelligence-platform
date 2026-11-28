# M0 session log: Foundation

Date range: 2026-11-17 to 2026-11-28
Branch / PR: one branch and PR per step: #46, #47, #48, #49, #50 (step 6, merged into the step 5 branch, see below), #52, #53, #54, #55, and this closing PR
Tag: `m0`, on commit 357da30 (the merge of #56)
Demo clip: none (M0 has no scenarios; the evidence is below)

## What was built

No scenarios; M0 is the enabler for all of them.

1. Repo, starter files, CLAUDE.md and the handoff docs under `docs/build/`.
2. Open decisions settled and logged with a cost estimate (Service Bus Basic, public repo, no Functions, no local Docker, AI Search test moved to M3).
3. Acceptance criteria for all 45 scenarios (`docs/acceptance-criteria.md`). `scripts/github/create-issues.ps1` creates the labels, milestones and one issue per scenario. Branch protection on main, and a public Projects board holding all 45 issues.
4. Terraform: `infra/bootstrap` (state storage), `infra/modules/platform` and `infra/envs/dev` and `demo`. dev is applied in Central US: Container Apps (web, API, worker), Azure SQL (free offer), Storage, Service Bus, Key Vault, Container Registry, Application Insights and Log Analytics, a budget alert, and one managed identity per app.
5. App skeletons: .NET 10 API (`/health` anonymous, `/api/me` protected), .NET 10 worker reading the ingestion queue, and a Vite React TypeScript web app. OpenTelemetry goes to Application Insights, and each app has a non-root Dockerfile on port 8080.
6. Entra in `infra/identity`: API and SPA app registrations (PKCE, v2 tokens with group IDs), six test groups and five test users. The web app signs in with MSAL; the API validates the tokens and allows the web origin through CORS.
7. CI/CD: `.github/workflows/ci-cd.yml` tests every PR. A push to main builds the images, pushes them to the registry, rolls the dev apps and checks the result. GitHub signs in to Azure through OIDC as a deploy identity per environment, with least-privilege roles and no stored secret.

## What changed from the brief

- **Service Bus instead of a Storage queue** for ingestion: cheaper per operation, with retries and a dead-letter queue built in (decision log, M0 decisions).
- **Region moved from East US 2 to Central US**: this subscription may not create Azure SQL servers in East US 2.
- **Terraform is applied by hand, not by CI**: CI rights over the infrastructure would need a much broader identity. Revisit before M8.
- **Entra objects are Terraform too**, in their own root shared by dev and demo.
- **The demo environment is written and validated but not applied**; it costs nothing until it's needed for a demo.

## Decisions made

All in `docs/build/decision-log.md` under "M0 decisions", "Cost estimate", "Terraform layout", "Entra sign-in" and "CI/CD".

## Tests and evidence

- **Automated:** 5 API tests (health, 401 without a token, 200 with a test token, CORS allowed and refused), 5 worker test cases (reading a valid job; rejecting bad messages so they are retried, then dead-lettered) and 5 web tests (API status, API unreachable, sign-in offered, signed-in user shown via the protected endpoint, API refusal shown). CI runs them on every PR, together with `terraform fmt` and `validate` for every root.
- **Push to main deploys:** run 46085335780 (#54) built and pushed the three images and rolled each app. It then checked that every new revision was ready on the commit's tag, `/health` answered 200, the web page loaded, the bundle contained the Entra client ID, and `/api/me` refused a call without a token (401).
- **Signed-in user reaches a protected endpoint:** on 2026-11-28, `docintel.reader` signed in on the dev web URL. The page showed the user and 2 groups, and Application Insights logged a CORS preflight (204) and `GET /api/me` 200. Without a token, the API answers 401. Locally, the same check passed on 2026-11-24 and 2026-11-27.
- **Telemetry:** API requests arrive in Application Insights within about 2 minutes.
- **Board:** 45 issues, each with a UC label, a Must, Should or Could label, and a milestone, all on the board.

Bugs found on the way, all fixed:
- PR #50 merged into the step 5 branch instead of main (it was stacked). Its content reached main through #52's squash, and the re-land PR #51 was closed as redundant.
- The first deploy was refused at sign-in. The repository sends GitHub's immutable OIDC subject (with the owner and repo IDs), and the credential expected the old form (#53).
- The deploy check didn't wait for new revisions to become ready (#53).
- The first dev sign-in failed with AADSTS900144: the web Dockerfile didn't declare the Entra build arguments, so Docker dropped them (#54).

## Left over

- **Budget alert test.** The dev budget is at $1, and on 2026-11-28 spend was $0.76, past the 50% threshold. When the alert email has been seen, run a plain `terraform apply` in `infra/envs/dev` to restore the $10 budget, and tick the checklist item. Until then, any dev apply needs `-var budget_amount=1`. Owner: the user (email), then a session (apply).
- **Free Trial end date.** Check it in the portal and upgrade to pay-as-you-go before it ends (decision log, Open). Owner: the user.
- **Required status checks.** Branch protection doesn't require the CI jobs yet. Adding them is a repo setting. Owner: the user.
- **Demo environment.** Not applied. When it is: create the GitHub `demo` environment and its variables, and add the demo web URL to `web_redirect_uris` in `infra/identity`.

## Needed by the next milestone

M1 (content lifecycle) builds on:
- **Database:** the API's managed identity and the Azure SQL database (Entra-only auth). M1 adds EF Core, the first migrations, and a database role for the API identity.
- **File storage:** Blob Storage for the files. The API identity already has Blob access.
- **Users and groups:** `/api/me` provides the signed-in user and group IDs. Use the test users for the lock scenario (a second user blocked by the lock).
- **Deploys:** the CI/CD pipeline. A migration step must run before (or as part of) the API deploy, and it needs to be designed in M1.
- **Open question:** edit lock or optimistic concurrency (decision log, Open). UC-02 requires an exclusive lock, and this must be settled before UC-02 is built.
