# M5 Review and approval

**Goal:** the workflow engine: checking in starts a review, reviewers decide, and approval publishes.

## Scenarios

| ID | Scenario | Priority |
|---|---|---|
| UC-09 | Start a review (automatic at check-in) | Must |
| UC-10 | Approve and pass to the next reviewer | Must |
| UC-11 | Give final approval | Must |
| UC-12 | Give feedback, then send back or let it advance | Must |
| UC-13 | Revise and resubmit after feedback | Must |
| UC-14 | Remind or reassign an overdue review | Could |

## Notes

- The author assigns reviewers in order. The first reviewer is notified; the draft is locked from edits.
- A reviewer may send the document back or advance it with feedback attached. After a send-back the author checks in a new minor version and the workflow continues at that reviewer.
- Final approval publishes as the next major version, archives the previous version and updates the search index.
- Nothing is published until the last reviewer approves; readers keep the old version until then.
- Wire the automatic start into the check-in built in M1.
- Fixed sequential approval only. Parallel approval and predefined templates are out of scope (templates come in M6).

## Design references

Review and approval swimlane, lifecycle diagram; wireframes Upload (reviewer field) and Review.

## Done checklist

- [ ] Each scenario has a passing test, including send back, advance with feedback, and an unauthorized approver refused
- [ ] The published version stays visible while a newer one is in review
- [ ] Publishing archives the old version and updates the index, tested
- [ ] Notifications work for new reviews and returns
- [ ] Audit events recorded for every decision
- [ ] Session log written and the index updated
