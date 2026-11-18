# M3 Ingestion and search

**Goal:** an uploaded document becomes searchable, and search only returns what the reader may see.

## Scenarios

| ID | Scenario | Priority |
|---|---|---|
| UC-20 | Search by keyword and metadata | Must |
| UC-28 | Handle a file that cannot be processed | Should |
| UC-15 (index check) | Permission changes reach the search index, verified by a test | Must |

## Notes

- Pipeline: upload API stores the file and queues a job; the worker extracts text, splits chunks, creates embeddings and writes each chunk to AI Search with its allowed groups.
- A failure marks the version failed with a reason, notifies the author, allows retry or replace, and never blocks other uploads.
- Search filters on allowed groups in the index, then SQL re-verifies. Filters work for category, status and version.
- Drafts are excluded for general readers; this prepares UC-26 in M4.
- Vector capacity on the AI Search free tier must be tested first; if it does not fit, record the cost of the smallest paid tier in the decision log and tear down between sessions.
- Permission changes trigger a re-index of that document's chunks.

## Design references

Ingestion pipeline diagram, the permission-change and re-index diagram from M2, data model (Chunk); wireframe Library with search.

## Done checklist

- [ ] A document uploaded in dev appears in search without a manual step
- [ ] A reader never sees a result outside their groups; a test proves it with several groups
- [ ] After a permission change the index matches SQL, tested
- [ ] A corrupt file ends in a failed state with a reason and a retry path
- [ ] Index and worker costs are measured and recorded
- [ ] Session log written and the index updated
