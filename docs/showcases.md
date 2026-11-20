# Showcase projects

## Lending showcases: one domain, six deliberately different approaches

Six products for **Harbourline Lending** (fictional), a lender to commercial property owners. Each one solves a different problem with a different architecture, so the set shows range instead of one template repeated.

| Repo | Problem | Approach demonstrated | Frontend | Building blocks |
|---|---|---|---|---|
| `clio` | Analysts copy loan terms out of scanned term sheets | Clean Architecture; extraction worker on a channel; pluggable model port; eval set with an accuracy gate | React | Core, Web, Auth, Audit, Outbox |
| `metis` | Strong deals wait behind weak ones in the inbound queue | Vertical slices; explainable rules engine; weights reload live without a deploy | Angular | Core, Web, Auth |
| `hestia` | Investors get statements by email and nobody knows who opened what | Multi-tenant modular monolith; central tenant filter with reflection tests; audited downloads | Blazor (SSR) | Core, Web, Auth, Audit, Notifications, Outbox |
| `chronos` | Drawdowns, interest and repayments live in spreadsheets | Event sourcing + CQRS; projections; balance as of any past date; event upcasting | React | Core, Web, Idempotency, Outbox, Audit |
| `demeter` | Rent rolls arrive in a dozen layouts and rarely reconcile | Python + DuckDB pipeline; data contracts; quarantine on mismatch; lineage to file and line | Vanilla TypeScript | (Python, on purpose) |
| `janus` | Servicing runs on an ageing terminal app | Strangler fig; hand-rolled proxy with shadow mode; contract tests; retries and circuit breaker | React | Core, Web |

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
