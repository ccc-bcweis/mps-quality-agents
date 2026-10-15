# Lane report: GB-F round 2 (Z1)

Branch `lane/z1-deploy`. Local commits only. Nothing was pushed. No `az` command was run. Docker was not started.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## Done

| Issue | What |
|---|---|
| Z1 (#36) | One container image, Easy Auth, optional SQL, manual OIDC deploy. |

No contract file was changed. No design-question file was opened. The gaps below are implementation notes for Bob, not contract changes.

### Image

`deploy/app.Dockerfile` is a three-stage build. Node 22 builds `web/` with `VITE_USE_MSW=false` and an empty `VITE_API_BASE` (same origin). The SDK publishes `Mps.Api`. The final stage is `mcr.microsoft.com/dotnet/aspnet:8.0`, copies `web/dist` to `wwwroot`, listens on 8080, and runs as the non-root user (`APP_UID`, fallback 1654). SQLite is `Data Source=/app/data/mps.db`. That directory is created and owned by the app user. The build context does not copy `data/groundtruth`.

`Mps.Api` serves static files and falls back to `index.html` only when `wwwroot` exists. Client routes return the console. `/api`, `/hubs`, `/health`, and missing files with an extension stay on the API. Local `dotnet run` has no `wwwroot`, so it stays API-only.

`deploy/api.Dockerfile` now also copies `prompts/`, because the agents project embeds those files. Local Compose still uses that Dockerfile and `deploy/web.Dockerfile`. Azure uses `deploy/app.Dockerfile`.

### Infrastructure

`infra/modules/acr.bicep`: Basic, admin user disabled, AcrPull (`7f951dda-4ed3-4680-a7ca-43fe172d538d`) for the app's user-assigned identity.

`infra/modules/container-apps.bicep`: image from that registry via the managed identity, external ingress on 8080, scale 0 to 3. Settings: Azure OpenAI endpoint and deployment names (the `gpt-4o-mini` deployment), `Mps__Model__Mode=Live`, `Mps__Model__DailyCapUsd=2`, `LLM_MODE=live` (the metrics route reads `LLM_MODE`), `AZURE_CLIENT_ID`, and the App Insights connection string as a secret reference. No model key.

Easy Auth is `Microsoft.App/containerApps/authConfigs` `current`, API version `2024-03-01`. The Entra provider takes `entraClientId` and `openIdIssuer`. `platform.enabled` is true and `unauthenticatedClientAction` is `RedirectToLoginPage`. That API version has no `requireAuthentication` property. An empty client id or issuer skips the auth config so the committed parameters file does not create a broken login. The workflow refuses to run without both variables.

`infra/modules/openai.bicep`: account with a custom subdomain, local auth disabled, one `gpt-4o-mini` GlobalStandard deployment. `capacity` defaults to 20 (about 20,000 tokens per minute). The managed identity has Cognitive Services OpenAI User. The previous `gpt-4o` deployment was removed so the demo only needs mini quota. Investigation and resolution settings both name `gpt-4o-mini`.

`deploySql` defaults to false. `deployStaticWeb` defaults to false. Outputs include `appFqdn`, `acrLoginServer`, `acrName`, `acrId`, `openAiEndpoint`, and `identityPrincipalId`. `apiFqdn` is still the same host name.

### Workflow

`.github/workflows/deploy.yml` is `workflow_dispatch` only, environment `azure-demo`, OIDC via `azure/login`. The image tag input defaults to blank, and a blank tag uses `github.sha`.

The job deploys `infra/main.bicep` with `mcr.microsoft.com/dotnet/aspnet:8.0` so the registry exists, waits until AcrPull is visible, runs `az acr build` of `deploy/app.Dockerfile` as `mps-app:<tag>`, deploys again with `apiImage=<acrLoginServer>/mps-app:<tag>`, then curls `https://<appFqdn>/health` and expects 401 or 302. It prints the URL.

Variables: `RESOURCE_GROUP`, `ENTRA_CLIENT_ID`, `ENTRA_ISSUER`, `BUDGET_EMAIL`. Secrets for login: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`. `BUDGET_EMAIL` may be empty. That skips the budget resource.

The README has a Deploy to Azure section. Diagrams were not edited.

## How it was verified

Bicep CLI 0.47.16 (`%USERPROFILE%\.azure\bin\bicep.exe`) `build infra/main.bicep`, `build-params infra/main.bicepparam`, and `lint infra/main.bicep` exited 0. The JSON was written under the temp directory and is not in git. CI compiles with Bicep 0.48.1. `az` was not used.

`dotnet format MPS.sln --verify-no-changes` exited 0. `dotnet build MPS.sln --configuration Release` exited 0 with 0 warnings. `dotnet test MPS.sln --configuration Release` exited 0: Domain 19, ModelGateway 13, Application 167, Simulator 47, Agents 77, Orchestration 15, MockEnterprise 4, Connectors 7, Architecture 14, Contract 39, Api 15. Api.IntegrationTests matched no `Category!=PendingLane` tests and the run stayed green. That filter is the project's own `VSTestTestCaseFilter`.

The new API tests cover a missing `wwwroot` (GET `/` is 404, `/health` is ok) and a temp `wwwroot` (console HTML at `/` and `/fleet`, the script asset, 404 for a missing script and a missing `/api` route, health still JSON). `DeployShapeTests` locks the Dockerfile, Bicep, and workflow tokens.

From `web/`: `npm ci`, `npm run lint`, `npm test` (26 files, 57 tests), and `npm run build` exited 0. Vite warns that the main chunk is about 870 kB. That warning was already true of this console. Docker was not run, so the multi-stage image was not built on this machine.

## Open questions for Bob

1. `AzureOpenAiChatClient.Create` still requires an API key. The template turns local auth off and grants Cognitive Services OpenAI User. The container sets the endpoint, the deployment name, and `AZURE_CLIENT_ID`, and it sets no key. Live completions will fail until the gateway uses that identity. This does not block deploying the container. It does block a live model call.
2. Easy Auth's code exchange often needs a client secret or a certificate. The brief named `entraClientId` and `openIdIssuer` only, and the template stores neither a secret nor a thumbprint. If the 302 reaches Entra and the token exchange fails, the fix is a Container App secret supplied from the GitHub environment, not from git.
3. The `gpt-4o` deployment is gone. Say if resolution should have its own deployment. Capacity 20 on mini may be more quota than the subscription has. Lower `openAiCapacity` in the workflow parameters if the deploy rejects it.
4. Each replica has its own SQLite file. `maxReplicas` is 3, as specified. Two replicas will not see the same tickets. For the pitch, one active replica is the safe case. Scale-to-zero drops the file.
5. The smoke check proves anonymous `/health` is 401 or 302. It does not sign in. An invited user, and a healthy revision after the image swap, are still Z2. Easy Auth can return 302 before the app process is listening, so a green smoke check is not proof the console loaded.
6. The workflow identity needs permission to deploy the resource group and to run `az acr build`. Contributor on the group is enough. AcrPull on the app identity is only for the pull.
7. Names come from `namePrefix` `mpsdemo`. The registry name is `mpsdemoacr`. The OpenAI custom subdomain is `mpsdemo-aoai`. Change the prefix if a name is taken. The registry name cannot contain a hyphen.
8. `docs/diagrams/physical.md` and `docs/diagrams/cicd.md` still describe a Static Web App and, in one identity table, the GitHub environment `demo`. They were left as they are.
9. `Mps:Model:Mode` and `Mps:Model:DailyCapUsd` are set as `Mps__Model__Mode` and `Mps__Model__DailyCapUsd` because a Linux environment name cannot contain a colon. The gateway options type still uses `Mode` as `ModelRoute` and `DailySpendCapUsd`. Nothing in the API binds the `Mps:Model` section yet.
