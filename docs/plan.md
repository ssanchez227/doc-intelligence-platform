# Showcase Project Plan: Permission-Aware Document Intelligence Platform

Nov 5, 2026

## Overview

A permission-aware document intelligence platform on Azure, built in 12 weeks at 30 to 90 minutes a day.

Azure is the only cloud. It fits a .NET stack, and the infrastructure is written in Terraform.

## The project

A small, modern descendant of Documentum: users upload documents, the system extracts and indexes them, enforces who can see what, and answers questions using only the documents the asker is allowed to read.

This is the problem enterprises are paying to solve now: classic document management (versions, check-out, review, audit, as in Documentum, Stellent and Lotus Notes/Domino) combined with AI answers that must never leak what the asker cannot read.

## Use case scenarios

Forty-five scenarios in six groups, starting from your list: 16 Must, 17 Should and 12 Could, plus UC-18, which was dropped. Each has an ID so it can be traced to a workflow diagram, a wireframe, a GitHub issue and an automated test. The negative and security scenarios are the showcase, and they feed the evaluation harness.

**Actors:** Contributor (creates and edits documents), Reader, Reviewer (approver in a workflow), Administrator, and System (automated steps such as indexing, conversion and lease expiry).

**Priority key:** Must = needed for the showcase by week 10. Should = build if time allows. Could = stretch.

### Content lifecycle

| ID | Actor | Scenario | What must be true afterward | Priority |
| --- | --- | --- | --- | --- |
| UC-01 | Contributor | Check in a new document | Version 0.1 assigned as a draft; category and metadata captured; file stored (any non-proprietary format); text extracted and indexed; default permissions applied from the category; the author assigns the reviewers and the review starts automatically | Must |
| UC-02 | Contributor | Check out a document to edit | Checkout recorded with its owner, time and starting version and shown to others; nobody is blocked; a check-in based on an out-of-date version is refused and told who checked in since (optimistic concurrency); others can still read the published version | Must |
| UC-03 | Contributor | Check in a new version | Next minor version number (such as 0.2) assigned as a draft and the review starts automatically; the published version stays visible until the new one is approved | Must |
| UC-04 | Contributor | Cancel a checkout | Lock released; no new version created; recorded in the audit trail | Should |
| UC-05 | Reader | View version history and open an older version | Every version listed with author, date and status; older versions open if the reader has access | Must |
| UC-06 | Contributor | Restore an older version | Old content becomes a new version; history is never rewritten | Could |
| UC-07 | System | Convert a published document to PDF | PDF stored and linked to that version; a failed conversion does not block publishing | Could |
| UC-08 | Administrator | Retire or archive a document | Removed from search and Q&A; kept in history and the audit trail; reversible | Should |

### Review and approval

| ID | Actor | Scenario | What must be true afterward | Priority |
| --- | --- | --- | --- | --- |
| UC-09 | Contributor | Start a review (automatic at check-in) | Workflow starts at step 1 with the reviewers the author assigned, in the order assigned; the draft is locked from edits; the first reviewer is notified; the start is audited | Must |
| UC-10 | Reviewer | Approve and pass to the next reviewer | Decision and comment recorded; the next reviewer is notified; the document stays unpublished until the last step | Must |
| UC-11 | Reviewer | Give final approval | Document published as the next major version (such as 1.0) and becomes the current version; the previous version is archived; the search index is updated | Must |
| UC-12 | Reviewer | Give feedback, then send back or let it advance | Comments recorded, and the reviewer chooses: send it back to the author (the workflow stops) or let it advance to the next reviewer with the feedback attached; the choice and reason are recorded | Must |
| UC-13 | Contributor | Revise and resubmit after feedback | New minor draft version created; the workflow resumes at the reviewer who sent it back; earlier feedback remains visible | Must |
| UC-14 | System | Remind or reassign an overdue review | Reviewer reminded; an administrator can reassign; both are audited | Could |

### Access and reading

