// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Join links and members. Shared key access is off. The app identity uses a data-plane role.

param location string
param name string
param principalId string
param linksTableName string
param membersTableName string

var tableDataContributor = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: account
  name: 'default'
}

resource linksTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: linksTableName
}

resource membersTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: membersTableName
}

resource tableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, principalId, tableDataContributor)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableDataContributor)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output tableEndpoint string = account.properties.primaryEndpoints.table
