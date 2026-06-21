// Azure hosting for The Markdown Web — Story 1.2 (walking skeleton, IaC).
//
// Provisions a single Azure Static Web App in MANUAL-DEPLOY mode (no repository linked):
// Story 1.3 deploys the built Astro site via GitHub Actions using the deployment token,
// and Story 1.4 binds the custom domain. Keeping CI/domain out of the template keeps
// provisioning idempotent and free of any PAT/secret.
//
// Deploy (resource-group scoped — the deploy identity is Contributor on rg-markdown-web only):
//   az deployment group create -g rg-markdown-web -f infra/main.bicep -p infra/main.bicepparam

targetScope = 'resourceGroup'

@description('Name of the Azure Static Web App. Stable (no per-run uniqueness) so re-deploys are idempotent. SWA names are unique within the resource group; the public hostname gets a random suffix automatically.')
@minLength(2)
@maxLength(60)
// DNS-safe: lowercase alphanumerics and hyphens, no leading/trailing hyphen. Caught at deploy otherwise with an opaque error.
@metadata({ pattern: '^[a-z0-9][a-z0-9-]{0,58}[a-z0-9]$' })
param appName string = 'swa-markdown-web'

@description('Location for the Static Web App. Must be an SWA-supported region (Free tier): westus2, centralus, eastus2, westeurope, eastasia.')
@allowed([
  'westus2'
  'centralus'
  'eastus2'
  'westeurope'
  'eastasia'
])
param location string = 'eastus2'

@description('Static Web App SKU. Free for the walking skeleton; custom domains + TLS are supported on Free.')
@allowed([
  'Free'
  'Standard'
])
param sku string = 'Free'

@description('Story 1.4 (FR-18): bind the apex custom domain. Leave FALSE until the _dnsauth TXT validation record and the apex A/ALIAS record are live in DNS — deploying this before DNS is in place would block on validation. Flip to true (or deploy with -p enableCustomDomain=true) once records have propagated; Azure then validates and auto-issues a managed TLS certificate.')
param enableCustomDomain bool = false

@description('Apex custom domain to bind when enableCustomDomain is true.')
param customDomain string = 'themarkdownweb.com'

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: appName
  location: location
  sku: {
    name: sku
    tier: sku
  }
  // Manual-deploy mode: intentionally no repositoryUrl/branch/repositoryToken. With no repo linked,
  // SWA enables the deployment-token (data-plane) path by default — that is what Story 1.3 uses to
  // push the built Astro site via GitHub Actions. The control-plane (this Bicep deploy) authenticates
  // separately via OIDC. `provider: 'None'` is the one settable default worth pinning so it does not
  // surface as what-if drift.
  // NOTE: `stableInboundIP` is read-only and server-computed — it cannot be set in any template and
  // will always appear as benign what-if "noise" on re-run. True idempotency is proven by re-running
  // `az deployment group create`: no duplicate resource, identical hostname (verified).
  properties: {
    provider: 'None'
  }
}

// Story 1.4 (FR-18): apex custom-domain binding, captured as IaC but guarded behind a flag.
// dns-txt-token validation: the user adds a TXT at _dnsauth.<domain> (the validation token) and an
// apex A/ALIAS record; Azure then validates and issues a managed TLS cert automatically. The live
// binding was initiated via `az staticwebapp hostname set` to surface the token for the manual DNS
// step; this resource is the declarative record to apply (enableCustomDomain=true) once DNS is live.
resource apexCustomDomain 'Microsoft.Web/staticSites/customDomains@2024-04-01' = if (enableCustomDomain) {
  parent: staticWebApp
  name: customDomain
  properties: {
    validationMethod: 'dns-txt-token'
  }
}

@description('Default *.azurestaticapps.net hostname assigned to the Static Web App. Consumed by Story 1.3 (deploy target) and Story 1.4 (apex A/ALIAS + _dnsauth TXT records).')
output defaultHostname string = staticWebApp.properties.defaultHostname

@description('Resource name of the Static Web App (for downstream az staticwebapp commands).')
output staticWebAppName string = staticWebApp.name
