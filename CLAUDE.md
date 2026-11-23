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

- Scenario issues: `./scripts/github/create-issues.ps1 -Repo ssanchez227/doc-intelligence-platform` creates labels, milestones and one issue per section of `docs/acceptance-criteria.md`. Safe to re-run; add `-WhatIf` for a dry run.
- Terraform: see `infra/README.md`. Bootstrap state once (`infra/bootstrap`, `terraform apply`), then per environment `terraform init -backend-config=backend.hcl` and `terraform plan` / `apply` in `infra/envs/dev` or `infra/envs/demo`. Check with `terraform fmt -recursive -check` in `infra/` and `terraform validate` in each root.
- main is protected: every change goes through a pull request, merged by squash or rebase (linear history).
