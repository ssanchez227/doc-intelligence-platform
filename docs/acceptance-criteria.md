# Acceptance criteria

Given/When/Then for every scenario in `docs/plan.md` (UC-01 to UC-46; UC-18 dropped). Each section becomes one GitHub issue: the heading is the title, the Given/When/Then is the acceptance criteria, and the labels come from the priority and milestone lines. Every action that changes state also writes an audit event (a standing rule in CLAUDE.md), so it is only repeated below where the audit record itself is the point.

Version numbers follow the decision log: drafts take minor numbers (0.1, 0.2), publishing assigns the next major (1.0).

## Content lifecycle (M1)

### UC-01 Check in a new document
Actor: Contributor · Priority: Must · Milestone: M1

- **Given** a signed-in contributor, a category, and a file in a non-proprietary format (ODF, PDF, Markdown or plain text)
- **When** they check it in with its metadata and an ordered list of reviewers
- **Then** the document is created at version 0.1 as a draft
- **And** the category and metadata are saved and the file is stored in Blob Storage
- **And** the category's default permissions are applied
- **And** text extraction and indexing are queued
- **And** the review starts automatically at the first assigned reviewer (UC-09)
- **And** a Word or other proprietary file is rejected with a clear message and nothing is stored

### UC-02 Check out a document to edit
Actor: Contributor · Priority: Must · Milestone: M1

- **Given** a document the contributor may edit
- **When** they check it out
- **Then** the checkout records their name, the time it was taken and the version they started from
- **And** the document shows everyone who currently has it checked out, with their names and times
- **And** readers can still open the published version
- **And** a second contributor may also check it out; nobody is blocked (optimistic concurrency, see the decision log, M1 decisions)

### UC-03 Check in a new version
Actor: Contributor · Priority: Must · Milestone: M1

- **Given** a contributor who checked out a document whose latest version is 1.0
- **When** they check in a changed file
- **Then** a new draft version 1.1 is created and their checkout is closed
- **And** the review starts automatically (UC-09)
- **And** version 1.0 stays the published, visible version until the new one is approved
- **And** if someone else checked in a newer version after this contributor checked out, the check-in is refused: no version is created, they are told who checked in which version and when, and their checkout stays open so they can start again from the latest version

### UC-04 Cancel a checkout
Actor: Contributor · Priority: Should · Milestone: M1

- **Given** a contributor who has a document checked out
- **When** they cancel the checkout
- **Then** their checkout is closed
- **And** no new version is created
- **And** the cancellation appears in the audit trail

### UC-05 View version history and open an older version
Actor: Reader · Priority: Must · Milestone: M1

- **Given** a document with several versions that the reader may read
- **When** the reader opens its version history
- **Then** every version is listed with its number, author, date and status
- **And** the reader can open any older version they have access to
- **And** a version they have no access to cannot be opened and reveals no content

### UC-06 Restore an older version
Actor: Contributor · Priority: Could · Milestone: M1

- **Given** a document at version 3.0 with older versions 1.0 and 2.0
- **When** a contributor restores version 1.0
- **Then** a new draft version (3.1) is created with the content of 1.0
- **And** versions 1.0, 2.0 and 3.0 are unchanged
- **And** the restore follows the normal review before it can be published

### UC-07 Convert a published document to PDF
Actor: System · Priority: Could · Milestone: M1

- **Given** a version that has just been published
- **When** the system converts it to PDF
- **Then** the PDF is stored and linked to that exact version
- **And** if the conversion fails, the version is still published and the failure is recorded against it

### UC-08 Retire or archive a document
Actor: Administrator · Priority: Should · Milestone: M1

- **Given** a published document
- **When** an administrator retires it
- **Then** it no longer appears in search or Q&A answers
- **And** its versions and audit trail are kept and can still be viewed by those with access
- **And** an administrator can reverse the retirement, after which it returns to search and Q&A
- **And** nothing is permanently deleted

## Review and approval (M5)

### UC-09 Start a review (automatic at check-in)
Actor: Contributor · Priority: Must · Milestone: M5

- **Given** a contributor checking in a draft with reviewers A, B and C, in that order
- **When** the check-in completes
- **Then** a workflow starts at step 1 with reviewer A
- **And** the draft is locked from further edits while it is in review
- **And** reviewer A is notified
- **And** the start of the workflow is audited
- **And** a check-in with no reviewers assigned is rejected

