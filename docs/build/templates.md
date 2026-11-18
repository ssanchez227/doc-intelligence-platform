# Session templates

## Opening prompt (paste into a new chat or Claude Code session)

> Milestone N: [name]. Read `docs/build/00-index.md`, `docs/build/milestones/MN-....md` and `docs/build/decision-log.md`, then the scenarios listed in the brief from `docs/plan.md`. Summarize the goal and the done checklist back to me, list anything in the brief that is unclear or blocked, and propose a step plan. Plan first; do not build until I approve the plan.

## During the session

- Work in small steps. After each step: tests pass, commit, one line on what changed.
- A new decision goes into `docs/build/decision-log.md` the moment it is made, with its reason.
- A change to a scenario, diagram or wireframe is made in the plan doc or the wireframes, not left in the chat.
- If the session is getting long or the area changes, stop and write the session log, then continue in a new chat.

## Closing prompt

> Close milestone N. Write `docs/build/session-logs/MN-session-log.md` using the template below, update `docs/build/decision-log.md`, tick the done checklist in the brief, update the status in `docs/build/00-index.md`, and tell me what the next milestone needs from this one.

## Session log template

```
# MN session log: [name]
Date range:
Branch / PR:
Tag:
Demo clip:

## What was built
(scenarios completed, by UC ID)

## What changed from the brief
(deviations and why)

## Decisions made
(also added to the decision log)

## Tests and evidence
(what proves each scenario works, including the negative cases)

## Left over
(unfinished work, bugs, follow-ups, with owners)

## Needed by the next milestone
```

## Review checklist (for going back later)

1. Open the session log and the pull request.
2. Run the demo clip and the evidence listed.
3. Check each UC ID in scope has a passing test.
4. Check the decision log has everything decided in that chat.
