# Spend notes

Bryan approved the first Azure deploy. `.github/workflows/deploy.yml` is manual (`workflow_dispatch`) on the GitHub environment `azure-demo`. CI still only runs `bicep build`. Bob runs the workflow into resource group `rg-mps-demo` (East US 2). Deleting that resource group is the teardown.

Azure budgets have no weekly grain. `monthlyBudgetUsd` defaults to 40, which is the stand-in for the plan's $10/week alert. The budget resource is created only when `budgetContactEmail` is set. The committed parameters file leaves that value empty. The workflow passes the `BUDGET_EMAIL` variable, which can also be empty.

The container setting `Mps__Model__DailyCapUsd` is `2`. That is the $2/day application cap. It is not a Bicep budget. Azure OpenAI is one `gpt-5.4-mini` deployment (2026-03-17; gpt-4o-mini 2024-07-18 is deprecating and Azure blocks new deployments of it), GlobalStandard, capacity `openAiCapacity` (default 20, about 20,000 tokens per minute). Local key authentication on the account is disabled. The user-assigned managed identity has Cognitive Services OpenAI User. The template declares no model key.

Other cost choices in the template:

- One Container App serves the API and the built console. It scales from 0 to 3 replicas. The image is `<acrLoginServer>/mps-app:<tag>` after the workflow build. The first deploy uses the public ASP.NET runtime image only so the registry exists.
- Azure Container Registry is Basic. The admin user is disabled. The app identity has AcrPull.
- SQLite lives inside the container (`Data Source=/app/data/mps.db`), seeded on start, and is discarded with the replica. `deploySql` defaults to false. When it is true, Azure SQL is General Purpose serverless, 0.5 vCore minimum, auto-pause after 60 minutes, local backup redundancy, with no SQL password. The managed identity is the Entra-only administrator.
- Log Analytics retention is 30 days on the PerGB2018 SKU.
- Application Insights is workspace-based. Its connection string is a deployment-time secret reference, not a value in git.
- Key Vault is standard, RBAC, 7-day soft delete. The managed identity has Key Vault Secrets User.
- `deployStaticWeb` defaults to false. The console is in the API image. Turning the parameter on creates a Standard Static Web App in `eastus2`, which is a fixed cost the demo does not need.

Names derived from `namePrefix` (`mpsdemo` in the parameters file) must be globally unique at deploy time, including the registry (`mpsdemoacr`), the Key Vault, and the Azure OpenAI custom subdomain. Change the prefix before deploy if a name is taken. Do not put secrets in the parameters file. The API runs exactly one replica (`minReplicas` 1, `maxReplicas` 1) so demo state lives in one SQLite file and is not lost to scale-to-zero. One always-on replica adds a small idle cost.