### UC-10 Approve and pass to the next reviewer
Actor: Reviewer · Priority: Must · Milestone: M5

- **Given** a draft in review where the signed-in user is the current reviewer, and not the last one
- **When** they approve it with an optional comment
- **Then** the decision and comment are recorded
- **And** the next reviewer is notified
- **And** the document stays unpublished
- **And** a user who is not the current reviewer cannot approve it

### UC-11 Give final approval
Actor: Reviewer · Priority: Must · Milestone: M5

- **Given** a draft (such as 1.1) at its last review step, with version 1.0 published
- **When** the last reviewer approves it
- **Then** it is published as version 2.0 and becomes the current version
- **And** version 1.0 is archived
- **And** the search index is updated so search and Q&A use version 2.0

### UC-12 Give feedback, then send back or let it advance
Actor: Reviewer · Priority: Must · Milestone: M5

- **Given** a draft in review where the signed-in user is the current reviewer
- **When** they give feedback and choose to send it back
- **Then** the feedback, the choice and the reason are recorded, the workflow pauses and the author is notified
- **When** they instead give feedback and choose to let it advance
- **Then** the feedback, the choice and the reason are recorded and the next reviewer is notified with the feedback attached
- **And** either choice without feedback is rejected

### UC-13 Revise and resubmit after feedback
Actor: Contributor · Priority: Must · Milestone: M5

- **Given** a draft 0.2 sent back by reviewer B after reviewer A had approved it
- **When** the author checks in a revised file
- **Then** a new draft version 0.3 is created
- **And** the workflow resumes at reviewer B, not at reviewer A
- **And** the earlier feedback stays visible on the document

### UC-14 Remind or reassign an overdue review
Actor: System · Priority: Could · Milestone: M5

- **Given** a review step that has passed its due date
- **When** the overdue check runs
- **Then** the reviewer is reminded
- **And** an administrator can reassign the step to another reviewer, who is then notified
- **And** both the reminder and the reassignment are audited

## Access and reading (M2)

### UC-15 Set who can see a document
Actor: Contributor · Priority: Must · Milestone: M2 (index check in M3)

- **Given** a document the contributor owns and Entra groups G1 and G2
- **When** they grant read access to G1 only
- **Then** members of G1 see the document in listings and search, and members of G2 do not
- **And** the access rule is stored in Azure SQL
- **And** the search index's allowed groups for every chunk of the document are updated, and an automated test proves it (M3)

### UC-16 Open a document without access
Actor: Reader · Priority: Must · Milestone: M2

- **Given** a reader who has no access to a document
- **When** they open it by its link or ID
- **Then** access is denied with a response that is the same as for a document that does not exist
- **And** no title, metadata or content is returned
- **And** the attempt is recorded in the audit trail

### UC-17 Check out a document for reading
Actor: Reader · Priority: Should · Milestone: M2

- **Given** a reader with access to a published document
- **When** they check it out for reading
- **Then** the checkout is recorded and shown to them
- **And** it has no expiry and stays until they return it
- **And** other readers can still read or check it out at the same time

### UC-19 Return a document early, with optional feedback
Actor: Reader · Priority: Should · Milestone: M2

- **Given** a reader with a reading checkout
- **When** they return it, optionally with feedback
- **Then** the checkout ends
- **And** any feedback is saved and visible to the document's owner

## Ingestion and search (M3)

### UC-20 Search by keyword and metadata
Actor: Reader · Priority: Must · Milestone: M3

- **Given** documents in several categories, states and versions, some the reader may see and some they may not
- **When** the reader searches by keyword and filters by category, state or version
- **Then** only documents they may see are returned
- **And** each filter narrows the results correctly
- **And** the result count does not reveal restricted documents

### UC-28 Handle a file that cannot be processed
Actor: System · Priority: Should · Milestone: M3

- **Given** a checked-in file whose text cannot be extracted
- **When** the worker processes it
- **Then** the version is marked failed with a reason
- **And** the author is notified and can retry or replace the file
- **And** other uploads in the queue are processed normally
- **And** a job that keeps failing ends in the dead-letter queue instead of retrying forever

## Questions and citations (M4)

UC-22, UC-23 and UC-25 are the leak tests for the evaluation harness.

