# M6 Audit and administration

**Goal:** administrators can see what happened and manage the system.

## Scenarios

| ID | Scenario | Priority |
|---|---|---|
| UC-27 | View the audit trail for a document | Must |
| UC-29 | Clear a stale checkout (was: break a stuck edit lock) | Should |
| UC-30 | Manage categories and metadata fields | Should |
| UC-31 | Define or change a workflow template | Could |

## Notes

- Audit shows who did what and when: check-ins, versions, approvals, permission changes and reading checkouts. Events have been written since M1; this adds the viewer.
- Clearing a stale checkout needs a recorded reason and notifies the owner. Checkouts stopped locking in M1 (decision log, M1 decisions).
- Each category carries default permissions and a default workflow. Workflow templates are the "predefined workflows" improvement the review flow was designed for.
- Wireframes still to draw: permissions admin, audit viewer, failed-ingestion list, clear checkout.

## Design references

Data model (Audit event, Category); new wireframes for the admin screens.

## Done checklist

- [ ] The audit trail is complete and filterable, with a test for each event type from earlier milestones
- [ ] Only administrators can reach the admin screens, tested
- [ ] Clearing a stale checkout records a reason and notifies the owner
- [ ] Category defaults apply to new check-ins
- [ ] Workflow templates can be defined and used by the review flow
- [ ] Session log written and the index updated
