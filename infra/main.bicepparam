// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// No secrets. budgetContactEmail is empty, so the budget resource is not created.

using 'main.bicep'

param location = 'eastus2'
param namePrefix = 'mpsdemo'
param apiImage = 'mcr.microsoft.com/dotnet/aspnet:8.0'
param staticWebAppLocation = 'eastus2'
param sqlDatabaseName = 'mps'
param monthlyBudgetUsd = 40
param budgetContactEmail = ''
param budgetStart = '2026-10-01'
param budgetEnd = '2027-09-30'
