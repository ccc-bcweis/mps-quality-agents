// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// No secrets. The workflow overrides the Entra values and the image.
// budgetContactEmail is empty here, so a parameters-file deploy does not create the budget resource.
// deploySql and deployStaticWeb stay false: the demo is one container and SQLite.

using 'main.bicep'

param location = 'eastus2'
param namePrefix = 'mpsdemo'
param apiImage = 'mcr.microsoft.com/dotnet/aspnet:8.0'
param deploySql = false
param deployStaticWeb = false
param staticWebAppLocation = 'eastus2'
param sqlDatabaseName = 'mps'
param openAiCapacity = 20
param entraClientId = ''
param openIdIssuer = ''
param monthlyBudgetUsd = 40
param budgetContactEmail = ''
param budgetStart = '2026-10-01'
param budgetEnd = '2027-09-30'