### UC-21 Ask a question and get a cited answer
Actor: Reader · Priority: Must · Milestone: M4

- **Given** published documents that answer a question, which the reader may see
- **When** the reader asks the question
- **Then** the answer uses only text from documents they may see
- **And** every citation links to the exact source version

### UC-22 Ask a question that touches a restricted document
Actor: Reader · Priority: Must · Milestone: M4

- **Given** a restricted document that contains the answer, which the reader may not see
- **When** the reader asks the question
- **Then** the answer contains no text from it, does not name it, and does not hint that it exists
- **And** no text from it was sent to the model (checked in the evaluation harness)

### UC-23 Ask a question after their access was removed
Actor: Reader · Priority: Must · Milestone: M4

- **Given** a reader who has just lost access to a document, and a search index that still lists them as allowed
- **When** they ask a question that the document answers
- **Then** the SQL re-check removes it before any text reaches the model
- **And** the answer contains nothing from it

### UC-24 Ask a question with no supporting document
Actor: Reader · Priority: Should · Milestone: M4

- **Given** no document the reader may see that answers the question
- **When** they ask it
- **Then** the system says it cannot answer from the documents they have access to
- **And** it does not guess or answer from general knowledge

### UC-25 Try to talk the assistant out of the rules
Actor: Reader · Priority: Should · Milestone: M4

- **Given** a reader who asks the assistant to ignore its rules or reveal restricted content
- **When** the question is processed
- **Then** the request is refused
- **And** no restricted text is returned
- **And** the attempt is logged for an administrator

### UC-26 Ask about an unpublished draft
Actor: Reader · Priority: Should · Milestone: M4

- **Given** a draft in review that a general reader may not see
- **When** the reader searches for it or asks about its content
- **Then** it is excluded from their results and answers
- **And** the draft's owner and its reviewers can find it and ask about it

### UC-41 Rate an answer and report a wrong or leaking one
Actor: Reader · Priority: Should · Milestone: M4

- **Given** a reader who has received an answer
- **When** they rate it, or report it as wrong or leaking
- **Then** the rating, question, answer and sources are stored
- **And** a leak report notifies an administrator
- **And** stored cases can be added to the evaluation set

## Audit and administration (M6)

### UC-27 View the audit trail for a document
Actor: Administrator · Priority: Must · Milestone: M6

- **Given** a document that has been checked in, versioned, reviewed, had its permissions changed and been checked out for reading
- **When** an administrator opens its audit trail
- **Then** each event is shown with who, what and when
- **And** the trail cannot be edited by anyone
- **And** a non-administrator cannot open it

Note: the plan doc still says "reading leases" here; read it as reading checkouts, since leases were dropped with UC-18.

### UC-29 Clear a stale checkout
Actor: Administrator · Priority: Should · Milestone: M6

Rewritten in M1: checkouts no longer lock (optimistic concurrency), so there is no lock to break. A checkout left open still misleads others about who is editing.

- **Given** a document someone else has had checked out for a long time
- **When** an administrator clears that checkout with a reason
- **Then** the checkout is closed and the reason is recorded
- **And** the former holder is notified
- **And** any unsaved work from the old checkout is not turned into a version

### UC-30 Manage categories and metadata fields
Actor: Administrator · Priority: Should · Milestone: M6

- **Given** an administrator
- **When** they create or change a category with its metadata fields, default permissions and default workflow
- **Then** new documents in that category get those fields, permissions and workflow
- **And** existing documents keep their current permissions unless changed explicitly

### UC-31 Define or change a workflow template
Actor: Administrator · Priority: Could · Milestone: M6

- **Given** an administrator
- **When** they define a workflow template with ordered steps and approver groups
- **Then** a contributor can choose that template at check-in instead of listing reviewers by hand
- **And** reviews already running keep the steps they started with

## Modern capabilities (M7)

### UC-32 Classify a new document and extract metadata
Actor: System · Priority: Should · Milestone: M7

- **Given** a new document being checked in
- **When** the system analyses it
- **Then** a category and fields such as parties and dates are suggested, each with a confidence score
- **And** low-confidence suggestions wait for the contributor to confirm or change them
- **And** every suggestion and the contributor's choice are audited

### UC-33 Detect a duplicate or near-duplicate on check-in
Actor: System · Priority: Could · Milestone: M7

