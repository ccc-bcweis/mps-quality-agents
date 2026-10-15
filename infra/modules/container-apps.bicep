// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// One container serves the API and the console. It pulls from ACR with its user-assigned identity.
// Easy Auth requires a sign-in. The 2024-03-01 schema has no requireAuthentication property.
// platform.enabled plus unauthenticatedClientAction RedirectToLoginPage is that gate.
// Anonymous callers get a redirect (302). Some clients get 401. There is no model key.

param location string
param envName string
param appName string
param identityId string
param identityClientId string
param apiImage string
param acrLoginServer string
param openAiEndpoint string
param modelDeployment string

@description('Entra application (client) id for Easy Auth. Empty skips the auth config.')
param entraClientId string = ''

@description('OpenID issuer, for example https://login.microsoftonline.com/<tenant>/v2.0. Empty skips the auth config.')
param openIdIssuer string = ''

@secure()
param appInsightsConnectionString string

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: envName
  location: location
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: acrLoginServer
          identity: identityId
        }
      ]
      secrets: [
        {
          name: 'appinsights'
          value: appInsightsConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ConnectionStrings__Mps'
              value: 'Data Source=/app/data/mps.db'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: identityClientId
            }
            {
              name: 'AZURE_OPENAI_ENDPOINT'
              value: openAiEndpoint
            }
            {
              name: 'AZURE_OPENAI_DEPLOYMENT'
              value: modelDeployment
            }
            {
              name: 'AZURE_OPENAI_DEPLOYMENT_INVESTIGATION'
              value: modelDeployment
            }
            {
              name: 'AZURE_OPENAI_DEPLOYMENT_RESOLUTION'
              value: modelDeployment
            }
            {
              // ASP.NET configuration key Mps:Model:Mode. Linux env names cannot use a colon.
              name: 'Mps__Model__Mode'
              value: 'Live'
            }
            {
              // ASP.NET configuration key Mps:Model:DailyCapUsd.
              name: 'Mps__Model__DailyCapUsd'
              value: '2'
            }
            {
              // The injector routes faults past 3 sigma through the SPC rules, which raise the alert and open the ticket.
              // The API host runs no background simulator, so this is the hosted demo's SPC path.
              name: 'Mps__Injector__DirectTicket'
              value: 'true'
            }
            {
              name: 'Mps__Model__Endpoint'
              value: openAiEndpoint
            }
            {
              name: 'Mps__Model__Deployment'
              value: modelDeployment
            }
            {
              name: 'LLM_MODE'
              value: 'live'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'appinsights'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
      }
    }
  }
}

resource auth 'Microsoft.App/containerApps/authConfigs@2024-03-01' = if (!empty(entraClientId) && !empty(openIdIssuer)) {
  parent: app
  name: 'current'
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      redirectToProvider: 'azureactivedirectory'
      unauthenticatedClientAction: 'RedirectToLoginPage'
    }
    httpSettings: {
      requireHttps: true
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: entraClientId
          openIdIssuer: openIdIssuer
        }
        validation: {
          // The second audience accepts Azure CLI tokens for the sign-in app's user_impersonation scope,
          // so an assigned user can run the scripted smoke test (deploy/smoke-live.sh). See infra/ENTRA.md.
          allowedAudiences: [
            entraClientId
            'api://${entraClientId}'
          ]
        }
      }
    }
  }
}

output id string = app.id
output fqdn string = app.properties.configuration.ingress.fqdn
