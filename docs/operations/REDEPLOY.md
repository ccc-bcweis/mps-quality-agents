# MPS demo — torn down 2026-11-03 (redeploy runbook)

All Azure resources were in **rg-mps-demo** (eastus2, subscription Pay-As-You-Go 296993e9-…). The group was deleted to stop spend. The repo, Entra app registrations and GitHub config were kept.

## Deleted (rg-mps-demo)
mpsdemo-api (Container App), mpsdemo-cae (Container Apps env + managed cert for mpsdemo.commonconsulting.us), mpsdemoacr (ACR, image `mps-app`), mpsdemokv (Key Vault, secret `approval-signing-key`; purged), mpsdemo-aoai (Azure OpenAI, deployment `gpt-5.4-mini` 2026-03-17 GlobalStandard cap 20; purged), mpsdemokdxz6lxk2b4pg (Storage, tables JoinLinks/Members), mpsdemo-id (user-assigned identity), mpsdemo-law, mpsdemo-ai, Failure Anomalies alert rule.

## Kept
- Entra apps: `MPS Quality Demo (sign-in)` a21f580f-8d6c-440d-a6ca-6cb7e5227f74, `mps-demo-github-deployer` d8704784-1dc2-4e05-a55b-1d8fa0b836ff (its Contributor + UAA role assignments on the RG went with it).
- GitHub environment `azure-demo`: secrets AZURE_CLIENT_ID / AZURE_SUBSCRIPTION_ID / AZURE_TENANT_ID; variables ADMIN_EMAILS, AZURE_LOCATION, BUDGET_EMAIL, CUSTOM_DOMAIN, CUSTOM_DOMAIN_CERT_ID, ENTRA_CLIENT_ID, ENTRA_ISSUER, RESOURCE_GROUP.
- Workflows CI, CodeQL, Deploy, Quality are **disabled** (Quality ran daily at 06:00 UTC).

## Redeploy
1. `az group create -n rg-mps-demo -l eastus2`
2. Re-grant the deployer SP: Contributor + User Access Administrator on rg-mps-demo (`az role assignment create --assignee d8704784-1dc2-4e05-a55b-1d8fa0b836ff ...`). Check its federated credential still targets env `azure-demo`.
3. `gh workflow enable Deploy CI CodeQL Quality` then `gh workflow run Deploy` (or `deploy/deploy.sh`; infra is `infra/main.bicep` + `main.bicepparam`, see infra/ENTRA.md, infra/BUDGET.md).
4. Custom domain: the managed cert ID changes. Clear/unset `CUSTOM_DOMAIN_CERT_ID`, deploy, re-validate `mpsdemo.commonconsulting.us` (DNS CNAME/TXT to the new Container App FQDN; the old FQDN was mpsdemo-api.politepond-dc09540c.eastus2.azurecontainerapps.io), then set the new cert ID.
5. Key Vault `approval-signing-key`: a fresh random value is fine (it only signs approvals); the old value is backed up off-repo.
6. Restore JoinLinks rows from the backup if still wanted (2 rows; Members was empty).
7. Run `deploy/smoke-live.sh`.

Backups (box only, not in repo): `/workspace/mps-teardown/` — ARM export, container app config, secrets (chmod 600), table export.