- **Given** a file that matches or nearly matches an existing document the contributor may see
- **When** they check it in
- **Then** they are warned and shown the match
- **And** they can cancel, continue as a new document, or check it in as a new version of the match
- **And** a match the contributor may not see is never shown to them

### UC-34 Apply a retention schedule
Actor: System · Priority: Should · Milestone: M7

- **Given** a category with a retention period
- **When** a document in that category reaches the end of its retention period
- **Then** it goes to a disposition review
- **And** after disposition it is archived, never permanently deleted

### UC-35 Place and release a legal hold
Actor: Administrator · Priority: Could · Milestone: M7

- **Given** a document under a retention schedule
- **When** an administrator places a legal hold with a reason
- **Then** the document cannot be archived or disposed of and its retention timer pauses
- **When** the hold is released
- **Then** the timer resumes
- **And** the hold, its reason and its release are audited

### UC-36 Share a document with an external reviewer
Actor: Contributor · Priority: Should · Milestone: M7

- **Given** a contributor who owns a document
- **When** they share it with an external reviewer until a set date
- **Then** the guest can open only that document, until that date or until the share is revoked
- **And** the guest never reaches search or Q&A
- **And** every guest action is audited

### UC-37 Sign an approval electronically
Actor: Reviewer · Priority: Could · Milestone: M7

- **Given** a reviewer approving a specific version
- **When** they sign the approval
- **Then** the signer and time are recorded against that exact version
- **And** a later version does not carry the signature over
- **And** a real e-signature provider is a stretch goal

### UC-38 Flag sensitive data on check-in
Actor: System · Priority: Could · Milestone: M7

- **Given** a file that contains personal or confidential data
- **When** it is checked in
- **Then** it is flagged and restricted to its owner
- **And** it is excluded from Q&A until the owner confirms its permissions

### UC-39 Bulk import legacy documents with metadata mapping
Actor: Administrator · Priority: Should · Milestone: M7

- **Given** an export of files, metadata and permissions from another system, and a field mapping
- **When** an administrator runs the import
- **Then** the files, metadata and permissions are imported
- **And** failures are listed with reasons
- **And** running the import again does not create duplicates

### UC-40 Flag stale content for re-certification
Actor: System · Priority: Should · Milestone: M7

- **Given** a document past its review date
- **When** the stale-content check runs
- **Then** the document is flagged and its owner is notified
- **And** the owner can re-certify, update or retire it
- **And** until then it is marked as stale in search and Q&A

### UC-42 Let an AI assistant search on the reader's behalf through MCP
Actor: Reader · Priority: Could · Milestone: M7

- **Given** a reader who connects an AI assistant through the MCP server
- **When** the assistant searches or asks a question
- **Then** it sees only what that reader can see
- **And** each call is audited under the reader's identity
- **And** the leak tests UC-22, UC-23 and UC-25 pass through MCP as well

### UC-43 Follow a document and get notified of new versions
Actor: Reader · Priority: Could · Milestone: M7

- **Given** a reader who follows a document
- **When** a new version is published or the document is retired
- **Then** the reader is notified
- **And** a reader who has lost access is not notified

### UC-44 Compare two versions
Actor: Reader · Priority: Could · Milestone: M7

- **Given** two versions of a document that the reader may open
- **When** they compare them
- **Then** the changes are shown side by side
- **And** a version they may not open cannot be chosen for comparison

### UC-45 Request access to a restricted document from a link they already have
Actor: Reader · Priority: Could · Milestone: M7

- **Given** a reader with a link to a document they may not read
- **When** they request access with a reason
- **Then** the request reaches the owner
- **And** the owner's decision is audited and the reader is told the outcome
- **And** neither the request page nor the response reveals the document's title or content

### UC-46 Send a read-only copy of a document to another user, subject to approval
Actor: Reader · Priority: Should · Milestone: M7

- **Given** a reader who may read a document version
- **When** they request to send a read-only copy to a named user, with a reason
- **Then** the request waits for an approver
- **And** on approval the recipient can open that exact version read-only, cannot check it out or edit it, and gains no other access
- **And** on rejection the sender is notified
- **And** a reader cannot request a copy of anything they cannot read
- **And** the request, the decision and every opening of the copy are audited

Open details (decision log, M7): who approves, whether the copy expires, and whether the recipient can be outside the system.
