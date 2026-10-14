// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Resource-group budget. timeGrain Monthly is the smallest Azure budget grain.

param name string
param amount int
param contactEmail string
param startDate string
param endDate string

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: name
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
      endDate: endDate
    }
    notifications: {
      Actual_80: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        contactEmails: [
          contactEmail
        ]
        thresholdType: 'Actual'
      }
      Forecast_100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        contactEmails: [
          contactEmail
        ]
        thresholdType: 'Forecasted'
      }
    }
  }
}
