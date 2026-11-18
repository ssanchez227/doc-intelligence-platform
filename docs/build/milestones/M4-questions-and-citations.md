# M4 Questions and citations

**Goal:** the showcase feature. Readers ask questions and get cited answers built only from text they are allowed to read, proven by an evaluation harness.

## Scenarios

| ID | Scenario | Priority |
|---|---|---|
| UC-21 | Ask a question and get a cited answer | Must |
| UC-22 | Ask a question that touches a restricted document | Must |
| UC-23 | Ask a question after access was removed | Must |
| UC-24 | Ask a question with no supporting document | Should |
| UC-25 | Try to talk the assistant out of the rules | Should |
| UC-26 | Ask about an unpublished draft | Should |
| UC-41 | Rate an answer and report a wrong or leaking one | Should |

## Notes

- Flow: identify the reader, search with the filter, re-check in SQL, ask the model, answer with citations. The model only ever sees text the reader may read.
- UC-23 relies on the SQL re-check, so it must pass even when the index is stale.
- "Cannot answer" states reveal nothing about restricted documents.
- Build the evaluation harness alongside the feature. Each negative and security scenario is a test case with expected and forbidden outputs, run on every change.
- Citations link to the exact version used.
- Keep model costs low: small models and short contexts; record spend in the decision log.

## Design references

Permission-aware question flow; wireframe Ask with citations.

## Done checklist

- [ ] The evaluation harness runs in CI and covers UC-21 to UC-26
- [ ] No leak case passes through: restricted titles, content and existence never appear
- [ ] A stale-index test proves UC-23
- [ ] Prompt-injection cases are refused and logged
- [ ] Ratings and leak reports are stored and reach an administrator
- [ ] A short demo clip shows two users with different access getting different answers
- [ ] Session log written and the index updated
