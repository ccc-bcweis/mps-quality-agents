# Spend notes

This template is not deployed. CI runs `bicep build` only. Bob deploys later, after Bryan approves Z2, into resource group `rg-mps-demo` (example region East US 2). Deleting that resource group is the teardown.

Azure budgets have no weekly grain. `monthlyBudgetUsd` defaults to 40, which is the stand-in for the plan's $10/week alert. The budget resource is created only when `budgetContactEmail` is set. The committed parameters file leaves that value empty, so no address is stored here.

The $2/day model cap is application code in the model gateway (later issues A1 and Z2). It is not a Bicep resource. Azure OpenAI deployments use GlobalStandard capacity 1 (about 1,000 tokens per minute each) as a quota backstop. Local key authentication on the Azure OpenAI account is disabled. The user-assigned managed identity has Cognitive Services OpenAI User on that account.

Other cost choices in the template:

- Container Apps scale from 0 to 3 replicas. The image is `mcr.microsoft.com/dotnet/aspnet:8.0` until Z1 publishes the API image.
- Azure SQL is General Purpose serverless, 0.5 vCore minimum, auto-pause after 60 minutes, local backup redundancy. There is no SQL password. The managed identity is the Entra-only administrator. Creating a narrower contained database user is a post-deploy step this template does not run.
- Log Analytics retention is 30 days on the PerGB2018 SKU.
- Application Insights is workspace-based. Its connection string is a deployment-time secret reference, not a value in git.
- Key Vault is standard, RBAC, 7-day soft delete. The managed identity has Key Vault Secrets User.
- Static Web Apps uses the Standard SKU in `eastus2`. That SKU is the main fixed cost in this set. The Free SKU is the fallback if Bryan wants a lower floor before the review.

Names derived from `namePrefix` (`mpsdemo` in the parameters file) must be globally unique at deploy time, including the Key Vault, the SQL server, and the Azure OpenAI custom subdomain. Change the prefix before deploy if a name is taken. Do not put secrets in the parameters file.
