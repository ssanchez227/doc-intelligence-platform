# M8 Hardening and demo

**Goal:** finish the build as a secure, documented, cost-controlled system with a demo.

## Scope

- Security pass: authorization on every endpoint, dependency and secret scanning, least-privilege identities, rate limits on Q&A.
- Reliability: retries and dead-letter handling, backups, a runbook for the common failures.
- Cost: confirm the monthly spend, tear down what can be torn down, keep the budget alert.
- Documentation: README with the architecture diagram and the scenario catalogue, a short "how permission-aware retrieval works" write-up, the evaluation results.
- Demo: a 3-minute recording, plus a longer walkthrough if useful.
- The repo is public (decided in M0): confirm it holds only synthetic or public-domain documents and nothing from an employer.
- Reconcile the plan doc with what was built; archive anything out of date.

## Done checklist

- [ ] All scenarios UC-01 to UC-46 (except UC-18) pass in CI
- [ ] The evaluation report shows zero leaks across the test set
- [ ] README, diagram and demo are published
- [ ] The decision log is complete and the plan doc matches reality
- [ ] A cost summary and teardown steps are written
- [ ] Session log written and the index updated
