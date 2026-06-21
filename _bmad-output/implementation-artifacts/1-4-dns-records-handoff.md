# Story 1.4 — Manual DNS records to add for themarkdownweb.com

The apex custom-domain binding has been **initiated** on the Static Web App `swa-markdown-web`
(status: `Validating`). Azure is waiting for two DNS records. DNS for `themarkdownweb.com` is
managed externally, so **add these by hand** at your DNS provider. Once they propagate, Azure
validates the domain and **auto-issues a managed TLS certificate** (no further action needed).

## 1. Validation TXT record (required — proves you own the domain)

| Field | Value |
|-------|-------|
| Type | `TXT` |
| Host / Name | `_dnsauth` (i.e. `_dnsauth.themarkdownweb.com`) |
| Value | `_0eikqrixy05gpyeji5phm1667xvioua` |
| TTL | 3600 (or default) |

> Most registrars auto-append the zone — enter the host as `_dnsauth`, **not** the full
> `_dnsauth.themarkdownweb.com` (which would become `_dnsauth.themarkdownweb.com.themarkdownweb.com`).

## 2. Apex address record (required — points the domain at the Static Web App)

Prefer an **ALIAS / ANAME** at the apex if your provider supports it; otherwise use the **A** record.

| Option | Type | Host / Name | Value | TTL |
|--------|------|-------------|-------|-----|
| Preferred | `ALIAS` / `ANAME` | `@` (apex) | `purple-pond-09fadd20f.7.azurestaticapps.net` | 3600 |
| Fallback | `A` | `@` (apex) | `40.67.153.174` | 3600 |

> ⚠️ The apex currently resolves to a parked page (`76.223.105.230`, `13.248.243.5`). **Remove those
> existing apex A records** and replace them with the record above, or the domain will keep serving the
> old parked page. Never use a CNAME at the apex (`@`) — it breaks other apex records and most
> providers reject it; that's why ALIAS/ANAME or A is required here.

## 3. (Optional) www subdomain

| Type | Host / Name | Value | TTL |
|------|-------------|-------|-----|
| `CNAME` | `www` | `purple-pond-09fadd20f.7.azurestaticapps.net` | 3600 |

If you add `www`, also run `az staticwebapp hostname set -n swa-markdown-web -g rg-markdown-web --hostname www.themarkdownweb.com --validation-method cname-delegation` (CNAME validation is fine for a subdomain).

---

## After the records propagate

1. **Check validation status:**
   ```sh
   az staticwebapp hostname show -n swa-markdown-web -g rg-markdown-web \
     --hostname themarkdownweb.com --query "{status:status}" -o tsv
   ```
   Wait for `Ready` (can take minutes to a couple hours; TLS issuance follows automatically).

2. **(Optional) capture the binding as IaC** — re-deploy with the guard flag on so the live binding
   matches `infra/main.bicep`. **⚠️ Only run this AFTER step 1 shows `Ready`** — applying it against
   un-validated DNS will block on validation:
   ```sh
   az deployment group create -g rg-markdown-web -f infra/main.bicep -p infra/main.bicepparam \
     -p enableCustomDomain=true
   ```

3. **Verify the ACs (Story 1.4, Tasks 5):**
   ```sh
   curl -sI https://themarkdownweb.com/            # expect HTTP 200, valid TLS
   curl -s  https://themarkdownweb.com/ | grep -c "The Markdown Web"   # expect 1 (our page)
   curl -sI http://themarkdownweb.com/             # expect 301/308 redirect to https://
   ```
   Azure SWA serves valid TLS and redirects http→https automatically once the domain is `Ready`.

## Current state snapshot
- SWA: `swa-markdown-web` (rg-markdown-web), default hostname `purple-pond-09fadd20f.7.azurestaticapps.net`
- Custom domain `themarkdownweb.com`: **Validating** (awaiting the records above)
- Validation token: `_0eikqrixy05gpyeji5phm1667xvioua`
- Stable inbound IP (apex A fallback): `40.67.153.174`
