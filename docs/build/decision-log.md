# Decision log

One line per decision, with its reason. Add new decisions at the bottom of the right section and never edit history; supersede with a new line.

## Settled (as of 2026-11-11)

**Stack**
- Front end: React with TypeScript and TanStack Query. Chosen for market size.
- Back end: .NET (current LTS), C# minimal APIs, EF Core. Azure SQL, Blob Storage, Azure AI Search, Azure OpenAI, Entra ID, Container Apps, Terraform, GitHub Actions with OIDC, OpenTelemetry with Application Insights, Key Vault.

**Architecture**
- Modular monolith API plus a separate ingestion worker.
- Azure SQL is the source of truth for permissions.
- Permissions are copied into the search index as allowed groups on every chunk. Search filters on them, then SQL re-verifies before any text reaches the model.
- Versions are immutable, with a pointer to the current version.
- Managed identity for service-to-service access.
- Two environments, dev and demo, from the same Terraform.

**Behaviour (from the scenario questions)**
- Check-in starts the review automatically. There is no separate submit step.
- The author assigns the reviewers, who approve in the order assigned. Parallel approval is out of scope; predefined workflows are a future improvement.
- A reviewer can send the document back to the author or let it advance to the next reviewer with feedback attached. After a send-back, the workflow continues at that reviewer; it does not restart.
- Draft versions take minor numbers (0.1, 0.2). Publishing assigns the next major number (1.0).
- File formats: any non-proprietary format for now (for example ODF, PDF, Markdown, plain text). Proprietary formats such as Word are out of the first version.
- No permanent deletion, only archiving.
- Reading checkout has no expiry and does not block other readers. UC-18 is dropped.
- Scope stays whole. If time runs long, the schedule stretches.

**M0 decisions (2026-11-17)**
- Ingestion queue: Service Bus, Basic tier. $0.05 per million operations against $0.40 per million for Storage Queue (Azure Retail Prices API, East US), and it has max-delivery retries and a dead-letter queue built in, where Storage Queue would need hand-written poison-queue handling.
- Azure Functions are not used; the ingestion worker runs in Container Apps.
- The GitHub repo is public: free branch protection and unlimited Actions minutes. Before going public, personal notes were removed from the docs and the git history was replaced with one clean commit. The repo holds engineering material only.
- Local Docker is skipped; container images build in CI.
- The AI Search free-tier vector test moves to M3, the first milestone that indexes anything. Until it passes, M3 assumes the Basic tier may be needed (about $74 a month if left running, so create it only for test and demo sessions).

**Cost estimate, dev environment, per month (2026-11-17)**

| Service | Expected cost | Basis |
|---|---|---|
| Azure SQL (free offer), Container Apps (scale to zero) | $0 | Within the monthly free grants in the plan doc |
| Container Registry, Basic | $0 for 12 months, then about $5 | 12-month offer applies (Free Trial account); $0.1666 a day afterwards |
| Service Bus Basic, Blob Storage, Key Vault | Under $1 | Per-operation and per-GB rates above and in the plan doc |
| Application Insights and Log Analytics | $0 with a daily cap | First 5 GB a month free, then $2.30 per GB |
| Entra ID Free | $0 | Free tier covers app registrations and test groups |
| AI Search | $0 on Free; about $0.10 an hour on Basic | Basic only while testing or demoing, then destroyed |
| Azure OpenAI | Usage only | Embeddings about $0.02 per million tokens; answers are the main variable cost |

The subscription is a Free Trial with a spending limit: $200 of credit for 30 days. To keep the free monthly amounts after that, upgrade to pay-as-you-go, with the budget alert set first.

## Open

| Question | Needed by | Notes |
|---|---|---|
| Does the AI Search free tier hold the vector index we need | M3 | Moved from M0 on 2026-11-17; decides whether M3 stays free |
| Edit lock (UC-02) or optimistic concurrency | M1 | The plan doc's versioning lean says optimistic concurrency instead of locks, but UC-02 requires an exclusive edit lock |
| Free Trial end date | Before the trial ends | Check it in the portal and upgrade to pay-as-you-go before then |
| UC-46 details: who approves, does the copy expire, can the recipient be outside the system | M7 | Assumed: owner or designated approver, no stated expiry, undecided |
| Restart point of a send-back when several reviewers have already approved | M5 | Current answer: continue at the reviewer who sent it back |

## Changelog

- 2026-11-11: log created from the plan doc and the scenario decisions.
- 2026-11-17: moved into the repo under `docs/build/`; the repo is now the working copy. The Doc Mgt Project pages are a read-only mirror.
- 2026-11-17: M0 decisions logged (queue, public repo, Functions, Docker, AI Search test timing) with a cost estimate. Personal positioning notes removed from this log and the other docs before the repo went public; the queue, public repo and free-tier questions closed; two new open questions added.
