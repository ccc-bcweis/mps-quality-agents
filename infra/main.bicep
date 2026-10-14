// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Resource-group deployment. This file does not create the resource group and is not deployed by this lane.

targetScope = 'resourceGroup'

@description('Region for regional resources. East US 2 is the plan example.')
param location string = resourceGroup().location

@minLength(3)
@maxLength(12)
@description('Lowercase letters and digits. Derived names must be globally unique before a real deploy.')
param namePrefix string = 'mpsdemo'

@description('Container image for the API. The ASP.NET runtime image is a placeholder until Z1.')
param apiImage string = 'mcr.microsoft.com/dotnet/aspnet:8.0'

@description('Static Web Apps region. eastus2 is supported for this SKU.')
param staticWebAppLocation string = 'eastus2'

@description('Azure SQL database name.')
param sqlDatabaseName string = 'mps'

@description('Monthly USD budget. Azure has no weekly grain. 40 stands in for the $10/week alert.')
param monthlyBudgetUsd int = 40

@description('Notification address. Empty skips the budget resource so no address is committed.')
param budgetContactEmail string = ''

@description('Budget window start. Must be the first day of a month.')
param budgetStart string = '2026-10-01'

@description('Budget window end.')
param budgetEnd string = '2027-09-30'

var identityName = '${namePrefix}-id'
var workspaceName = '${namePrefix}-law'
var insightsName = '${namePrefix}-ai'
var vaultName = '${namePrefix}kv'
var sqlServerName = '${namePrefix}-sql'
var openAiName = '${namePrefix}-aoai'
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

module sql 'modules/sql.bicep' = {
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
    appInsightsConnectionString: insights.outputs.connectionString
  }
}

module web 'modules/static-web-app.bicep' = {
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
output apiFqdn string = apps.outputs.fqdn
output webHostname string = web.outputs.hostname
output openAiEndpoint string = openai.outputs.endpoint
output keyVaultUri string = vault.outputs.uri
output sqlServer string = sql.outputs.fullyQualifiedDomainName
