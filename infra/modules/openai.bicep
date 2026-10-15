// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Local auth is off. The managed identity calls the data plane. No key is declared.
// GlobalStandard capacity is thousands of tokens per minute. 20 is about 20,000 TPM.

param location string
param name string
param principalId string

@description('GlobalStandard capacity for the gpt-5.4-mini deployment.')
param capacity int = 20

var openAiUserRole = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource investigation 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: account
  name: 'gpt-5.4-mini'
  sku: {
    name: 'GlobalStandard'
    capacity: capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.4-mini'
      version: '2026-03-17'
    }
    raiPolicyName: 'Microsoft.Default'
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
  }
}

resource openAiUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, principalId, openAiUserRole)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', openAiUserRole)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output id string = account.id
output endpoint string = account.properties.endpoint
output deploymentName string = investigation.name
output investigationDeployment string = investigation.name
output resolutionDeployment string = investigation.name
