# Lane report: GB-F foundation

Branch `lane/f-foundation`. Local commits only. Nothing was pushed. `contracts-v1` was not tagged.

| Issue | Commit | What landed |
|---|---|---|
| F1 (#1) | `22ca3ba` | `MPS.sln`, layered projects, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `global.json` (SDK 8.0.426), NetArchTest layering |
| F2 (#2) | `e8560a1` | Domain entities and invariants, read tools, write ports, `IInvestigationWorkflow`, OpenAPI, mock-enterprise OpenAPI, MSW handlers, 12 event schemas, `docs/adr/ADR-001-contracts.md` |
| F3 (#3) | `00c9a2f` | `.github/workflows/ci.yml`, `codeql.yml`, `deploy.yml` stub, `coverlet.runsettings` |
| F4 (#4) | `c34b737` | `docker-compose.yml`, API and placeholder web Dockerfiles, `.env.example`, `.env` gitignore |
| F5 (#5) | `8d3b3af` | `.vscode` launch/tasks/extensions, `.devcontainer/devcontainer.json`, README Clone and run |
| F6 (#6) | `a436641` | `infra/main.bicep`, `infra/modules/*.bicep`, `infra/main.bicepparam`, `infra/BUDGET.md` |

## How it was verified

- After F2: `dotnet format MPS.sln --verify-no-changes` exited 0. `dotnet test MPS.sln --configuration Release` passed 19 domain tests and 10 architecture tests. The 12 event schemas parse as JSON.
- F5: `dotnet run --project src/Mps.Api --configuration Release` answered `GET http://localhost:8080/health` with `{"status":"ok","service":"Mps.Api"}`. The process was stopped after that check.
- F6: Bicep CLI 0.47.16 (`%USERPROFILE%\.azure\bin\bicep.exe`) compiled `infra/main.bicep` and `infra/main.bicepparam` with exit 0. The compiled JSON was written under the temp directory and deleted. It is not in git.
- VS Code and devcontainer JSON files parse.
- Docker Compose was not started. The dev container was not built. Azure CLI was not used. GitHub Actions were not executed from this machine.

## F3 behavior Bob should expect

`ci.yml` runs on pull requests and on push to `main`: restore, Release build, `dotnet format --verify-no-changes`, Coverlet plus a ReportGenerator HTML artifact. The 70% coverage gate is not turned on. The web job skips lint and test when `web/package.json` is absent. Gitleaks uses `gitleaks/gitleaks-action` v3.0.0. Dependency review runs on pull requests only. The Bicep job downloads Bicep CLI v0.48.1 and builds `infra/main.bicep`. It does not call `az`.

`codeql.yml` analyzes C# with `build-mode: manual` and `dotnet build`. JavaScript is omitted until `web/` exists.

`deploy.yml` is `workflow_dispatch` only, environment `azure-demo`, `azure/login` with OIDC (`id-token: write`). Build, push, what-if, and deploy are comments marked `TODO Z1`.

Action SHAs are pinned in the workflow files. Required checks and the `azure-demo` reviewer are GitHub settings for Bob.

## F6 shape

The deployment is resource-group scoped. It does not create `rg-mps-demo`. It defines a user-assigned identity, Log Analytics, workspace-based Application Insights, Key Vault (RBAC), Azure SQL General Purpose serverless, Azure OpenAI with local auth disabled, a Container Apps environment and app (min replicas 0, max 3, target port 8080), and a Standard Static Web App in `eastus2`. SQL has no password. The managed identity is the Entra-only administrator. The monthly budget module is included only when `budgetContactEmail` is non-empty. The parameters file leaves that value empty. Spend notes are in `infra/BUDGET.md`.

## Open questions for Bob

1. ADR-001 is at `docs/adr/ADR-001-contracts.md` because this brief asked for that path. PLAN section 4 records later decisions under `docs/decisions/`. Say if you want one directory.
2. Please review and tag `contracts-v1`. ADR-001 status is Proposed. This lane did not create the tag.
3. SPEC puts compose and IaC under `deploy/`. This lane followed the brief: `docker-compose.yml` at the repo root, Dockerfiles under `deploy/`, Bicep under `infra/`.
4. PLAN names the GitHub environment `demo`. This brief names `azure-demo`. `deploy.yml` uses `azure-demo`.
5. PLAN F6 acceptance mentions `az bicep build` and what-if. CI builds with the Bicep CLI and does not run what-if. What-if stays in the Z1 comments on `deploy.yml`.
6. FluentAssertions is 7.2.2 (Apache-2.0). 8.x is commercially licensed, so it is not referenced from this MIT repo.
7. `contracts/msw/handlers.ts` is hand-written for MSW 2 and is not executed here. `msw` is not installed, and `web/` was not created, so the web CI job stays skipped.
8. Gitleaks organization scans need a `GITLEAKS_LICENSE` secret. `ccc-bcweis/mps-quality-agents` is a user-owned repo, so the license is not required unless the repo moves under an organization.
9. Azure budgets cannot be weekly. The template uses a $40 monthly amount as the stand-in for $10/week. The $2/day model cap remains application code.
10. After a future deploy, a narrower SQL contained user is still a manual step. The template makes the managed identity the Entra admin and does not run a deployment script.
11. The Container Apps image is `mcr.microsoft.com/dotnet/aspnet:8.0` until Z1. The Aspire image tag is `mcr.microsoft.com/dotnet/aspire-dashboard:9.5` and was not pulled.
12. Derived Azure names use prefix `mpsdemo`. Key Vault, SQL, and the Azure OpenAI subdomain must be unique at deploy time. Change `namePrefix` if a name is taken.
13. A failed CAPA verification does not move the stage backward in this contract. JSON wire enums are camelCase. C# enums stay PascalCase. Mapping is documented in ADR-001 and is not implemented in the API yet.
14. F5 debugs the API. The web target in Compose is a static placeholder page.
