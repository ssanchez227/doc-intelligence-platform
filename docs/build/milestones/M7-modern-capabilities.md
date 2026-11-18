# M7 Modern capabilities

**Goal:** the proposed modern features, in four batches. Each batch gets its own chat and session log (M7a to M7d). Order inside the milestone can change; the batches are independent.

## M7a AI-assisted ingestion

| ID | Scenario | Priority |
|---|---|---|
| UC-32 | Classify a new document and extract metadata | Should |
| UC-33 | Detect a duplicate or near-duplicate on check-in | Could |
| UC-38 | Flag sensitive data on check-in | Could |
| UC-39 | Bulk import legacy documents with metadata mapping | Should |

## M7b Governance

| ID | Scenario | Priority |
|---|---|---|
| UC-34 | Apply a retention schedule (archive only, never delete) | Should |
| UC-35 | Place and release a legal hold | Could |
| UC-40 | Flag stale content for re-certification | Should |

## M7c Sharing and approval

| ID | Scenario | Priority |
|---|---|---|
| UC-36 | Share a document with an external reviewer | Should |
| UC-37 | Sign an approval electronically | Could |
| UC-45 | Request access to a restricted document | Could |
| UC-46 | Send a read-only copy to another user, with approval | Should |

UC-46 details are open: who approves, whether the copy expires, and whether the recipient can be outside the system. Settle them in the decision log first.

## M7d Reader experience and AI

| ID | Scenario | Priority |
|---|---|---|
| UC-42 | Let an AI assistant search on the reader's behalf through MCP | Could |
| UC-43 | Follow a document and get notified of new versions | Could |
| UC-44 | Compare two versions | Could |

## Notes

- Every batch keeps the permission rules: copies, notifications, comparisons and MCP calls must respect current access and be audited.
- UC-42 reuses the leak tests from M4.

## Done checklist (per batch)

- [ ] Each scenario has a passing test, including its negative cases
- [ ] Permissions and audit hold for every new path
- [ ] Wireframes added for any new screen
- [ ] Session log written and the index updated
