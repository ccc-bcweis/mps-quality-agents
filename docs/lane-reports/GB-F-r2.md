# Lane report: GB-F round 2 (Z1)

Branch `lane/z1-deploy`. Local commits only. Nothing was pushed. No `az` command was run. Docker was not started.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Detail is in `docs/lane-reports/GB-F-r2.md`. No contract file was changed.

## Done

| Issue | What |
|---|---|
| Z1 (#36) | One container for the API and the built console. Easy Auth on that origin. SQLite in the container. SQL and Static Web Apps off unless a parameter turns them on. Manual OIDC workflow. |

`deploy/app.Dockerfile` builds the console in live API mode and publishes `Mps.Api` into a non-root `aspnet:8.0` image. The API serves `wwwroot` and falls back to `index.html` only when that folder exists. The registry is Basic with admin disabled and AcrPull on the app identity. Azure OpenAI is keyless (`gpt-4o-mini`, capacity 20). Container settings include `Mps__Model__Mode=Live` and `Mps__Model__DailyCapUsd=2`. Anonymous `/health` was expected to be 401 or 302 (since Oct 17: health 200, API 401, console redirected to the landing page).

## How it was verified

Bicep CLI 0.47.16 compiled `infra/main.bicep`, built `infra/main.bicepparam`, and linted clean. CI still compiles with Bicep 0.48.1.

`dotnet format MPS.sln --verify-no-changes` exited 0. `dotnet build MPS.sln --configuration Release` exited 0 with 0 warnings. `dotnet test MPS.sln --configuration Release` exited 0: Domain 19, ModelGateway 13, Application 167, Simulator 47, Agents 77, Orchestration 15, MockEnterprise 4, Connectors 7, Architecture 14, Contract 39, Api 15. Api.IntegrationTests matched no `Category!=PendingLane` tests and the run stayed green.

From `web/`: `npm ci`, `npm run lint`, `npm test` (26 files, 57 tests), and `npm run build` exited 0. The image itself was not built here.

## Open questions for Bob

1. The OpenAI adapter still requires an API key. The template disables local keys and grants Cognitive Services OpenAI User. Live calls need the gateway to use that identity. Deploy of the container is not blocked.
2. Easy Auth is client id plus issuer only. There is no client secret in the template. If the login redirect cannot exchange a code, add a secret from the GitHub environment, not from git.
3. The `gpt-4o` deployment was removed. Both roles use `gpt-4o-mini`. Lower `openAiCapacity` if quota 20 is refused.
4. Each replica has its own SQLite file. Max replicas is 3. Scale-to-zero drops the file.
5. The smoke check proves the auth gate, not that an invited user can load the console. That remains Z2.
6. The workflow identity needs Contributor (or equivalent) on the resource group for the deploy and `az acr build`. AcrPull is only the pull identity.
7. Derived names use prefix `mpsdemo` (`mpsdemoacr`, `mpsdemo-aoai`). Change the prefix if a name is taken.
8. Diagrams were not updated. They still mention a Static Web App.
9. `Mps__Model__Mode` is the Linux form of `Mps:Model:Mode`. The API metrics route reads `LLM_MODE`, which is also set to `live`. Nothing binds the `Mps:Model` section yet.
