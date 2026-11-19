# Showcase projects

Six small products for **Harbourline Lending** (fictional), a lender to commercial property owners. Each one is run through the Baton model, uses invented data only, and is ready to hand over.

| # | Repo | The problem | Traits given to the advisor | Shape it suggested | Stack |
|---|---|---|---|---|---|
| 1 | `termsheet-reader` | Analysts copy loan terms out of scanned term sheets and facility letters by hand | DocumentIngestion, RegulatoryAudit, ComplexBusinessRules, SmallTeam | Clean Architecture + queue-fed worker + review queue | .NET 8, React, Postgres, local or hosted model |
| 2 | `deal-triage` | The inbound application queue is first-come-first-served, so strong deals wait | ComplexBusinessRules, DocumentIngestion, SmallTeam | Clean Architecture rules engine + model-written reasons | .NET 8, Angular |
| 3 | `lp-hub` | Fund investors get statements by email attachment and nobody can say who opened what | MultiTenant, RegulatoryAudit, PersonalData, ReadHeavy, SeveralBusinessAreas, SmallTeam | Modular monolith with tenant isolation tests and an access log | .NET 8, Angular, Postgres |
| 4 | `loan-ledger` | Drawdowns, interest and repayments live in spreadsheets | MoneyMovement, RegulatoryAudit, LongRunningWorkflows, SeveralBusinessAreas, ComplexBusinessRules, SmallTeam | Modular monolith, append-only ledger, idempotency keys, outbox, sagas | .NET 8, React, Postgres |
| 5 | `rent-roll-pipeline` | Borrowers send rent rolls in a dozen layouts; the portfolio view is rebuilt by hand each month | BatchDataFeeds, ReadHeavy, ExternalPartnerIntegrations, RegulatoryAudit, SmallTeam | Import job per layout → warehouse → dashboard, with lineage and reconciliation | .NET Worker, Postgres/DuckDB |
| 6 | `legacy-bridge` | Servicing runs on an ageing terminal app only two people understand | LegacySystems, SeveralBusinessAreas, ExternalPartnerIntegrations, SmallTeam | Strangler fig routing layer + adapter + new modular monolith | .NET 8 + YARP, contract tests |

Every repo gets the full agent setup and its `.agentic/` baton **before** the first feature, and the commit history shows each leg.

## Consumer-scale showcases

Six original consumer products for invented clients. Each one shows the same judgement call: the advisor's **global-scale** answer next to the **launch** answer the team actually builds, with the evolution trigger written into ADR-0001.

| Repo | What it is | Invented client | Launch → global | Slice 1 |
|---|---|---|---|---|
| `framely` | Photo-sharing social app | Lumen Collective (photographers) | Modular monolith → microservices + CDN (ref #19) | Post a photo; followers see it first (fan-out on write, EXIF location stripped) |
| `plaza` | Community social network | Buurt Forum (neighbourhood associations) | Modular monolith → microservices (ref #20) | Group notice with live notifications and audited moderation |
| `relay` | Private messaging | Kalahari Mobile (mobile network) | Modular monolith → microservices for live connections (ref #14) | Real-time chat with ticks, offline queue and idempotent sends |
| `cinewave` | Video streaming | Sahel Pictures (African cinema) | Modular monolith → microservices + event stream (ref #5) | Territory-filtered catalogue, HLS playback, resume on any device |
| `wavelength` | Music streaming | Mzansi Indie Alliance (labels) | Modular monolith → microservices (ref #5) | Search, play, queue; royalty plays counted once after 30 s |
| `huddle` | Digital banking for savings groups | Thrive Mutual (cooperative) | Modular monolith at both scales (ref #9) | Group contributions on a double-entry ledger with idempotency keys |
