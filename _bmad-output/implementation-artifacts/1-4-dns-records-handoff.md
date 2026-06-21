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

**GoDaddy does not support ALIAS/ANAME at the apex — and you don't need it.** Azure Static Web Apps
apex domains use a plain **A record**, which GoDaddy supports. Use this:

| Type | Host / Name | Value | TTL |
|------|-------------|-------|-----|
| `A` | `@` (apex) | `40.67.153.174` | 1 hour (or default) |

> ⚠️ Never use a CNAME at the apex (`@`) — RFC-invalid and GoDaddy rejects it. The A record above is
> the correct and only apex option here.

### GoDaddy-specific steps
1. GoDaddy → **My Products → Domains → themarkdownweb.com → DNS / Manage DNS**.
2. **Turn OFF domain forwarding/parking** if enabled (Domain Settings → Forwarding → remove). GoDaddy's
   parked page injects its own apex `A` record (`76.223.105.230` / `13.248.243.5`) — that will fight your
   record if forwarding stays on.
3. Find the existing apex **A** record with Name `@` and **edit it** to value `40.67.153.174`
   (or delete the parked one and Add a new `A` record, Name `@`, Value `40.67.153.174`).
4. **Add** a `TXT` record: Name `_dnsauth`, Value `_0eikqrixy05gpyeji5phm1667xvioua` (record #1 above).
5. Save. GoDaddy DNS usually propagates within minutes (can be up to an hour).

> The SWA inbound IP `40.67.153.174` is Azure's documented **stable** inbound IP for this app — fine to
> pin in an A record.
>
> Prefer fully IaC-managed DNS instead? You can delegate the domain to **Azure DNS** (create a zone,
> point GoDaddy's nameservers at Azure once, then manage records as code). Same apex A record applies —
> Azure DNS isn't an alias target for SWA either — so it's optional and only worth it if you want the
> DNS records themselves under IaC. Say the word and I'll set that up.

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
