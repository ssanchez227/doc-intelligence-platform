# Project context and handoff

Read this first in any new Claude Code session. It carries what was decided in the planning chats, which Claude Code cannot see.

## What this is

A permission-aware document intelligence platform on Azure: classic document management (versions, check-out, review, audit) plus AI answers limited to what the asker may read. The repo is public, so it holds engineering material only.

## Non-negotiables

- Keep the full scope (UC-01 to UC-46, UC-18 dropped). If time runs long, the schedule stretches; nothing is cut. Priorities (Must, Should, Could) only set the order inside a milestone.
- Plan first, then build, in small steps with tests and a commit each time.
- The rules in CLAUDE.md apply to every change.

## Where things are

- Spec and scenarios: `docs/plan.md` (text copy; its seven diagrams are placeholders, see the live doc below).
- Build index, milestone briefs M0 to M8, decision log, templates: `docs/build/`.
- Live plan doc with diagrams: https://claude.ai/artifact/LGQ3wknML3PsYD77h7YkRk
- Wireframes (six screens): https://claude.ai/artifact/TkVVMhkyMTnuyYvGQzzsBy
- Still to draw: permission-change and re-index diagram (before M3); wireframes for permissions admin, audit viewer, failed-ingestion list, break lock.

## Current status

M0 Foundation is closed (2026-11-28); see `docs/build/session-logs/M0-session-log.md`. dev runs in Central US: web, API and worker deployed by CI/CD from main through OIDC, Entra sign-in with test users and groups (`infra/identity`), telemetry in Application Insights. Left from M0: the budget alert test (dev budget at $1 until the alert email is seen, so any dev apply needs `-var budget_amount=1`). Next: M1 Content lifecycle.

## Open decisions

See "Open" in `docs/build/decision-log.md`: AI Search free-tier vector capacity, edit lock or optimistic concurrency, Free Trial end date, UC-46 details, send-back restart point.

## How to start a session

Paste the opening prompt from `docs/build/templates.md` with the milestone filled in, for example: "Milestone 0: Foundation." Close with the closing prompt, which writes the session log and updates the decision log and index.

## Things only the user can do

Sign in to Azure (`az login`) and GitHub, create the subscription, approve cost-bearing resources, and review and merge pull requests.

## Lessons from setup

- `gh pr create` can fail with a GraphQL 403 in some environments; use `gh api repos/{owner}/{repo}/pulls` instead.
- PowerShell here-strings don't pipe into `git commit -F -` or `gh pr create --body-file -`; write the message to a file and pass its path.
- Don't stack PRs on each other's branches: when the base PR merges first, the stacked one merges into the old branch, not main.
- The API scales to zero, so the first call after a quiet spell takes several seconds (cold start).
- The plan doc's lifecycle diagram caption still says 6 states; there are five (In review, Sent back, Published, Retired, Archived).
