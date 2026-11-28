# Document Intelligence Platform

A permission-aware document management and Q&A platform on Azure. Spec and scenarios (UC-01 to UC-46) live in `docs/plan.md`; build briefs, the decision log and session templates live in `docs/build/` (start at `docs/build/00-index.md`). Read `docs/build/project-context.md` first in a new session. The repo is the working copy; the claude.ai plan doc and Doc Mgt Project are the original source and may be older.

## How to work here

- Plan first, then build. Do not write code before the plan is approved.
- Work in small steps: tests pass, commit, one line on what changed.
- A decision made in a session goes into the decision log the moment it is made.
- Every scenario maps to an issue labelled with its UC ID and a Must, Should or Could label.

## Layout

- `src/api`: .NET minimal API (C#, EF Core)
- `src/worker`: ingestion worker
- `src/web`: React with TypeScript and TanStack Query
- `infra`: Terraform (azurerm), dev and demo from the same code
- `docs`: diagrams and write-ups

## Rules that must not be broken

- Azure SQL is the source of truth for permissions. The search index holds a copy (allowed groups per chunk); SQL re-verifies before any text reaches the model.
- Versions are immutable. Drafts use minor numbers (0.1), publishing assigns the next major (1.0).
- No permanent deletion, only archiving.
- Managed identity for service-to-service access; no secrets in the repo.
- The repo holds synthetic or public-domain documents only. Nothing from an employer.
- Every action writes an audit event.

## Local tools

Installed with winget on Windows (versions as of 2026-11-17): git 2.56.0, .NET SDK 10.0.404, Terraform 1.17.1, Azure CLI 2.92.0, Node 26.13.0, GitHub CLI 2.104.0. Docker is not installed locally; container images are built in CI.

## Commands

Filled in as each part is created (build, test, run, deploy).

- .NET (API and worker, solution `DocIntel.slnx`, .NET 10 LTS): `dotnet build DocIntel.slnx`, `dotnet test DocIntel.slnx`. Run the API with `dotnet run --project src/api/DocIntel.Api`. ASP.NET packages stay on 10.0.x; the newest ones target .NET 11.
- Database (EF Core): Azure SQL in Azure, SQLite locally and in tests (`App_Data/` under the API, gitignored). Add a migration with `dotnet tool restore` then `dotnet ef migrations add <Name> --project src/api/DocIntel.Api --output-dir Data/Migrations` (always generated for SQL Server). To run the API tests against SQL Server, set `DOCINTEL_TEST_SQLSERVER` to a connection string; CI does this in the "API tests on SQL Server" job.
- API in Azure (M1): settings come from Terraform (`infra/modules/platform`, `local.app_settings.api`): Azure SQL and Blob through the API's managed identity, group IDs (`group_object_ids` in each environment's gitignored tfvars, from `terraform output -json group_object_ids` in `infra/identity`), and `Features__DevPublish` (dev only). Once per environment, before the first apply with these settings, run `scripts/dev/grant-api-database-access.sql` as a SQL admin (portal Query editor) so the API identity has its database user; the API then applies migrations at startup.
- Web (`src/web`, Vite, React, TypeScript, TanStack Query): `npm ci`, `npm test` (Vitest), `npm run build`, `npm run lint` (oxlint), `npm run dev`.
- Local sign-in: after `infra/identity` is applied and initialised, run `./scripts/dev/write-local-auth-config.ps1` once; it writes the Entra IDs to `src/web/.env.development.local` and the API's user secrets (both outside the repo). Then run the API (`http` profile, port 5299) and `npm run dev` (port 5173), and sign in as a test user from `infra/identity` outputs.
- Windows Smart App Control may block a freshly built test DLL ("An Application Control policy has blocked this file"); `dotnet test -c Release` gets a different build. Do not turn the protection off.
- Container images: `src/api/Dockerfile`, `src/worker/Dockerfile`, `src/web/Dockerfile`, each built with its own folder as the context. All listen on 8080 (worker has no port) and run as non-root. Docker is not installed locally, so CI builds them.
- Scenario issues: `./scripts/github/create-issues.ps1 -Repo ssanchez227/doc-intelligence-platform` creates labels, milestones and one issue per section of `docs/acceptance-criteria.md`. Safe to re-run; add `-WhatIf` for a dry run.
- Terraform: see `infra/README.md`. Bootstrap state once (`infra/bootstrap`, `terraform apply`), then per environment `terraform init -backend-config=backend.hcl` and `terraform plan` / `apply` in `infra/envs/dev` or `infra/envs/demo`. Check with `terraform fmt -recursive -check` in `infra/` and `terraform validate` in each root.
- CI/CD: `.github/workflows/ci-cd.yml` runs the .NET, web and Terraform checks on every PR and push; a push to main also builds the images and deploys them to dev through OIDC (GitHub environment `dev`, main only). Watch a run with `gh run watch`. Terraform is applied by hand.
- main is protected: every change goes through a pull request, merged by squash or rebase (linear history).
