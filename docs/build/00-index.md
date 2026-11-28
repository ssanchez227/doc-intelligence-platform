# Document Intelligence Platform: build index

The single starting page for every build chat. Read this, then the brief for your milestone, then the decision log.

**Master spec:** the plan doc, "Showcase Project Plan: Permission-Aware Document Intelligence Platform". A text copy lives in this repo at `docs/plan.md` (the seven diagrams appear there only as placeholders). The live version, with diagrams, is at https://claude.ai/artifact/LGQ3wknML3PsYD77h7YkRk. It holds the scenarios (UC-01 to UC-46), the six workflow and data diagrams, the architecture and the stack. Change it only when a decision changes.

**Wireframes:** https://claude.ai/artifact/TkVVMhkyMTnuyYvGQzzsBy (six screens so far).

**Rule:** the whole scope stays. If time runs long, the schedule stretches; scenarios are not cut. Priority labels (Must, Should, Could) only decide the order inside a milestone.

## Milestones

| Chat | Scope | Scenarios | Status |
|---|---|---|---|
| M0 Foundation | Repo, Terraform, CI/CD, sign-in, board, CLAUDE.md | none (enabler) | Done except the budget alert test (2026-11-28), see the M0 session log |
| M1 Content lifecycle | Check in and out, versions, history, restore, PDF, archive | UC-01 to UC-08 | Not started |
| M2 Access and reading | Permissions, denied access, reading checkout | UC-15, 16, 17, 19 (UC-18 dropped) | Not started |
| M3 Ingestion and search | Pipeline, index, keyword search, failures | UC-20, UC-28, index check for UC-15 | Not started |
| M4 Questions and citations | Q&A, leak tests, evaluation, feedback | UC-21 to UC-26, UC-41 | Not started |
| M5 Review and approval | Workflow, send back or advance, publish | UC-09 to UC-14 | Not started |
| M6 Audit and administration | Audit trail, break lock, categories, workflow templates | UC-27, 29, 30, 31 | Not started |
| M7 Modern capabilities | Four batches, see the M7 brief | UC-32 to UC-40, UC-42 to UC-46 | Not started |
| M8 Hardening and demo | README, diagrams, demo | none (finish) | Not started |

Every scenario UC-01 to UC-46 appears in exactly one milestone. UC-18 was dropped because reading checkouts do not expire.

## Pages

- Briefs: `docs/build/milestones/M0-foundation.md` through `M8-hardening-and-demo.md`
- Decisions: `docs/build/decision-log.md`
- Opening and closing prompts: `docs/build/templates.md`
- Session logs: written at the end of each chat as `docs/build/session-logs/Mn-session-log.md`
- Project context and handoff notes: `docs/build/project-context.md`

## How a milestone runs

1. Open a new chat or Claude Code session named after the milestone and paste the opening prompt from the templates page.
2. Plan first, then build in small steps. Building happens in Claude Code sessions on the repo; the standing rules live in CLAUDE.md.
3. Close with the closing prompt: session log, decision log, checklist, and what the next milestone needs.
4. Review later with the session log, the milestone's pull request, the tag and the demo clip.
