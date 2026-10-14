// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

param name string
param location string

resource site 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: 'Enabled'
  }
}

output id string = site.id
output hostname string = site.properties.defaultHostname
