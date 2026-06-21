using './main.bicep'

// No secrets, no subscription IDs — only safe, declarative defaults.
param appName = 'swa-markdown-web'
param location = 'eastus2'
param sku = 'Free'