| ID | Actor | Scenario | What must be true afterward | Priority |
| --- | --- | --- | --- | --- |
| UC-15 | Contributor | Set who can see a document | Access granted to Entra groups; the change shows in listings and search; the search index is updated and verified by a test | Must |
| UC-16 | Reader | Open a document without access | Access denied; no title or content leaks; the attempt is recorded | Must |
| UC-17 | Reader | Check out a document for reading | Checkout recorded and shown to the reader; it never expires and the reader keeps it until they return it; it does not block other readers | Should |
| UC-18 | System | Expire a reading lease (dropped: reading checkouts do not expire) | No longer applies | Dropped |
| UC-19 | Reader | Return a document early, with optional feedback | Checkout ends; feedback, if given, is saved and visible to the owner | Should |

### Search and questions

UC-22, UC-23 and UC-25 are the leak tests for the evaluation harness.

| ID | Actor | Scenario | What must be true afterward | Priority |
| --- | --- | --- | --- | --- |
| UC-20 | Reader | Search by keyword and metadata | Only documents the reader may see are returned; filters work for category, status and version | Must |
| UC-21 | Reader | Ask a question and get a cited answer | The answer uses only documents the reader may see; each citation links to the source version | Must |
| UC-22 | Reader | Ask a question that touches a restricted document | Nothing from it appears in the answer, including its title or that it exists | Must |
| UC-23 | Reader | Ask a question after their access was removed | Content they lost access to is not used, even if the search index is stale | Must |
| UC-24 | Reader | Ask a question with no supporting document | The system says it cannot find an answer instead of guessing | Should |
| UC-25 | Reader | Try to talk the assistant out of the rules | The request is refused; no restricted text is returned; the attempt is logged | Should |
| UC-26 | Reader | Ask about an unpublished draft | Drafts are excluded from search and Q&A for general readers; owners and reviewers may see them | Should |

### Audit and administration

| ID | Actor | Scenario | What must be true afterward | Priority |
| --- | --- | --- | --- | --- |
| UC-27 | Administrator | View the audit trail for a document | Who did what and when is shown: check-ins, versions, approvals, permission changes and reading leases | Must |
| UC-28 | System | Handle a file that cannot be processed | Marked failed with a reason; the author is notified; retry or replace is possible; other uploads are unaffected | Should |
| UC-29 | Administrator | Clear a stale checkout | Checkout closed with a recorded reason; the owner is notified (was "break a stuck edit lock" until checkouts stopped locking in M1) | Should |
| UC-30 | Administrator | Manage categories and metadata fields | Each category carries default permissions and a default workflow | Should |
| UC-31 | Administrator | Define or change a workflow template | Steps and approver groups configurable; not needed if one fixed template is enough | Could |

### Modern capabilities (proposed)

