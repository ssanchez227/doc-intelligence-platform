# M2 Access and reading

**Goal:** permissions are real and enforced in the API: the right people see the right documents, and everyone else gets nothing.

## Scenarios

| ID | Scenario | Priority |
|---|---|---|
| UC-15 | Set who can see a document | Must |
| UC-16 | Open a document without access | Must |
| UC-17 | Check out a document for reading | Should |
| UC-19 | Return a document early, with optional feedback | Should |

UC-18 (expire a reading lease) was dropped.

## Notes

- Access is granted to Entra groups and stored in Azure SQL, the source of truth.
- A denied request leaks nothing: no title, no content, no hint it exists. The attempt is recorded.
- Reading checkout never expires and does not block other readers. The reader keeps it until they return it; feedback on return is visible to the owner.
- UC-15 also requires the search index to be updated and verified by a test. The index arrives in M3, so write the permission-change event now and finish the verification test in M3.
- A permission-change and re-index diagram is still to be drawn; do it at the start of this milestone.

## Design references

Data model (Permission, Lease); wireframes Document detail (Permissions tab, still to draw).

## Done checklist

- [ ] UC-15, UC-16, UC-17 and UC-19 each have passing tests, including the negative cases
- [ ] A leak test proves a denied user learns nothing about a document, including its title
- [ ] The permission-change and re-index diagram is in the plan doc
- [ ] A permission-change event is emitted for M3 to consume
- [ ] Session log written and the index updated
