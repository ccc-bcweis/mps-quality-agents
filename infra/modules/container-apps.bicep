// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// One container serves the API and the console. It pulls from ACR with its user-assigned identity.
// Easy Auth signs people in with the existing Entra app. Anonymous requests reach the app (AllowAnonymous) so a signed-out
// browser lands on the branded /landing.html instead of a raw login redirect. The app enforces sign-in: the API and hub
// answer 401 without an Easy Auth principal (AccessGate), and console pages redirect to the landing page (ConsoleEntryGate).
// There is no model key.

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

@description('Host name to bind on the container app. Empty means no custom domain. Set this together with customDomainCertificateId, or leave both empty.')
param customDomainName string = ''

@description('Full resource id of a managed certificate in this Container Apps environment. Empty means no custom domain. Set this together with customDomainName, or leave both empty.')
param customDomainCertificateId string = ''

// Both empty: no custom domain, which is what a clone gets. Both set: bind the host name to the
// existing managed certificate. Only one set: stop, so a deploy cannot drop the binding by accident.
var domainNameSet = !empty(customDomainName)
var domainCertificateSet = !empty(customDomainCertificateId)
var customDomains = (domainNameSet && domainCertificateSet) ? [
  {
    name: customDomainName
    certificateId: customDomainCertificateId
    bindingType: 'SniEnabled'
  }
] : (domainNameSet || domainCertificateSet) ? fail('Set both customDomainName and customDomainCertificateId, or leave both empty. One without the other stops the deploy so the custom domain binding is not dropped.') : []

@description('Comma-separated emails that get the demo admin harness (join links, resets). No default: main.bicep passes the deployer-supplied list.')
param adminEmails string

@secure()
param appInsightsConnectionString string

@description('Key Vault secret URI for the approval signing key (hex, 32 bytes). The app identity reads it. Empty leaves it unset, and the app will not start in Production.')
param approvalKeySecretUri string = ''

@description('API container vCPU, as a number string. 0.5 pairs with memory 1Gi.')
param cpu string = '0.5'

@description('API container memory. 1Gi pairs with cpu 0.5.')
param memory string = '1Gi'

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
        customDomains: customDomains
      }
      registries: [
        {
          server: acrLoginServer
          identity: identityId
        }
      ]
      secrets: concat([
        {
          name: 'appinsights'
          value: appInsightsConnectionString
        }
      ], empty(approvalKeySecretUri) ? [] : [
        {
          // Key Vault reference: the value never passes through the deployment or Git.
          name: 'approval-signing-key'
          keyVaultUrl: approvalKeySecretUri
          identity: identityId
        }
      ])
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: concat([
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
              // ASP.NET configuration key Mps:Model:Mode (offline | openai | azure). Linux env names cannot use a colon.
              name: 'Mps__Model__Mode'
              value: 'azure'
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
              // Easy Auth signs people in; the API then lets in home-tenant users and join-link members (ADR-011).
              name: 'Mps__Auth__Mode'
              value: empty(entraClientId) ? 'local' : 'azure'
            }
            {
              name: 'Mps__Auth__HomeTenantId'
              value: tenant().tenantId
            }
            {
              name: 'Mps__Auth__AdminEmails'
              value: adminEmails
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
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'appinsights'
            }
          ], empty(approvalKeySecretUri) ? [] : [
            {
              // ASP.NET configuration key Mps:Approval:SigningKey, read once by ApprovalPolicyLoader.
              name: 'Mps__Approval__SigningKey'
              secretRef: 'approval-signing-key'
            }
          ])
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
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
      unauthenticatedClientAction: 'AllowAnonymous'
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
