# Physical architecture (hosted demo)

**Issue:** G2 (#47)
**Copyright (c) 2026 Bryan C. Weis and Common Consulting Company**

Hosted demo in Bryan’s Azure subscription, resource group `rg-mps-demo`. Local run is `docker compose` (API, mock enterprise, web, Aspire dashboard) and is **not** this diagram. Bicep is authored in the foundation lane; this file does not deploy anything.

Invite-only. No secrets in Git. Cost-minimal SKUs, scale-to-zero, teardown = delete the resource group after the review.

## Diagram

```mermaid
flowchart TB
  subgraph Plants["Simulated plants, not regions"]
    direction LR
    NW["Northwater Fab"]
    IW["Ironwood Fab"]
    LM["Lumen Pilot Works"]
  end

  subgraph Public["Public edge"]
    REV["Invited reviewer"]
    GH["GitHub Actions"]
  end

  subgraph IdP["Sign-in tenant"]
    APP["App registration"]
    GUESTS["Invited guests only"]
  end

  REV -->|sign-in| APP
  APP --> GUESTS

  subgraph RG["rg-mps-demo"]
    subgraph Edge["Static Web App"]
      HRN["Harness bar"]
      CON["Meridian console"]
    end

    subgraph Net["Container Apps"]
      APIc["API"]
      SITES["Sites API"]
      EGATE["Escalation gate"]
      WRK["Investigation worker"]
      SIMc["Simulator, one replica"]
      MOCK["Mock enterprise"]
      EVD["Scenario evidence"]
    end

    Q["Storage queue"]
    SQL[("Serverless SQL")]
    SRCH[("Search index")]
    AOAI["Azure OpenAI"]
    KV["Key Vault"]
    MI["Managed identity"]
    AI["Traces"]
    BUD["Spend cap"]
    ACR["Container Registry"]
  end

  GUESTS --> HRN
  GUESTS --> CON
  CON -->|token| APIc
  NW --> SIMc
  IW --> SIMc
  LM --> SIMc
  APIc --> SITES
  APIc --> EGATE
  APIc --> Q
  Q --> WRK
  SIMc --> APIc
  WRK --> AOAI
  WRK --> SRCH
  WRK --> SQL
  WRK --> MOCK
  MOCK --> EVD
  APIc --> SQL
  APIc --> MOCK

  MI --> KV
  MI --> AOAI
  MI --> SQL
  MI --> SRCH
  APIc --> AI
  WRK --> AI
  CON --> AI
  BUD --> AI

  GH -->|manual deploy| ACR
  GH --> APIc
  GH --> HRN
```

## Plants and the two screen layers

The three plants are simulated. They are not Azure regions and not separate resource groups. Northwater Fab and Ironwood Fab are front-end fabs. Lumen Pilot Works is strip, clean, and a pilot line. One simulator replica streams all three from one seed. The API serves `GET /api/sites`, `GET /api/sites/{siteId}`, and `GET /api/sites/compare`. Scenario SC-5 is one supplier lot used at Northwater and at Ironwood.

The Static Web App draws two layers. The harness bar is the presenter strip: fault injector, return on investment, metrics, reset, the scenario picker, live / replay / mock, and spend. The Meridian console is the product: Sites, Fleet, Tool, Alerts, Tickets, Investigation, Approval, and corrective and preventive action. A harness panel is not a console tab.

Scripted scenarios carry their own evidence on the mock read path. The resolution agent sees the full record text. An escalated investigation cannot be approved straight into a corrective and preventive action. The API refuses that approval. The approver requests more evidence, or overrides with a written reason.

## Identity, secrets, network

| Control | Demo behavior |
|---|---|
| **Entra ID** | Single-tenant app registration, **user assignment required**. Reviewers are B2B guests. Uninvited accounts never receive a token. Local profile uses a dev user, not Entra. |
| **Managed identity** | Container Apps (and the SWA API link) use a user-assigned identity. Azure OpenAI, SQL, Key Vault, and Search are granted via RBAC. **No access keys in images or App Settings that a PR can print.** |
| **Key Vault** | Holds the few remaining secrets (if any). Apps read through managed identity. Repo has `.env.example` only; gitleaks in CI. |
| **Network boundary** | Demo: Container Apps *environment* is the trust boundary; ingress to `api` is Entra-authenticated. Production target (talk track, not built): Front Door + WAF, private endpoints to SQL/AOAI/KV, Event Hubs for telemetry, APIM in front of the model gateway. |
| **GitHub OIDC** | `deploy.yml` federates to the subscription on repo `ccc-bcweis/mps-quality-agents`, environment `demo`, **required reviewer**. No stored Azure credentials. CI (`ci.yml`) never deploys. Bryan’s go is the gate for Z1/Z2. |

## Scaling, latency, unit cost

| Item | Target |
|---|---|
| API p95 | **&lt; 300 ms**. Investigations are async (queue + worker); the HTTP call returns an investigation id. |
| Investigation p95 | **&lt; 45 s** wall time (tool calls + two model completions + gates). |
| API replicas | Scale **0–3** on HTTP. Worker scales on queue depth. Simulator **1 replica**. |
| SQL | Serverless, auto-pause. Fine for demo concurrency; not a production HA pattern. |
| Azure OpenAI | gpt-5.4-mini (managed identity) for both investigation and resolution. TPM quota is a backstop; the gateway $2/day cap is the primary brake. |
| **Unit cost** | **≈ $0.02–0.10 per successful investigation** (tokens + tiny compute). Measure on the metrics screen; do not quote as a contract price. |
| Spend cap | Gateway **hard stop at $2/day** (SQL ledger). Over cap → UI banner **REPLAY MODE**, no live calls. Azure budget alert (~$10/week) and AOAI quota sit behind that. |
| Rollback | Container Apps revisions (blue/green). Prompt/model versions are first-class so an AI regression rolls back without a full app rollback. DR for the demo = IaC redeploy + SQL geo-backup (not configured until Z2). |

## CI → deploy path

1. PR: build, analyzers, tests, coverage, format, eval in replay, CodeQL, gitleaks, dependency review.
2. Merge to `main` (Bob, then Bryan for lane merges).
3. `deploy.yml` is **manual / environment-gated**. OIDC mints an Azure token, Bicep applies `rg-mps-demo`, ACR gets the image, Container Apps revision and SWA production token update.
4. Smoke: Entra sign-in as an invited guest, LIVE badge, spend meter, S1 in replay then live.

Azure OpenAI runs gpt-5.4-mini in eastus2 behind the gateway cap (gpt-4o-mini was blocked for new deployments).

## Large-format print

ARCH D sheet (36 × 24 in, landscape), v1.1, 2026-10-14. The mermaid block above is the source. The plotter files are hand-composed so labels do not cross. v1.1 adds the three simulated plants, the Sites routes, the harness-over-console split, scenario evidence on the mock read, and the refusal of a bare approve. The print sheet labels the GitHub environment `azure-demo`. The identity table in this file still says `demo`. See [`cicd.md`](cicd.md).

- [`print/physical-archD.pdf`](print/physical-archD.pdf)
- [`print/physical-archD.svg`](print/physical-archD.svg)
- [`print/physical.svg`](print/physical.svg)

Regenerate with `pwsh docs/diagrams/render.ps1`. See [`README.md`](README.md).
