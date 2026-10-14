# Design questions from GB-1

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Lane `lane/1-data-sim`. No contract file was edited. None of these questions block D1–D6. The lane shipped against the contracts as they are.

## 1. Tool time windows vs the mock OpenAPI

**Context.** `IMesTool`, `IQmsQueryTool`, `IPlmTool.GetEcosAsync`, `IErpTool`, and `ICmmsTool.GetWorkOrdersAsync` all take `from` and `until`. `contracts/mock-enterprise.openapi.yaml` puts those parameters only on `GET /mes/runs`. The other GETs take `toolId`, and `GET /erp/notices` takes an optional `materialCode`.

The connector still honors the tool window where the record has a timestamp. It filters client-side on NCR `openedAt`, ECO `effectiveAt`, material `receivedAt`, notice `issuedAt`, and work-order `performedAt`. `GET /mes/runs` passes `from` and `until` to the API.

`LotReference` has no timestamp. `GetLotsAsync` cannot drop lots outside the window. `MockEnterpriseClientTests.PortsReadFaultEvidenceAndFilterOnTheRecordTimestamp` locks that `GetLotsAsync` for E-02 with a zero-width window at `Now` still returns the tool's lots, including NF-3317.

**Options.**

1. Leave the OpenAPI as written. Keep the client-side filters. Lot queries return every lot for the tool.
2. Add `from` and `until` to the mock GETs whose records already have timestamps, and filter on the server. Lots stay unfiltered until the domain record changes.
3. Add a timestamp to `LotReference` and a window to `GET /mes/lots`. That is a domain and OpenAPI change.

**Recommendation.** Option 1 for the demo. Option 3 only if an agent must not see lots outside the investigation window.

**Blocking?** No.

## 2. Injected timeout duration

**Context.** `X-Mock-Fault: timeout` is in the OpenAPI enum for writes. The wait length is not. The mock reads `Mock:TimeoutSeconds`, defaults to 30, and the tests set 2. It delays, then returns 504 if the caller is still connected. The connector test uses an `HttpClient` timeout of 500 ms and expects `TaskCanceledException`.

**Options.**

1. Keep the delay as a host setting. The OpenAPI continues to name the fault and not the duration.
2. Add an optional header or query for the delay.

**Recommendation.** Option 1. A pitch demo does not need callers to choose the wait.

**Blocking?** No.

## 3. No CAPA read API

**Context.** `POST /qms/capas` creates a CAPA and returns `externalId`. There is no GET. `IQmsQueryTool` returns NCRs. The seed's earlier quality history is three NCRs (`NCR-C-01-104`, `NCR-D-02-077`, `NCR-S-01-052`). A created CAPA stays in the mock process and is not readable later.

**Options.**

1. Keep NCRs as the readable history.
2. Add `GET /qms/capas` and a domain record for a stored CAPA.

**Recommendation.** Option 1 unless the investigation agent must cite a prior CAPA id. The write path already returns `QMS-{guid}` and `TKT-` numbers.

**Blocking?** No.

## Noted, not a contract change

`GET /plm/config` returns one `ConfigRevision`. The seed keeps one current revision per tool. The D-01 contradiction is that current PLM revision C against MES recipe revision B on the run header. Historical config revisions were not required.

`DocumentHit` has no score. Hybrid search orders hits and drops the rest. The embedding cache is `mps-hash-v1` (256 dimensions, no API key). Replacing it with a hosted model would rebuild `data/index/embeddings.json` and would not change the `DocumentHit` contract.

The console fleet stream remains SignalR at `/hubs/fleet` in `contracts/openapi.yaml`. This lane publishes an in-process channel. GB-3 owns the hub. That is an integration note in `LANE_REPORT.md`, not a contract edit.

ROI unit prices are lane assumptions in `docs/roi-assumptions.md`. They are not domain fields.
