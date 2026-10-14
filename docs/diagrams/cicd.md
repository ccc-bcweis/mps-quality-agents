# CI/CD and promotion

**Issue:** G3 (#48)
**Copyright (c) 2026 Bryan C. Weis and Common Consulting Company**

How a change reaches the Meridian Precision Systems demo. Parallel Grok Build lanes open pull requests. CI must pass. Bob reviews. Merge to `main` does not deploy. `deploy.yml` is a manual, environment-gated workflow. Smoke runs after deploy.

Logical view: [`logical.md`](logical.md). Physical view: [`physical.md`](physical.md).

## Diagram

```mermaid
flowchart LR
  subgraph Lanes["Grok Build lanes - parallel PRs"]
    direction TB
    GBF["GB-F Foundation - lane/f-foundation - P0"]
    GBD["GB-D Design - lane/d-design - P0"]
    GB1["GB-1 Data and simulation - lane/1-data-sim"]
    GB2["GB-2 Agents - lane/2-agents"]
    GB3["GB-3 API - lane/3-api"]
    GB4["GB-4 UI - lane/4-ui"]
    GB5["GB-5 QA, eval, security - lane/5-qa"]
  end

  PR["Pull request"]

  subgraph CI["ci.yml - required checks"]
    direction TB
    BUILD["Build - warnings as errors, analyzers"]
    TEST["Test - xUnit, Vitest, ESLint, tsc"]
    COV["Coverage - 70 percent Domain and Application"]
    FMT["Format - dotnet format verify"]
    CQ["CodeQL - C# and JavaScript"]
    GL["gitleaks - secret scan"]
    DEP["Dependency review"]
    BICEP["Bicep build - compile only, no deploy"]
  end

  subgraph Later["After I1 - not on day-1 PRs"]
    direction TB
    EVAL["Eval replay - hard gates"]
    RED["Red-team and ZAP baseline"]
  end

  BOB["Required review - Bob"]
  BRY["Bryan - lane merges and Azure go"]
  MAIN["Merge to main"]

  subgraph DEPLOY["deploy.yml - manual, gated"]
    direction TB
    MAN["workflow_dispatch only"]
    ENV["Environment azure-demo"]
    REVU["Required reviewer"]
    OIDC["OIDC to Azure - no stored secrets"]
    PUSH["Build and push containers"]
    WHAT["Bicep what-if"]
    APPLY["Deploy rg-mps-demo"]
  end

  SMOKE["Smoke - invited sign-in, LIVE badge, S1"]

  GBF --> PR
  GBD --> PR
  GB1 --> PR
  GB2 --> PR
  GB3 --> PR
  GB4 --> PR
  GB5 --> PR
  PR --> BUILD
  PR --> TEST
  PR --> COV
  PR --> FMT
  PR --> CQ
  PR --> GL
  PR --> DEP
  PR --> BICEP
  BUILD --> BOB
  TEST --> BOB
  COV --> BOB
  FMT --> BOB
  CQ --> BOB
  GL --> BOB
  DEP --> BOB
  BICEP --> BOB
  EVAL -.-> BOB
  RED -.-> BOB
  BOB --> BRY
  BRY --> MAIN
  MAIN --> MAN
  MAN --> ENV --> REVU --> OIDC --> PUSH --> WHAT --> APPLY --> SMOKE
```

## Gates

| Stage | What blocks progress | What it must not do |
|---|---|---|
| Lanes | Each lane is a branch and a PR. P0 (GB-F, with GB-D) lands before the P1 lanes build on frozen contracts. At most five Grok Build sessions at once. | Agents do not call git remotes. Bob syncs with `sync_to_github.ps1`. |
| `ci.yml` | Build (warnings as errors, analyzers), test (xUnit, Vitest, ESLint, tsc), coverage at or above 70% on Domain and Application, `dotnet format --verify-no-changes`, CodeQL (C# and JavaScript), gitleaks, dependency review, Bicep build. | CI does not deploy and does not receive Azure credentials. |
| Later checks | Eval in replay mode with hard gates, promptfoo red-team, and the OWASP ZAP baseline. They arrive with integration (I1) and SEC1, then stay on PRs. | A day-1 scaffold PR is not failed for an eval suite that does not exist yet. |
| Review | Bob is the required reviewer. SPEC also has Bryan approve lane merges. Branch protection on `main`: PR, green CI, review. | A lane does not merge itself. |
| `main` | Integration branch. Still no Azure changes. | A push to `main` does not run a deploy. |
| `deploy.yml` | Manual `workflow_dispatch`. GitHub environment `azure-demo` with a required reviewer. OIDC federation, no stored cloud secrets. Build and push images, Bicep what-if, then deploy `rg-mps-demo`. | It does not run on an unreviewed PR, and it does not use a client secret in the repo. |
| Smoke | Invited guest can sign in. LIVE AI badge and spend meter show. Scenario S1 runs in replay, then live. An uninvited account is blocked. | Smoke is not a substitute for the eval hard gates. |

The operator brief for this sheet names the GitHub environment `azure-demo`. PLAN.md §10 and [`physical.md`](physical.md) call that environment `demo`. This diagram uses `azure-demo` until Bob confirms the name on the federated credential.

## Large-format print

ARCH D sheet (36 × 24 in, landscape), v1.0, 2026-10-14:

- [`print/cicd-archD.pdf`](print/cicd-archD.pdf)
- [`print/cicd-archD.svg`](print/cicd-archD.svg)
- [`print/cicd.svg`](print/cicd.svg)

Regenerate with `pwsh docs/diagrams/render.ps1`. See [`README.md`](README.md).
