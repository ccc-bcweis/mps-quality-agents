// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Resource-group deployment. This file does not create the resource group.
// The demo container keeps SQLite. SQL and the Static Web App stay off unless a parameter turns them on.

targetScope = 'resourceGroup'

@description('Region for regional resources. East US 2 is the plan example.')
param location string = resourceGroup().location

@minLength(3)
@maxLength(12)
@description('Lowercase letters and digits. No hyphen: the registry name is this prefix plus acr. Derived names must be globally unique before a real deploy.')
param namePrefix string = 'mpsdemo'

@description('Container image. The workflow passes <acr>/mps-app:<tag> after the first bootstrap deploy.')
param apiImage string = 'mcr.microsoft.com/dotnet/aspnet:10.0'

@description('Create Azure SQL. The demo uses SQLite inside the container, so this stays false.')
param deploySql bool = false

@description('Create the Static Web App. The console is served from the API container, so this stays false.')
param deployStaticWeb bool = false

@description('Static Web Apps region. Used only when deployStaticWeb is true. eastus2 is supported for this SKU.')
param staticWebAppLocation string = 'eastus2'

@description('Azure SQL database name. Used only when deploySql is true.')
param sqlDatabaseName string = 'mps'

@description('GlobalStandard capacity for gpt-5.4-mini. 20 is about 20,000 tokens per minute.')
param openAiCapacity int = 20

@description('Entra application (client) id for Container Apps Easy Auth. The workflow passes the azure-demo variable.')
param entraClientId string = ''

@description('OpenID issuer for Easy Auth, for example https://login.microsoftonline.com/<tenant>/v2.0.')
param openIdIssuer string = ''

@description('Host name to bind on the container app. Empty means no custom domain. Set this together with customDomainCertificateId, or leave both empty.')
param customDomainName string = ''

@description('Full resource id of a managed certificate in the Container Apps environment. Empty means no custom domain. Set this together with customDomainName, or leave both empty.')
param customDomainCertificateId string = ''

@description('Comma-separated admin emails (join links, resets). Required when Entra sign-in is on: the app will not start without one. The deploy fills it from ADMIN_EMAILS, the running app, or the signed-in deployer.')
param adminEmails string = ''

@description('Monthly USD budget. Azure has no weekly grain. 40 stands in for the $10/week alert.')
param monthlyBudgetUsd int = 40

@description('Notification address. Empty skips the budget resource so no address is committed.')
param budgetContactEmail string = ''

@description('Budget window start. Must be the first day of a month.')
param budgetStart string = '2026-10-01'

@description('Key Vault secret that holds the approval signing key (hex, 32 bytes). It is created by hand, never by this template. Empty leaves the key unset.')
param approvalKeySecretName string = 'approval-signing-key'

@description('Budget window end.')
param budgetEnd string = '2027-09-30'

var identityName = '${namePrefix}-id'
var workspaceName = '${namePrefix}-law'
var insightsName = '${namePrefix}-ai'
var vaultName = '${namePrefix}kv'
var sqlServerName = '${namePrefix}-sql'
var openAiName = '${namePrefix}-aoai'
var registryName = '${namePrefix}acr'
var environmentName = '${namePrefix}-cae'
var appName = '${namePrefix}-api'
var webName = '${namePrefix}-web'

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    location: location
    name: identityName
  }
}

module logs 'modules/log-analytics.bicep' = {
  name: 'log-analytics'
  params: {
    location: location
    name: workspaceName
  }
}

module insights 'modules/app-insights.bicep' = {
  name: 'app-insights'
  params: {
    location: location
    name: insightsName
    workspaceId: logs.outputs.id
  }
}

module vault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    location: location
    name: vaultName
    principalId: identity.outputs.principalId
  }
}

module sql 'modules/sql.bicep' = if (deploySql) {
  name: 'sql'
  params: {
    location: location
    serverName: sqlServerName
    databaseName: sqlDatabaseName
    adminName: identity.outputs.name
    adminPrincipalId: identity.outputs.principalId
  }
}

module openai 'modules/openai.bicep' = {
  name: 'openai'
  params: {
    location: location
    name: openAiName
    principalId: identity.outputs.principalId
    capacity: openAiCapacity
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    location: location
    name: registryName
    principalId: identity.outputs.principalId
  }
}

module apps 'modules/container-apps.bicep' = {
  name: 'container-apps'
  params: {
    location: location
    envName: environmentName
    appName: appName
    identityId: identity.outputs.id
    identityClientId: identity.outputs.clientId
    apiImage: apiImage
    acrLoginServer: acr.outputs.loginServer
    openAiEndpoint: openai.outputs.endpoint
    modelDeployment: openai.outputs.deploymentName
    entraClientId: entraClientId
    openIdIssuer: openIdIssuer
    customDomainName: customDomainName
    customDomainCertificateId: customDomainCertificateId
    adminEmails: adminEmails
    appInsightsConnectionString: insights.outputs.connectionString
    approvalKeySecretUri: empty(approvalKeySecretName) ? '' : '${vault.outputs.uri}secrets/${approvalKeySecretName}'
  }
}

module web 'modules/static-web-app.bicep' = if (deployStaticWeb) {
  name: 'static-web-app'
  params: {
    name: webName
    location: staticWebAppLocation
  }
}

module budget 'modules/budget.bicep' = if (!empty(budgetContactEmail)) {
  name: 'budget'
  params: {
    name: '${namePrefix}-monthly'
    amount: monthlyBudgetUsd
    contactEmail: budgetContactEmail
    startDate: budgetStart
    endDate: budgetEnd
  }
}

output identityClientId string = identity.outputs.clientId
output identityPrincipalId string = identity.outputs.principalId
output appName string = appName
output appFqdn string = apps.outputs.fqdn
output apiFqdn string = apps.outputs.fqdn
output acrName string = acr.outputs.name
output acrId string = acr.outputs.id
output acrLoginServer string = acr.outputs.loginServer
output openAiEndpoint string = openai.outputs.endpoint
output webHostname string = deployStaticWeb ? web!.outputs.hostname : ''
output sqlServer string = deploySql ? sql!.outputs.fullyQualifiedDomainName : ''
output keyVaultUri string = vault.outputs.uri
