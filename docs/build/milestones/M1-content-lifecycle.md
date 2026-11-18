# M1 Content lifecycle

**Goal:** documents can be checked in, checked out, versioned, restored and archived, with every action audited.

## Scenarios

| ID | Scenario | Priority |
|---|---|---|
| UC-01 | Check in a new document | Must |
| UC-02 | Check out a document to edit | Must |
| UC-03 | Check in a new version | Must |
| UC-04 | Cancel a checkout | Should |
| UC-05 | View version history and open an older version | Must |
| UC-06 | Restore an older version | Could |
| UC-07 | Convert a published document to PDF | Could |
| UC-08 | Retire or archive a document | Should |

## Notes

- Versions are immutable. Drafts take minor numbers (0.1, 0.2); a published version takes the next major (1.0).
- Exclusive edit lock shows its owner and time. Readers keep seeing the published version.
- Accepted file formats: any non-proprietary format for now.
- UC-01 and UC-03 also start the review automatically. That step is wired in M5; until then a checked-in version stays a draft.
- Archiving is reversible and nothing is permanently deleted.
- Write an audit event for every action, even though the viewer comes in M6.
- Text extraction and indexing are M3; here the file is stored and its version recorded.

## Design references

Lifecycle diagram, check out and check in flow, data model; wireframes Library, Document detail, Upload.

## Done checklist

- [ ] Each scenario above has a passing automated test, including a second user blocked by the lock
- [ ] Version numbers follow the minor and major rule, tested
- [ ] Restore creates a new version and never rewrites history
- [ ] The React screens for library, detail and upload work end to end in dev
- [ ] Audit events exist for check-in, check-out, cancel, restore and archive
- [ ] Session log written and the index updated