These reflect where current platforms are heading: [generative and agentic AI lead 2026 content management trends](https://www.techtarget.com/enterprise-software/feature/The-top-6-content-management-trends-in-2026), [retention and legal hold are treated as core governance](https://geosurge.ai/corpus/gen-3665/document-management-system/document-retention-and-legal-hold-workflows-in-modern-document-management-systems.html), and [aging content is a growing concern](https://e-docs.ua/en/?p=1663). All are Should or Could so they do not crowd out the 16 Musts. Delete any you do not want.

| ID | Actor | Scenario | What must be true afterward | Priority |
| --- | --- | --- | --- | --- |
| UC-32 | System | Classify a new document and extract metadata | Category and fields such as parties and dates are suggested with confidence scores; low-confidence results wait for the contributor to confirm; every suggestion is audited | Should |
| UC-33 | System | Detect a duplicate or near-duplicate on check-in | The contributor is warned and shown the match, and can cancel, continue, or check in as a new version of it | Could |
| UC-34 | System | Apply a retention schedule | Retention period set from the category; at expiry the document goes to a disposition review and is archived, never permanently deleted | Should |
| UC-35 | Administrator | Place and release a legal hold | Held documents cannot be deleted or disposed of; retention timers pause; the hold, its reason and its release are audited | Could |
| UC-36 | Contributor | Share a document with an external reviewer | Expiring, revocable access limited to that one document; guest actions audited; the guest never reaches search or Q&A | Should |
| UC-37 | Reviewer | Sign an approval electronically | Signer and time recorded against the exact version; a later edit invalidates it; a real e-signature provider is a stretch | Could |
| UC-38 | System | Flag sensitive data on check-in | Personal or confidential data is detected; the document is restricted and excluded from Q&A until the owner confirms its permissions | Could |
| UC-39 | Administrator | Bulk import legacy documents with metadata mapping | Files, metadata and permissions imported from an export; failures listed with reasons; the import can be re-run safely | Should |
| UC-40 | System | Flag stale content for re-certification | Documents past their review date are flagged; the owner re-certifies, updates or retires them; stale content is marked in search and Q&A | Should |
| UC-41 | Reader | Rate an answer and report a wrong or leaking one | Rating, question, answer and sources stored; leak reports reach an administrator; stored cases can join the evaluation set | Should |
| UC-42 | Reader | Let an AI assistant search on the reader's behalf through MCP | The assistant sees only what that reader can see; each call is audited under the reader's identity; the same leak tests apply | Could |
| UC-43 | Reader | Follow a document and get notified of new versions | Notified when a new version is published or the document is retired; notifications respect current access | Could |
| UC-44 | Reader | Compare two versions | Changes shown side by side; only versions the reader may open can be compared | Could |
| UC-45 | Reader | Request access to a restricted document from a link they already have | The request goes to the owner with a reason; the decision is audited; requesting reveals no restricted content | Could |
| UC-46 | Reader | Send a read-only copy of a document to another user, subject to approval | The sender can only send what they can read, and names the recipient and a reason; the request waits for an approver; on approval the recipient gets a read-only copy of that exact version that cannot be checked out or edited, and nothing else in the repository; a rejection notifies the sender; the request, decision and every access to the copy are audited | Should |

### Decisions on the scenarios

Your answers to the seven open questions, and how they changed the scenarios above.

- **Reading checkout (UC-17):** no expiry; the reader keeps it until they return it. It does not block other readers. UC-18 is dropped because nothing expires.
- **Starting a review (UC-01, UC-09):** check-in starts the review automatically, so there is no separate submit step.
- **After feedback (UC-12, UC-13):** the reviewer chooses to send the document back to the author or let it advance to the next reviewer with the feedback attached. After a send-back, the workflow resumes at the reviewer who returned it (the workflow continues, it does not restart).
- **Version numbers:** drafts take minor numbers (0.1, 0.2); publishing assigns the next major number (1.0).
- **File formats at check-in:** any non-proprietary format for now, such as ODF, PDF, Markdown and plain text. Proprietary formats such as Word are out for the first version.
- **Workflow shape:** the author assigns the reviewers, who approve in the order assigned. Parallel approval is not in scope; predefined workflows are a future improvement.
- **Retention:** no permanent deletion, only archiving (UC-08, UC-34).

**Next step:** redraw the lifecycle, review and data-model diagrams below to match these decisions (automatic review start, send back or advance, minor and major version numbers).

## Workflow and data diagrams

Six diagrams cover the flows complicated enough to need one: the document lifecycle, check out and edit, review and approval, ingestion, permission-aware questions, and the data model. Simple scenarios stay as Given/When/Then. Because the open questions above are still unanswered, the diagrams assume the following, and will be redrawn when you answer:

- Checking in creates a draft, and the author submits it for review (UC-09).
- After feedback the author checks in a new draft version, and the workflow restarts at the first reviewer.
- Reviews run in a fixed sequence of reviewers, one at a time.

### Document lifecycle

&#91;embedded content: document lifecycle · 6 states\]

A new version only becomes Published when its last reviewer approves; at that moment the previous version moves to Archived.

### Check out, edit and check in

&#91;embedded content: check out, edit and check in · 2 decisions\]

Checking out records who is editing and from which version; it does not lock. A check-in based on an out-of-date version is refused, so a second person is told who checked in since instead of overwriting their work. (Changed in M1 from an exclusive lock; the live diagram still shows the lock.)

### Review and approval

&#91;embedded content: review and approval · 3 lanes, 2 decisions\]

Each approval hands the draft to the next reviewer in the sequence; only the last approval publishes it, and any reviewer can send it back to the author.

### Ingestion pipeline

&#91;embedded content: ingestion pipeline · 4 steps, 1 failure path\]

The worker runs outside the request, so a slow or failed extraction never blocks an upload; a failure is recorded against the version.

### Permission-aware question flow

&#91;embedded content: permission-aware question · 6 steps, 2 permission checks\]

The filter in the index is fast but denormalized, so SQL, the source of truth for permissions, re-verifies every hit before any text reaches the model.

### Data model

&#91;embedded content: data model · 9 entities\]

Every arrow runs from parent to child. Chunks are the one entity outside Azure SQL, and each carries a copy of its document's allowed groups, which is why a permission change triggers a re-index.

## Stack

| Layer | Choice | What it demonstrates |
| --- | --- | --- |
| API | Current LTS .NET, C# minimal APIs, EF Core | Modern C#, clean architecture |
| Front end | React with TypeScript (hooks, TanStack Query) | Current framework patterns |
| Data | Azure SQL (metadata, ACLs, audit) plus Blob Storage | Schema design, indexing, versioning |
| Search and AI | Azure AI Search with vector search, plus Azure OpenAI (or Foundry) | RAG, embeddings, security-trimmed retrieval |
| Ingestion | Service Bus (Basic) and a worker in Container Apps | Event-driven design |
| Identity | Microsoft Entra ID (OIDC, roles, groups) | Modern auth |
| Hosting | Azure Container Apps | Containers without the Kubernetes overhead |
| Infrastructure | Terraform (Bicep as a short side exercise) | Infrastructure as code |
| Delivery | GitHub Actions with OIDC to Azure, environments, a deployment gate | CI/CD |
| Operations | OpenTelemetry, Application Insights, Key Vault, budget alerts | Observability, secrets, cost awareness |

## Tools and free-tier plan

Status below is as of November 17, 2026, with prices from the Azure Retail Prices API (East US). Free-tier limits change, so check each on its pricing page before relying on it. The subscription is a Free Trial, so the 12-month offers (such as Container Registry) apply.

| Azure service | Role in the project | Free-tier status | Watch for |
| --- | --- | --- | --- |
| Azure SQL Database (free offer) | Metadata, versions, permissions, audit | Confirmed: 100,000 vCore seconds, 32 GB data and 32 GB backup per month, for the life of the subscription | Set auto-pause when the monthly limit is reached |
| Azure Container Apps | Hosts the .NET API, React app and ingestion worker | Confirmed: first 180,000 vCPU-seconds, 360,000 GiB-seconds and 2 million requests per subscription per month | Scale to zero when idle |
| Azure AI Search, Free tier | Full-text and vector search over document chunks | Confirmed: 50 MB storage, 3 indexes, one free service per subscription | Sandbox only and may be deleted after long inactivity; test whether vectors fit in 50 MB; plan a paid tier for weeks 9 to 10 and demos, then destroy it |
| Azure Container Registry | Stores container images | Confirmed free for the first 12 months on a new account (1 Standard registry, 100 GB) | Alternative: GitHub Container Registry |
| Azure OpenAI or Foundry models | Embeddings and answer generation | Not free; embeddings are cheap (about $0.02 per million tokens), answer generation is the main variable cost | Confirm the current embedding model, since one tracker flags text-embedding-3-small for deprecation on Azure |
| Microsoft Entra ID | Sign-in, app registrations, groups for permissions | Free tier covers this use (checked 2026-11-17); its quotas do not bind at this scale | Plan for users in many groups; Free tier keeps sign-in logs only briefly |
| Blob Storage | Original files and extracted text | Pay as you go: $0.0208 per GB-month (Hot LRS, East US), pennies at this size | Also holds Terraform state |
| Key Vault | Secrets and keys | Pay per operation, about $0.03 per 10,000 (Standard; rate not confirmed in the price API) | Prefer managed identity so few secrets exist |
| Application Insights and Log Analytics | Logs, traces, metrics via OpenTelemetry | Confirmed: first 5 GB of ingestion a month free per billing account, then $2.30 per GB | Set a daily cap |
| Service Bus, Basic tier | Ingestion queue: decouples upload from indexing | Confirmed: $0.05 per million operations, no base charge; built-in retries and dead-letter queue | Decided 2026-11-17 over Storage Queue (see the decision log) |
| Azure Functions | Not used | Not needed: the worker runs in Container Apps | Dropped 2026-11-17 |

One account rule matters: after the 30-day, $200 credit period you must move to pay-as-you-go to keep the free monthly amounts, so set a budget alert before you do.

### Delivery, development and AI tooling

These tools sit outside Azure's free tier. Most are free; the one exception is Claude Code, which needs a Claude plan.

| Tool | Role in the project | Cost |
| --- | --- | --- |
| GitHub repo, Issues and Projects | Source control, planning board, roadmap | Free |
| GitHub Actions | CI/CD with OIDC sign-in to Azure | Free for public repos (confirm minutes if the repo goes private) |
| Terraform and the azurerm provider | Infrastructure as code for all Azure resources | Free |
| Azure CLI | Sign-in, quick checks, scripts | Free |
| Azure MCP Server (read-only first) | Lets Claude inspect Azure resources | Free; uses your own Azure sign-in and RBAC |
| Claude Code | Planning, code, tests, reviews | Requires a Claude plan |
| .NET SDK, Node.js, VS Code | Build and run the API and React app locally | Free |
| Docker | Build and test container images | Free for personal use; check licensing if used at work |
| xUnit, Vitest, Playwright | API, front-end and end-to-end tests | Free |
| OpenTelemetry | Vendor-neutral traces and metrics | Free |

What is not free, in one line: model calls (embeddings and answers), the paid AI Search tier during demo weeks, and anything left running after the free monthly amounts are used.

## Architecture diagram

The diagram shows where each component lives: the React app, API and ingestion worker in Container Apps, the data and AI services beside them in one resource group, and Entra ID and GitHub outside it.

&#91;embedded content: deployment architecture · where each component lives\]

The paths below are the planned design, to be tested during the architecture work.

- **Upload path:** the API stores the file in Blob Storage and its metadata in SQL, then queues an event. The worker extracts text, creates embeddings, and writes chunks with their allowed groups to AI Search.
- **Question path:** the API checks the caller's groups, searches with a group filter, re-checks permissions in SQL, and sends only permitted text to the model.

## Architecture planning

Spend weeks 1 and 2 designing before coding. Keep each deliverable short, since the goal is clear decisions, not paperwork.

| Deliverable | Question it answers | Target |
| --- | --- | --- |
| Requirements and non-functional targets | What must it do, for how many users and documents, at what cost, latency and security level? | Week 1 |
| Context and container diagrams | What are the parts, who and what talks to them? | Week 1 |
| Data model | How are documents, versions, metadata, permissions and audit records stored? | Week 2 |
| Permission model and security-trimming design | How is access decided, and how does retrieval guarantee the model never sees restricted content? | Week 2 |
| Ingestion and indexing flow | What happens from upload to searchable, and what happens when a document or its permissions change? | Week 2, refined week 7 |
| Authentication and authorization flow | How do the React app, the API and Azure services trust each other? | Week 2 |
| Threat model (one page) | What can go wrong, and what is the mitigation for each risk? | Week 2, revisited week 11 |
| Cost estimate and budget | What does it cost to run, idle and in use? | Week 2 |
| Decision records | Why was each major choice made, and what was rejected? | Ongoing, first set by week 2 |

### Decisions to settle first

These choices shape everything else. The leans below are starting hypotheses to test in planning, not conclusions.

| Decision | Options | Starting lean |
| --- | --- | --- |
| Application shape | Modular monolith, or several services | Modular monolith API plus a separate ingestion worker: fewer moving parts, and a clear story for how it would split at scale |
| Source of truth for permissions | Azure SQL, the search index, or Entra alone | Azure SQL holds the access rules; the search index carries a denormalized copy of allowed users and groups per chunk |
| Preventing leaks at retrieval | Index filter only, or filter plus re-check | Filter in the index by the caller's groups, then re-verify against SQL before anything reaches the model |
| Permission changes | Event-driven re-index, or check at query time | Both: re-index on change, and always re-verify at query time so a stale index never leaks |
| Versioning | Immutable versions with a current pointer, or in-place edits | Immutable versions plus a current-version pointer, with optimistic concurrency instead of locks |
| Authentication | SPA sign-in flow, API token validation, service-to-service identity | React app signs in through Entra with PKCE, the API validates tokens, and Azure services are reached by managed identity; plan for users in many groups, since tokens can overflow their group list |
| Chunking and embeddings | Fixed-size or structure-aware chunks; which embedding model | Start simple, then choose by measurement in the evaluation harness |
| Tenancy | Single tenant or multi-tenant | Single tenant to keep scope small; write down what would change for multi-tenant |
| Environments | One or several | Two: a dev environment and a demo environment, both from the same Terraform modules |

### How to run the planning

Draft each design yourself first, then use Claude Code to challenge it. This keeps the thinking yours and the review thorough.

1. **Write the draft unaided.** Sketch the diagram and write a few lines on each decision before opening Claude Code.
2. **Review in plan mode.** Ask Claude to critique the design as a skeptical senior reviewer: what fails, what is missing, what is simpler. Plan mode is read-only, so nothing in the repo changes while you discuss.
3. **Record each decision.** Save one short file per decision in the repo (for example docs/decisions/), using the template below. Update a record when the decision changes rather than deleting it.
4. **Keep diagrams as text.** Use Mermaid in markdown so diagrams live in the repo, show in the README, and change in the same commit as the code.
5. **Walk through it out loud.** Explain the design in 10 minutes, then let Claude ask follow-up questions.

**Decision record template**

- **Title:** the decision in one line
- **Context:** the problem and constraints
- **Options considered:** at least two, with the main tradeoff of each
- **Decision:** what you chose and why
- **Consequences:** what gets easier, what gets harder, and what would make you revisit it

**Done when (end of week 2)**

- A short design document and the diagrams are committed to the repo
- The first 6 to 8 decision records are written
- The SQL schema and Terraform skeleton match the design
- You can explain the whole design aloud in 10 minutes without notes

Revisit the design briefly at weeks 6 and 10, and update the records for anything that changed.

## Showcase features

Five features make the project worth showing; the permission-aware Q&A is the one to protect if time runs short.

1. Document versioning, metadata, check-out and audit trail
2. Per-document permissions mapped to Entra groups
3. Q&A that filters results by the asker's permissions before the model sees them
4. A small evaluation harness that scores answer quality and checks that restricted documents never leak
5. A README with an architecture diagram, a cost note, and a "what I'd do differently at scale" section

**Stretch goals, only if time allows:**

- An MCP server exposing document search to AI tools
- A minimal AWS deployment of one component, to show portability

## 12-week build plan

The build runs in six two-week blocks at 30 to 90 minutes a day, ending with a public repo and a 3-minute demo.

| Weeks | Focus | Milestone |
| --- | --- | --- |
| 1-2 | Repo setup, SQL schema, Terraform skeleton | Empty but deployable "hello world" through CI/CD |
| 3-4 | .NET API: upload, metadata, versioning, Entra auth | Authenticated API running in Container Apps |
| 5-6 | React front end: login, upload, browse, document detail | Usable UI end to end |
| 7-8 | Ingestion pipeline, text extraction, embeddings, AI Search index | Documents become searchable |
| 9-10 | Permission-aware Q&A, evaluation harness, observability | The core showcase feature working |
| 11-12 | Hardening, README and diagram, demo recording | Public repo plus a 3-minute demo |

## Practical notes

- **Cost:** use Azure's free tier and credits where possible, scale Container Apps to zero, and set a budget alert on day one. Spend stays small if you tear down AI Search between sessions.
- **Scope discipline:** a finished thin version beats an ambitious stalled one. If you're behind by week 8, cut the front-end polish, not the permission-aware retrieval.
- **Public repo:** use synthetic or public-domain documents only, and don't reuse anything from your employer.

## Decisions to make

Both are settled; later decisions live in `docs/build/decision-log.md`.

- [x] **Front-end framework:** React. It has the largest market (85% usage in State of JS 2025).
- [x] **Public GitHub repo:** yes, with a clear README and synthetic or public-domain documents only.
