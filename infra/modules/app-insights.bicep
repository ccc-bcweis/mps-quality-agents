// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

param location string
param name string
param workspaceId string

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspaceId
    IngestionMode: 'LogAnalytics'
  }
}

output id string = component.id

@secure()
output connectionString string = component.properties.ConnectionString
