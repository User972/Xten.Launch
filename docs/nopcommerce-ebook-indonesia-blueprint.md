# nopCommerce eBook Store — Indonesia Implementation Blueprint

**Prepared as:** Senior .NET Architect & nopCommerce Implementation Consultant deliverable
**Date:** 2026-05-30
**Scope:** Production-ready, secure, low-maintenance, high-performance **digital-eBook-only** storefront for an Indonesian customer, built on **nopCommerce (latest stable) + PostgreSQL + Redis**, deployed via **Docker on a VPS**.

> **Reading guide.** Sections 1–4 are architecture and platform decisions. Sections 5–9 are the store/product/security build. Sections 10–14 are operations. Sections 15–19 are decision aids, phasing, and the final recommendation. Concrete, runnable config lives in the `deploy/` folder of this repository and is referenced inline.

---

## 0. Executive summary (read this first)

| Question | Answer |
|---|---|
| Is nopCommerce + PostgreSQL suitable for this eBook store? | **Yes.** It is a strong, low-cost, self-hostable fit. The downloadable-product, license-agreement, and order-paid→activate-download model is built in and maps almost perfectly to digital eBooks. |
| Recommended version | **nopCommerce 4.90.x** (4.90.4 is current as of Mar 2026). Choose 4.90 specifically *because* its 4.90 release hardened cross-database collations for PostgreSQL/MySQL. |
| Runtime | **.NET 9** (what 4.90 targets). ⚠️ .NET 9 is **Standard-Term-Support** and its support window closes ~mid-2026 — see §2 for the mandatory upgrade plan to nopCommerce 5.0 / .NET 10 (LTS). |
| Biggest risk | **No official Indonesian payment plugin exists for nopCommerce.** Midtrans/Xendit/DOKU ship plugins for WooCommerce/Shopify/Magento, *not* nopCommerce → budget a **custom payment plugin** (recommended: Midtrans Snap, which gives QRIS + Virtual Account + e-wallets in one integration). |
| MVP payment | **Manual bank transfer / QRIS-by-upload** using the built-in "Manual" payment method (zero code) → then add Midtrans. |
| Do NOT | Do not fork/modify nopCommerce core. Do not expose eBook files as static URLs. Do not run app+DB with no backup test. Do not skip the .NET LTS upgrade plan. Do not rely on Stripe/PayPal for an Indonesia-only entity (see §8). Do not start on the pre-GA 5.0 `develop` branch. |

---

## 1. Recommended Architecture

### 1.1 VPS sizing

Digital-only stores are **light on compute** (no shipping calc, no warehouse, modest catalog) but **sensitive to memory** (nopCommerce + PostgreSQL + Redis in one box) and to **storage durability** (the eBook files and the DB are the business).

| Stage | Concurrent users (rough) | vCPU | RAM | Disk (SSD/NVMe) | Topology |
|---|---|---|---|---|---|
| **MVP / soft launch** | < 50 active | 2 | **4 GB** | 80 GB | Single VPS: app + PostgreSQL + Redis + reverse proxy, all in Docker |
| **Small production** | 50–300 active | 4 | **8 GB** | 160 GB | Single VPS (vertical) **or** split DB to its own small VPS |
| **Growth** | 300–1,500+ active | 4–8 (app) | **16 GB** (app) | 160 GB app + object storage | App node(s) + **separate/managed PostgreSQL** + managed Redis + CDN |

Notes:
- 4 GB is the realistic *floor* for app+DB+Redis together. On 2 GB you will OOM during catalog rebuilds. Give PostgreSQL ~25 % of RAM via `shared_buffers` (see §10.2) and leave headroom for the .NET server GC.
- Disk: keep the DB and download files on durable block storage with snapshots. NVMe helps PostgreSQL but is not required at MVP.
- Prefer a provider with **VPS snapshots + block-volume backups** in/near Indonesia or Singapore (low latency to Indonesian buyers): e.g. a Jakarta/Singapore region.

### 1.2 Docker Compose architecture

Five logical services (compose file: `deploy/docker-compose.yml`):

```
                      Internet (HTTPS 443)
                              │
                    ┌─────────▼─────────┐
                    │   reverse proxy   │  Caddy (auto-TLS) — or Nginx
                    │  (TLS, headers,   │  security headers, gzip/brotli,
                    │   gzip, ratelimit)│  rate-limit on /download/*
                    └─────────┬─────────┘
                              │ (internal network only)
                    ┌─────────▼─────────┐
                    │   nopcommerce     │  ASP.NET Core / .NET 9
                    │   (Nop.Web)       │  App_Data + Plugins on volumes
                    └───┬───────────┬───┘
                        │           │
              ┌─────────▼──┐    ┌───▼────────┐
              │ postgres   │    │  redis     │  distributed cache /
              │ (data)     │    │ (cache)    │  data-protection (optional)
              └─────┬──────┘    └────────────┘
                    │
            ┌───────▼────────┐
            │ db-backup      │  pg_dump on cron → local volume → offsite
            │ (sidecar)      │  (recommended; see §11)
            └────────────────┘
```

- **reverse proxy** is the *only* service published to the host (80/443). Everything else lives on an internal Docker network with **no published ports**.
- **A backup sidecar is recommended**, not optional, for production. Use a small `pg_dump`-on-cron container (e.g. `prodrigestivill/postgres-backup-local`) writing to a backup volume + pushing offsite to S3-compatible object storage.

### 1.3 Persistent volume strategy

nopCommerce keeps its identity and uploaded content under `App_Data` and `wwwroot`. **Anything not on a named volume is lost when the container is recreated** (and in this remote/cloud model, containers are ephemeral).

| Path inside container | What lives there | Volume? | Why |
|---|---|---|---|
| `/app/App_Data/` | `dataSettings.json` (DB conn), `appsettings.json`, `installedPlugins.json`, data-protection keys, install lock | **Yes — named volume `nop_appdata`** | Loss = re-install + everyone logged out + antiforgery breakage |
| `/app/Plugins/` (and `/app/App_Data/Plugins`) | Installed plugin binaries/state | **Yes — `nop_plugins`** | Survive redeploys without re-uploading |
| `/app/wwwroot/images/` | Uploaded product/cover images & thumbs | **Yes — `nop_images`** | User-generated assets |
| `/app/wwwroot/files/` & downloads | Downloadable files **if stored on disk** (by default nopCommerce stores downloads in the DB — see §9) | **Yes — `nop_downloads`** (only if using disk storage) | The product itself |
| nopCommerce logs | DB `Log` table by default; or Serilog → stdout | stdout → Docker logging | See §10.7 |
| PostgreSQL data dir `/var/lib/postgresql/data` | The database | **Yes — `pg_data`** | The business |
| Redis (optional persistence) | Cache | **Ephemeral by default** (see §11.4) | Cache loss is harmless if Redis is cache-only |

> **Key decision (§9/§11):** Keep eBook downloads in the **database** (nopCommerce default) for MVP — they are then served only through authorized controller logic and are covered by your DB backup. Move to disk/object-storage only when the catalog grows large enough that DB bloat hurts; that requires a storage plugin and changes the backup story.

### 1.4 OS, reverse proxy, TLS, domain

- **Linux OS:** **Ubuntu 24.04 LTS** (or Debian 12). Long support window, broad Docker tooling, easy unattended-upgrades.
- **Reverse proxy:** **Caddy** for MVP/small production — automatic Let's Encrypt TLS, HTTP/2 + HTTP/3, dead-simple config (`deploy/Caddyfile`). Choose **Nginx** instead only if you need very fine-grained rules or already standardize on it.
- **TLS/SSL:** Let's Encrypt, fully automated by Caddy (or Certbot for Nginx). Force HTTPS, HSTS, TLS 1.2+ only. Optionally place **Cloudflare** in front (free WAF, DDoS protection, DNS, CDN for static assets — see §10.4). If you use Cloudflare proxied, set SSL mode to **Full (strict)**.
- **Domain:** Use a dedicated domain (e.g. a `.com` or Indonesian `.co.id`/`.id`). Point `A`/`AAAA` to the VPS (or Cloudflare). Configure `www` → apex redirect. Set the canonical store URL in nopCommerce (Configuration → Stores).

### 1.5 Same VPS vs separated DB

- **MVP & small production: same VPS.** App + PostgreSQL + Redis co-located. Lowest cost, lowest latency, simplest ops. Perfectly adequate for a digital catalog with modest traffic.
- **Separate / managed DB at growth:** Move PostgreSQL to a **managed service** (or a dedicated DB VPS) when you need (a) independent scaling, (b) automated managed backups + PITR, (c) HA/failover, or (d) to scale the app to multiple instances. This is also the moment Redis stops being "nice to have" and becomes required (shared cache + data-protection keys across app instances).

### 1.6 MVP vs production recommendation

> **MVP:** one 2 vCPU / 4 GB Ubuntu VPS, Docker Compose with app + PostgreSQL + Redis + Caddy + backup sidecar, downloads stored in DB, manual bank-transfer/QRIS payment, daily off-site DB+App_Data backup. **Production:** 4 vCPU / 8 GB (or split DB), Midtrans plugin live, Cloudflare in front, off-site backups with tested restore, security headers + download rate-limiting, monitored.

---

## 2. nopCommerce Version & PostgreSQL Compatibility

### 2.1 Recommended version — **nopCommerce 4.90.x**

- 4.90.4 (released **16 March 2026**) is the current stable. The 4.90 line added AI-assisted admin features, B2B/quote features, mega-menu, multiple wishlists, Cloudflare Images integration — and, most relevant here, **improved cross-database collations and container fixes for MySQL/PostgreSQL**. For a *PostgreSQL* deployment specifically, that collation work is a concrete reason to pick 4.90 over older lines.

### 2.2 .NET version — **.NET 9**, with a mandatory LTS plan

- nopCommerce 4.90 targets **.NET 9** (install the .NET 9 ASP.NET runtime; SDK 9 + VS 2022 17.14+ to build/develop).
- ⚠️ **Runtime lifecycle caveat (important for a "low-maintenance" goal):** .NET 9 is a **Standard-Term-Support (STS)** release; its support window closes around **mid-2026**. .NET 8 is the current LTS (supported into late 2026) and **.NET 10 (LTS)** shipped Nov 2025. nopCommerce **5.0** is expected to move to .NET 10.
- **Recommendation:**
  1. **Build on 4.90.x / .NET 9 now** — it is the newest *stable* line and has the PostgreSQL fixes you want. **nopCommerce 5.0 / .NET 10 is not yet released as GA** (speculated ~spring 2026; only in-progress `develop`-branch work). Critically, **no marketplace plugins — including every Stripe/payment plugin — support 5.0 yet** (they top out at 4.90), so starting on 5.0 would also mean *no* off-the-shelf payments. Building a production store on a pre-GA `develop` branch trades away exactly the stability/low-maintenance this project is for.
  2. **Treat the move to nopCommerce 5.0 / .NET 10 (LTS) as a planned, budgeted upgrade.** **Upgrade trigger:** proceed only when (a) 5.0 is GA *and* (b) your required plugins (payment gateway, theme) publish 5.0-compatible builds. Pin the build to a specific tag (`NOP_VERSION=release-4.90.x`) for reproducible deploys until then, and keep core untouched (§15) so the jump stays cheap.
  3. In the meantime, the app is **not internet-facing directly** — it sits behind Caddy/Cloudflare on an internal network — which materially reduces the practical risk of a runtime nearing EOL. Keep the base image patched (`mcr.microsoft.com/dotnet/aspnet:9.0` receives updates) and rebuild regularly.
- *Alternative considered:* 4.70 on .NET 8 (LTS to late 2026). Rejected for this project because it lacks 4.90's PostgreSQL collation fixes and is itself near EOL — you'd be upgrading either way, so start on the line with the best PostgreSQL behavior.

### 2.3 PostgreSQL support & known caveats

- nopCommerce supports PostgreSQL natively (data layer via LinqToDB + FluentMigrator, Npgsql provider). Selectable in the install wizard alongside SQL Server and MySQL.
- **Caveats to plan for:**
  - **`citext` extension** is required (case-insensitive columns). nopCommerce installs it during DB initialization, but this means the **DB user must be able to `CREATE EXTENSION`** on first install (superuser, or pre-create the extension as superuser then run install with a lower-privilege app user). See §2.5.
  - **Third-party plugin compatibility:** Some community plugins historically assumed SQL Server (raw T-SQL, `NOLOCK`, SQL-specific functions). Always verify a plugin states PostgreSQL support before buying. (Most mainstream plugins are now DB-agnostic; the old "Ajax Filters" gap is built-in since 4.40.)
  - **Migration-from-SQL-Server quirks** (schema search-path, case sensitivity) — irrelevant here since you start fresh on PostgreSQL.
  - **Use released tags, not the `develop` branch.**

### 2.4 PostgreSQL vs SQL Server for this use case

| Factor | PostgreSQL | SQL Server | Verdict for this project |
|---|---|---|---|
| License cost | Free, open source | Express is free but capped (10 GB DB, 1 GB RAM, 1 socket); Standard is costly | **PostgreSQL** — no license, no Express ceiling |
| Linux/Docker fit | First-class, lightweight | Runs on Linux but heavier, more RAM | **PostgreSQL** |
| nopCommerce support | Supported & improved in 4.90 | Reference/most-tested DB | Tie; SQL Server is the most-trodden path but PostgreSQL is solid in 4.90 |
| Managed hosting options later | Abundant + cheap (many providers, Supabase, Aiven, RDS) | Fewer/pricier | **PostgreSQL** |
| Plugin ecosystem assumptions | Occasionally SQL-Server-centric | Default assumption | Minor edge to SQL Server; mitigated by vetting plugins |

**Conclusion:** For a cost-sensitive, Docker-on-Linux, digital-only store, **PostgreSQL is the right choice.** SQL Server's only real edge is being the most-tested DB and the safest assumption for random third-party plugins — which you neutralize by (a) staying on 4.90, (b) avoiding core changes, and (c) vetting each plugin for PostgreSQL support.

### 2.5 First-installation nuances

1. Bring up PostgreSQL first; create the database and an app user.
2. **`citext`:** either grant the install user permission to `CREATE EXTENSION`, or pre-run `CREATE EXTENSION IF NOT EXISTS citext;` as superuser before installing.
3. Browse to the site → nopCommerce **install wizard** → choose **PostgreSQL**, enter host/port/db/user/password, set the admin email/password, and **uncheck "Create sample data"** for a clean store (or check it once on a throwaway DB to learn the layout).
4. The wizard writes `App_Data/dataSettings.json` (provider + connection string) and `App_Data/appsettings.json`, then creates the schema. **Because `App_Data` is on a named volume, you run the wizard once and never again** across redeploys.
5. After install, edit `App_Data/appsettings.json` to enable Redis (snippet in §3) and restart.

---

## 3. appsettings.json / Environment Configuration

nopCommerce splits its config into two files under `App_Data/`:

- **`dataSettings.json`** — database provider + connection string (this is what the install wizard writes).
- **`appsettings.json`** — runtime config: cache, distributed cache (Redis), hosting/proxy, plugins, common.

### 3.1 `dataSettings.json` (PostgreSQL)

`deploy/config/dataSettings.template.json`:

```json
{
  "DataProvider": "postgresql",
  "ConnectionString": "Host=postgres;Port=5432;Database=nopcommerce;Username=nopapp;Password=__SET_VIA_SECRET__;Pooling=true;Maximum Pool Size=100;Timeout=15;Command Timeout=60;",
  "WithNoLock": false
}
```

- `DataProvider` value is literally `postgresql` (others: `sqlserver`, `mysql`).
- Npgsql keywords: `Host`, `Port`, `Database`, `Username`, `Password`, `Pooling`, `Maximum Pool Size` (see §10.8 for pool sizing).
- `WithNoLock` is a SQL-Server concept; keep `false` on PostgreSQL.

### 3.2 `appsettings.json` — Redis, hosting, plugins, logging

The relevant blocks (full snippet to merge: `deploy/config/appsettings.redis-snippet.json`):

```json
{
  "CacheConfig": {
    "DefaultCacheTime": 60,
    "ShortTermCacheTime": 3,
    "BundledFilesCacheTime": 120
  },
  "HostingConfig": {
    "UseProxy": true,
    "KnownProxies": ""
  },
  "DistributedCacheConfig": {
    "DistributedCacheType": "redis",
    "Enabled": true,
    "ConnectionString": "redis:6379,password=__SET_VIA_SECRET__,ssl=False,abortConnect=False",
    "SchemaName": "",
    "InstanceName": "nop_"
  },
  "PluginConfig": {
    "UseUnsafeLoadAssembly": true
  },
  "CommonConfig": {
    "UseSessionStateTempDataProvider": false,
    "StaticFilesCacheControl": "public,max-age=604800",
    "SupportPreviousNopcommerceVersions": false
  }
}
```

- `HostingConfig.UseProxy = true` is **mandatory behind Caddy/Nginx/Cloudflare** so nopCommerce honors `X-Forwarded-Proto`/`-For` (correct HTTPS scheme, correct client IP, correct redirect URLs). Set `KnownProxies` to your proxy's container IP/subnet if you want to restrict header trust.
- `DistributedCacheConfig.DistributedCacheType` accepts `memory`, `redis`, or `sqlserver`. See §3.5 for when to use Redis vs memory.
- `CommonConfig.StaticFilesCacheControl` controls browser caching of static assets.

### 3.3 Injecting secrets via environment variables in Docker

Two complementary mechanisms:

1. **ASP.NET Core env-var override for `appsettings.json` values.** nopCommerce's host reads environment variables, so any `appsettings.json` key can be overridden using the `__` (double-underscore) section delimiter. Example in compose:

   ```yaml
   environment:
     DistributedCacheConfig__ConnectionString: "redis:6379,password=${REDIS_PASSWORD},ssl=False,abortConnect=False"
     HostingConfig__UseProxy: "true"
   ```

   This keeps the Redis password out of any committed file (it comes from the `.env` / Docker secret at runtime).

2. **The DB connection string lives in `dataSettings.json`,** which nopCommerce's data layer reads directly (it is not a standard `IConfiguration` source). Two clean patterns to avoid committing the DB password:
   - **(Recommended, simplest) Run the install wizard once** into the persisted `nop_appdata` volume. The password lives only inside the volume on the server, never in git.
   - **(Advanced / fully reproducible) Entrypoint templating:** ship `dataSettings.template.json` with `__SET_VIA_SECRET__` placeholders and an entrypoint that renders the real file from environment variables (or Docker/Swarm secrets) at container start, *only if the file doesn't already exist*. See `deploy/app/entrypoint.sh`.

> **Never commit real passwords.** Use `.env` (git-ignored) for compose interpolation, or Docker secrets / your VPS provider's secret store. The repo ships `.env.example` only.

### 3.4 Persisting nopCommerce-generated settings in containers

This is the #1 thing people get wrong. nopCommerce writes runtime state into `App_Data` (install lock, generated settings, plugin install list, data-protection keys). **Mount `App_Data` as a named volume** (`nop_appdata`) so:
- you install once and survive every redeploy,
- customers stay logged in and antiforgery tokens keep validating across restarts (data-protection keys persist),
- plugin install state persists.

Plugins go on a separate `nop_plugins` volume so you can manage them independently of config.

### 3.5 Redis cache options — and when to use them

| Mode (`DistributedCacheType`) | Use when | Trade-off |
|---|---|---|
| `memory` (in-process) | **Single app instance** (MVP/small prod) | Fastest (no network hop); but cache is per-instance and lost on restart |
| `redis` | **Multiple app instances / web-farm**, or you want a shared cache that survives app restarts, or to centralize data-protection keys/session | Slight latency vs memory; one more service to run/secure |
| `sqlserver` | SQL-Server shops wanting DB-backed distributed cache | N/A here |

**Recommendation for this project:** The stack *includes* Redis, which is the right call because it (a) lets you scale the app horizontally later without re-architecting, (b) gives a stable place for data-protection keys/session so container restarts don't log users out, and (c) keeps cache warm across app redeploys. **However, be honest about MVP:** with a *single* app instance, `memory` cache is actually faster. A reasonable path is **start with `memory` for the MVP single node and flip to `redis` the moment you add a second app instance or want cross-restart cache warmth** — the toggle is one config value. Either way, run Redis from day one (it's cheap) so the switch is instant.

Redis hardening: set `requirepass`, bind to the internal Docker network only (no published port), and (if used only as cache) leave persistence off — see §11.4.

---

## 4. Indonesia Store Configuration

### 4.1 Currency — IDR

`Configuration → Currencies`:
- Add/enable **Indonesian Rupiah**, ISO code **`IDR`**, display locale **`id-ID`**.
- **Custom formatting:** IDR has no minor unit in practice → format as `Rp #,##0` (no decimals). Set rounding to whole numbers.
- Set **Primary store currency = IDR** and **Primary exchange-rate currency = IDR** (`Configuration → Settings → Currency settings`).
- Since you sell only in IDR, **hide the currency selector** (publish only IDR, or disable "Allow customers to select currency").
- If you ever display USD for foreign buyers, configure an exchange-rate provider; otherwise keep it single-currency for simplicity.

### 4.2 Language / localization — Bahasa Indonesia + English

`Configuration → Languages`:
- Keep **English** as one language; add **Bahasa Indonesia** (culture `id-ID`, unique SEO code `id`, flag).
- Import the **Indonesian language resource pack** (download the `id-ID` resource XML from nopCommerce's official localization/translation site, then *Import resources*). This translates the storefront + emails. Expect to top up a few missing strings manually.
- Decide the **default language** (recommend Bahasa Indonesia for an Indonesian audience, English secondary) and show a clean language switcher.
- Localize **product names/descriptions, topics (legal pages), and message templates** per language (each entity has a "Localizable" tab; templates have per-language variants).
- "Supported cleanly?" — **Yes.** nopCommerce is fully multilingual with per-language SEO slugs and hreflang (see §4.7). The only manual effort is content translation.

### 4.3 Time zone

`Configuration → Settings → General settings`:
- **Default time zone = (UTC+07:00) Jakarta** (Asia/Jakarta, WIB).
- Disable "Allow customers to select time zone" (irrelevant for digital goods; keeps invoices/timestamps consistent).

### 4.4 Tax / PPN — **assumptions, with a hard "confirm with accountant" flag**

> ⚠️ **This must be confirmed with the customer's Indonesian tax consultant/accountant before launch. Do not guess on tax.** The following are *configuration-ready assumptions*, not tax advice.

- Indonesia's VAT is **PPN**. The statutory headline rate moved toward **12%** (UU HPP), but a "nilai lain" (other-value) mechanism kept the **effective rate at ~11%** for most goods/services through 2025. The **exact rate and whether it applies to commercial digital eBooks** must be confirmed.
- **Whether the business must charge PPN at all** depends on whether it is a registered taxable enterprise (**PKP**) and on the digital-goods/PMSE rules. Certain books (e.g. general educational/religious books) can be exempt; whether *commercial eBooks* qualify is **uncertain and must be confirmed**.
- **Config-ready setup:** create a **Tax category "eBooks (PPN)"**, configure a tax rate provider (`Configuration → Tax settings` → fixed-rate or by-region), and set tax-inclusive vs tax-exclusive display per Indonesian B2C norms (typically **tax-inclusive** pricing for consumers). Assign products to the eBook tax category. Leave the **rate as a single editable value** so the accountant's answer is a one-field change.
- Decide **"Prices include tax"** = true for consumer-facing IDR pricing (confirm with accountant). Configure invoice to show PPN line if required.

### 4.5 Store address, invoice/receipt, email sender, legal pages

- **Store info & address:** `Configuration → Settings → Store information` and `Configuration → Stores` — set store name, the Indonesian business address, contact details, and the **canonical store URL** (must match your domain for correct links/emails).
- **Invoice/receipt:** the **order confirmation email** + **PDF invoice** (Sales → Orders → Print/PDF) serve as the receipt. Customize the PDF header/footer/logo (`Configuration → Settings → PDF settings`) with the legal business name, address, and (if PKP) NPWP/PPN details — **confirm legal invoice requirements with the accountant**.
- **Email sender:** `Configuration → Email accounts` — create the sending account (see §14). Use a domain address (e.g. `no-reply@yourdomain.co.id`), not a free mailbox, for deliverability.
- **Legal pages:** `Content Management → Topics (pages)` — create Conditions of Use, Privacy Policy, **Refund Policy** (digital-goods specific, §7), About, Contact. Translate each per language.

### 4.6 WhatsApp support/contact

- **MVP (no code):** add a **click-to-chat** link `https://wa.me/62XXXXXXXXXX?text=...` as a floating button / header / footer / contact page item. WhatsApp is the dominant support channel in Indonesia — make it prominent and mobile-tap-friendly. This is plain HTML/JS in the theme or a topic/widget; no plugin needed.
- **Later:** transactional WhatsApp *notifications* (order paid, download ready) via a provider (Twilio, or local providers like Fonnte/Wablas/Qontak) — implemented as a **custom plugin** consuming nopCommerce order events (§14, §15).

### 4.7 SEO basics for Indonesian search

- Enable **SEO-friendly URLs**, canonical URLs, `sitemap.xml`, and **schema.org Product/Offer microdata** (`Configuration → Settings → SEO settings`).
- **hreflang**: with both `id-ID` and `en` published, nopCommerce emits per-language alternate URLs — keep slugs localized (Bahasa Indonesia slugs for the `id` language).
- Author Bahasa Indonesia titles/meta-descriptions; use Indonesian keywords (e.g. "ebook", "buku digital", genre/author terms).
- Register the site in **Google Search Console**; submit the sitemap.
- Performance is an SEO factor in Indonesia's mobile-heavy market — see §10 and §13 (mobile-first, lightweight pages, fast TTFB via cache + nearby region).
- Add structured data for the organization and breadcrumbs; ensure OpenGraph tags for nice WhatsApp/social link previews (huge for sharing in Indonesia).

---

## 5. Digital eBook Product Configuration (exact admin steps)

All steps are in `Catalog → Products → Add new` (or edit). The fields below are the ones that turn a generic product into a secure digital eBook.

### 5.1 Create an eBook product
1. `Catalog → Products → Add new`.
2. **Product type = Simple product.** Product name, full/short description, SKU.
3. Assign **Category** (e.g. by genre) and **Manufacturer = the Author/Publisher** (nopCommerce has no native "Author" entity; model authors as **Manufacturers** or **Vendors** — see §13 author page).
4. Set **Price** in IDR; assign the **Tax category** from §4.4.
5. Upload **cover image(s)** (Pictures tab) — optimized (§10.5).

### 5.2 Mark product as downloadable
6. On the **General/Downloadable** area, tick **"Downloadable product"** (`IsDownload`). This reveals the download fields.

### 5.3 Upload secure downloadable files
7. **Download file:** upload the primary file (e.g. the PDF). By default nopCommerce stores it in the **database** (binary), *not* in the web root — it is served only through an authorized controller (§9). 
8. **Use download URL** option exists but **do not use external public URLs** for paid files (defeats access control). Upload the file into nopCommerce.

### 5.4 Disable shipping
9. **Shipping tab → untick "Shipping enabled" / "This product is shipped."** Digital goods must not be shippable (no shipping step, no shipping cost, no address requirement driven by this product).

### 5.5 Disable warehouse / inventory tracking
10. **Inventory tab → Inventory method = "Don't track inventory."** A digital file has unlimited stock; never let stock hit zero and block sales. (If you *want* to model a limited/numbered edition, that's the exception — otherwise don't track.)

### 5.6 Download activation type (post-payment) — the critical setting
11. **Download activation type = "When order is paid."** This is the linchpin of digital fulfillment: the customer can download **only after payment status = Paid**. (The alternative, "Manually," requires staff to flip a switch per order — use only for manual/edge workflows.)

### 5.7 Limit number of downloads (recommended)
12. **Untick "Unlimited downloads"** and set **"Maximum number of downloads"** to a sane value (e.g. **5**). Generous enough for device changes/re-downloads, low enough to discourage credential-sharing-as-distribution. (See §9 anti-piracy.)

### 5.8 Download expiry (recommended-with-judgment)
13. **"Number of days before the download link expires"** — for *owned* eBooks, many stores leave this **unlimited** (lifetime access is a selling point) and rely on the download-count cap instead. If you prefer time-boxing, set e.g. **365 days**. Decide with the customer; default recommendation: **unlimited validity + capped count**.

### 5.9 Customer access from "My Account / Downloadable products"
14. No extra setting needed: paid downloadable items automatically appear under **My Account → Downloadable products** and on the order details page, each as an authorized download link. Verify the link in `My Account → Downloadable products` works post-payment (it's part of UAT, §16/§17).

### 5.10 Multiple formats (PDF, EPUB, MOBI)
nopCommerce's single "Download file" is one file per product. Options:
- **(Recommended) Bundle formats into one ZIP** (`book-title.zip` containing PDF+EPUB+MOBI) as the single download. Simple, one purchase, all formats. Note the formats in the description.
- **Associated/variant products** if you genuinely want to sell/track formats separately (more admin overhead; usually unnecessary for eBooks).
- A **custom "multiple files" plugin** if you need per-format download buttons with independent counts — only if a real requirement (§15).
> Recommendation: **ZIP bundle** for MVP.

### 5.11 Sample / free preview downloads
15. **"Sample download" → upload a sample file** (e.g. first chapter PDF). Samples are served via a **public** sample endpoint by design (free preview) and show a "Download sample" button on the product page. Keep samples deliberately small and watermarked "SAMPLE."
- For a **fully free eBook**, set price = 0; nopCommerce will still require the order/account flow (good for capturing the customer and applying the same access rules).

### 5.12 Preorder / delayed-release eBooks
16. **"Available for pre-order" + "Pre-order availability start date"** lets you list a title before release; the cart shows pre-order. **However**, for a *downloadable* pre-order, the file may not exist yet — so:
- Either set **Download activation = Manually** and activate downloads when the file is ready, or
- Upload a placeholder and **swap the file on release** (§12.2), then notify buyers.
- Use `availableStartDateTimeUtc` / `availableEndDateTimeUtc` to control when the product is purchasable.
> Supported, with the caveat that you must manage *when the file becomes downloadable* for pre-orders.

---

## 6. Checkout & Customer Account Rules

### 6.1 Disable guest checkout for digital eBooks — **yes**
`Configuration → Settings → Order settings`:
- **"Allow anonymous checkout" / guest checkout = OFF.** Digital fulfillment is tied to an account so the buyer can re-download from *My Account* and you have a clear identity per license (important for watermarking & support).

### 6.2 Force account creation/login before download
- With guest checkout off + **`Configuration → Settings → Customer settings → User registration type = "Standard"`**, every buyer has an account.
- Set **`Order settings → "Validate user when downloading downloadable products" = ON`** (`DownloadableProductsValidateUser`). This forces the download endpoint to require the **logged-in owner** of the order — anonymous/holder-of-link access is refused (§9). This is the single most important anti-leak toggle.

### 6.3 Forgotten passwords & customer support
- Built-in **password recovery** (email reset link) — ensure the *password reset* message template is enabled and email works (§14).
- Provide WhatsApp + email support (§4.6) for "can't log in / lost access" cases; staff can resend/reset from admin (§12).

### 6.4 Order status / payment status flow for digital fulfillment
```
Order placed  →  Payment status: Pending
   │
   ├─(payment confirmed via gateway webhook / manual mark-as-paid)
   ▼
Payment status: PAID  ──►  downloads ACTIVATE (because activation = "When order is paid")
   │
   ▼
Order status: Complete  (no shipping step for digital)
```
- Because there is no shipping, configure the workflow so a paid, digital-only order moves cleanly to **Complete** (nopCommerce auto-completes orders with nothing left to ship). Downloads work as soon as **Paid**, even before "Complete."
- For **manual bank transfer**, the order sits at **Pending** until staff verify the transfer and **mark it Paid** → downloads then activate automatically.

### 6.5 Recommended email templates
Enable and localize (§14): **Order placed**, **Order completed/paid**, and a clear **"Your eBook is ready to download"** message that links to *My Account → Downloadable products*. Customize the order-completed/"download instructions" template to spell out: how to access downloads, format notes (PDF/EPUB/MOBI), device tips, and support contact.

---

## 7. User Agreements, Terms & Refund Policy

### 7.1 Enforce terms before purchase
`Configuration → Settings → Order settings`:
- **"Terms of service enabled"** on the cart and/or order-confirm page → forces a checkbox tied to the **"Conditions of use"** topic before checkout completes.

### 7.2 Enforce a per-eBook license agreement before download
On each downloadable product (§5):
- **"Has user agreement" = ON** + paste the **eBook license text**. nopCommerce then requires the customer to **accept the agreement before the download proceeds** — a per-product, per-download consent gate. Ideal for license enforcement.

### 7.3 Recommended wording sections (digital eBook terms)
Draft with the customer's lawyer; structure to cover:
- **Personal, non-commercial use only** — license to read, not ownership of the work.
- **No redistribution / no file-sharing** — may not copy, upload, share, or distribute the file or its contents.
- **No resale / no sublicensing.**
- **No circumvention** of any watermark/DRM; watermark identifies the licensee.
- **Refund rules for digital goods** — e.g. *no refunds once the file has been downloaded/accessed*, with explicit exceptions (corrupt/wrong file, duplicate charge); a short pre-download cancellation window if you choose to offer one. **Confirm against Indonesian consumer-protection norms.**
- **Copyright / IP ownership** — all rights reserved to the author/publisher; license is revocable on breach.
- **Liability/warranty disclaimer** and **governing law (Indonesia)** + dispute resolution.

### 7.4 Where these are configured
- **Topics (pages):** `Content Management → Topics` → *Conditions of Use*, *Privacy Policy*, *Refund Policy* (each localized per language).
- **Checkout enforcement:** `Order settings → Terms of service`.
- **Per-product download consent:** product → *Has user agreement* + agreement text.
- Link Refund/Terms/Privacy in the **footer** and the **order-completed email**.

---

## 8. Payment Gateway Strategy for Indonesia

### 8.1 Comparison (from a nopCommerce implementation lens)

| Option | nopCommerce plugin status | Methods covered | Implementation effort | Notes |
|---|---|---|---|---|
| **Manual bank transfer** | **Built in** ("Check / Money Order" — rename to *Bank Transfer*) | Manual VA/transfer; QRIS-by-screenshot | **Zero code** | Buyer transfers, uploads/sends proof, staff mark Paid → downloads activate. Perfect MVP. Manual reconciliation = labor + delay. |
| **QRIS** | No native plugin | QRIS (all Indonesian wallets/banks via one QR) | Via Midtrans/Xendit | QRIS is *delivered through* an aggregator (Midtrans/Xendit), not a standalone nopCommerce plugin. |
| **Midtrans** | **No official nopCommerce plugin** (official plugins target WooCommerce/Shopify/Magento) | **Snap** = QRIS + Virtual Account (BCA/BNI/BRI/Permata/Mandiri) + e-wallets (GoPay/ShopeePay/etc.) + cards — *one integration* | **Custom plugin** (moderate); Snap is redirect/popup so PCI scope is minimal | **Best all-rounder for ID.** Well-documented REST/Snap API; mature; GoPay-native. |
| **Xendit** | **No official nopCommerce plugin** | Invoices/VA/QRIS/e-wallets/cards; strong disbursement/split for marketplaces | **Custom plugin** (moderate) | Clean API/docs; great if you later need payouts/marketplace splits. |
| **DOKU** | **No official nopCommerce plugin** | VA/QRIS/e-wallets/cards | **Custom plugin** | Established local player; fine alternative to Midtrans. |
| **PayPal** | **Official nopCommerce plugin** (PayPal Commerce/Standard) | Cards + PayPal balance (international) | **Config only** | Useful for *foreign* buyers of English eBooks; **not** a primary Indonesian rail (low local adoption, FX/IDR friction). |
| **Stripe** | **Off-the-shelf marketplace plugins for 4.90** (nopStation, nopCommercePlus, foxnetsoft) | Cards + Apple/Google Pay; 135+ currencies; **NOT** QRIS / local VA / Indonesian e-wallets | **Config only** (buy + configure, ~$0–19) | ⚠️ **Invite-only/preview for Indonesian merchants** — an Indonesia-only entity generally **cannot open a Stripe account**. Card-first → underserves Indonesian buyers. Viable *only* with a foreign (e.g. Singapore) entity and/or international card buyers. |

> **Reality check (the project's #1 risk):** unlike WordPress/Shopify, **there is no turnkey Indonesian gateway plugin for nopCommerce.** Plan a **custom `IPaymentMethod` plugin**. The good news: nopCommerce's payment plugin contract is well-defined, these gateways expose clean REST APIs (and Snap/redirect flows keep you out of PCI scope), and it's a contained, well-bounded build — *not* a core change.

> **Confirmed scope for this project (entity = Indonesia-only; buyers = Indonesian consumers in IDR):** **Stripe and PayPal are *not* viable as the primary rail** — Stripe won't onboard an Indonesia-only merchant, and neither serves QRIS/VA/e-wallets, which is how Indonesians pay. The decided path is **manual QRIS/bank transfer (MVP) → custom Midtrans plugin (production)**. Revisit Stripe/PayPal only if a Stripe-supported (e.g. Singapore) entity is added or you start targeting international card buyers.

### 8.2 Best MVP payment approach
**Built-in Manual method, relabeled "Transfer Bank / QRIS (verifikasi manual)."** Show bank account + a static QRIS image; buyer pays and sends proof via WhatsApp; staff **mark order Paid** → downloads auto-activate (§6.4). Zero code, launch immediately, validate demand. Trade-off: manual effort and fulfillment delay (minutes–hours).

### 8.3 Best production payment approach
**Custom Midtrans (Snap) plugin.** One integration delivers **QRIS + Virtual Account + e-wallets + cards** with **automatic** payment confirmation via webhook. This removes manual reconciliation and gives instant download activation. Add **Xendit** later if you need payouts/splits. (International card options like Stripe/PayPal only become relevant if you add a foreign entity — see §8.1.) (Decision matrix: §15.)

### 8.4 Downloads activate only after confirmed payment — how
- Product setting **Download activation = "When order is paid"** (§5.6).
- The payment plugin (Midtrans) handles the gateway's **server-to-server notification/webhook**: on a verified `settlement/capture` status, it calls nopCommerce's order-processing service to **mark the order Paid**. nopCommerce then activates downloads automatically.
- **Verify webhooks server-side** (signature/`order_id`+`status_code`+`gross_amount`+server-key hash for Midtrans) — *never* trust client-side redirect params alone. For manual transfer, "confirmed payment" = staff verification → mark Paid.

---

## 9. Security for eBook Downloads

### 9.1 How nopCommerce protects downloadable products by default
- Paid files are **not** static, publicly-linkable assets. They are served through an **authorized controller action** (`/download/getdownload/{orderItemGuid}`), which checks: the order item exists, the download is **activated**, the order's **payment status allows download**, the **download count** isn't exceeded, the link **hasn't expired**, and (if enabled) the **requesting user owns the order**.
- By default, the file bytes live in the **database** (`Download.DownloadBinary`), i.e. **outside the web root entirely** — there is no filesystem URL to leak.
- **Samples** (§5.11) are intentionally public (free preview) via a separate endpoint.

### 9.2 Are paid files public URLs or authenticated logic?
**Authenticated/authorized logic — not public URLs.** This is a core strength of nopCommerce for digital goods and a primary reason it fits this project.

### 9.3 Best practices (apply all)
- **Require login for downloads:** guest checkout OFF (§6.1) + **`Order settings → Validate user when downloading downloadable products` = ON** (§6.2). This rejects anyone who isn't the logged-in order owner, even if they have the link.
- **Disable anonymous download access:** consequence of the above; verify in UAT by trying a download while logged out / as another user.
- **Avoid public static file links:** never use the "download URL" field for paid products; keep files in the DB (or, if on disk, **under `App_Data`/outside `wwwroot`** so they're never statically served).
- **Store files outside the public web root:** DB storage (default) achieves this. If you switch to disk for large files, store under a non-served path and keep serving through the controller (or a storage plugin that proxies through auth).
- **Expiring links (custom flows):** if you ever build custom direct links (e.g. a CDN-signed URL for very large files), make them **short-lived signed URLs** (HMAC + expiry + one-time/limited use) — never permanent.
- **Rate-limit suspicious download behavior:** at the reverse proxy, rate-limit `/download/*` per IP/account (e.g. N requests/min) to blunt scraping/credential-sharing abuse (`deploy/Caddyfile` shows a sample). Combine with the per-product **download-count cap** (§5.7).
- **Audit download logs:** track download activity (orders → order item download count; add a custom audit/event log via a plugin if you need per-download IP/time records). Review outliers (one account, many IPs, hitting the cap fast).

### 9.4 Customer-specific PDF watermarking — worth it?
**Yes for PDF, as a deterrent — recommended as a Phase-6 enhancement, implemented as a plugin (no core changes).**
- **Why:** the realistic leak vector for eBooks isn't the protected URL; it's a legitimate buyer re-sharing the file. **Per-buyer watermarking** ("Licensed to {Name} — {email} — Order #{id}") makes shared copies traceable and psychologically discourages sharing. It's "social DRM," not unbreakable DRM — and that's the right, low-friction posture (hard DRM frustrates honest buyers).
- **How (plugin design):**
  - Hook the download pipeline (event consumer / override the download retrieval for downloadable order items) so the PDF is **stamped with the buyer's identity at download time**, then streamed.
  - For performance, **generate the watermarked copy on first paid download and cache it** keyed by order-item (regenerate if the master file changes), rather than stamping on every request.
  - Use a PDF library for stamping (e.g. **PDFsharp/MigraDoc** — MIT-friendly; or a commercial lib like Aspose.PDF for richer features; iText7 is AGPL/commercial — mind licensing).
  - Keep it a **separate plugin** so it's optional, upgrade-safe, and doesn't touch core.
- **EPUB/MOBI:** harder to watermark robustly; apply lightweight **social DRM** (licensee line in metadata/colophon/footer) later. Prioritize **PDF watermarking first**.
- **MVP stance:** capped downloads + login-required + license agreement is enough to launch. Add watermarking once volume/value justifies the build.

---

## 10. Performance Tuning (digital-only store)

### 10.1 Redis caching
- Single node: `memory` cache is fastest; switch to `redis` for multi-instance or cross-restart warmth (§3.5).
- Tune `CacheConfig.DefaultCacheTime` (minutes) up for a mostly-static catalog (eBooks change rarely) — e.g. 60+; clear cache on deploy/content changes (nopCommerce invalidates on entity changes automatically).
- Secure Redis with a password + internal-only networking.

### 10.2 PostgreSQL indexes / maintenance / autovacuum / backup
- **Indexes:** nopCommerce + its migrations create the necessary indexes. Add targeted indexes only if `pg_stat_statements` shows a hot, unindexed query (rare for a small catalog).
- **Memory params (4 GB box example):** `shared_buffers ≈ 1GB`, `effective_cache_size ≈ 2.5GB`, `work_mem ≈ 16–32MB`, `maintenance_work_mem ≈ 256MB`. Scale with RAM (`shared_buffers` ~25 %, `effective_cache_size` ~70–75 %).
- **Autovacuum:** keep enabled (default). nopCommerce's churny tables (cart, logs, customers/guests, scheduled-task state) benefit; consider more aggressive autovacuum on high-write tables if you keep verbose logging.
- **Extensions/stats:** enable `pg_stat_statements` to find slow queries.
- **Backup:** see §11 (logical `pg_dump` nightly + optional WAL/PITR).

### 10.3 Static asset optimization
- Enable nopCommerce **bundling + minification** (WebOptimizer) for CSS/JS.
- Reverse proxy: **gzip/brotli** compression + long `Cache-Control` for static files (`/css`, `/js`, `/images` thumbs). `deploy/Caddyfile` includes this.
- Serve over **HTTP/2/HTTP/3** (Caddy default).

### 10.4 CDN — yes for assets, NOT for paid eBook files
- Put **Cloudflare** (or similar) in front for **images, CSS, JS, fonts** — big win for Indonesian mobile users on variable networks.
- **Do NOT let the CDN cache paid downloads.** `/download/*` is dynamic, authorized, per-user — bypass cache for it (Cloudflare cache rule: bypass `/download/*`). Caching a paid file at the edge would defeat access control. (Samples are fine to cache.)
- nopCommerce 4.90's **Cloudflare Images** integration can offload product-image resizing/delivery — useful for cover-heavy catalogs.

### 10.5 Image optimization for product covers
- Configure **picture sizes/thumbnails** (`Media settings`) to the sizes the theme actually uses; avoid serving full-res covers as thumbnails.
- Prefer **WebP/AVIF** where supported (via Cloudflare Polish/Images or pre-optimized uploads).
- **Lazy-load** below-the-fold covers; set explicit dimensions to avoid layout shift (mobile UX + SEO).

### 10.6 Background / scheduled tasks
`System → Schedule tasks`:
- Keep **Send emails** frequent (e.g. every 60s) so order/download emails go out promptly.
- **Delete guests**, **Clear log** (essential if logging to DB — §10.7), **rebuild sitemap**, **keep-alive** — tune intervals; disable keep-alive if the proxy/health-check already keeps the app warm.
- **Multi-instance caution:** scheduled tasks run in-process; if you scale to multiple app instances, ensure tasks don't double-run (use a single "worker" instance or nopCommerce's task locking).

### 10.7 Logging level & retention
- Default DB logging can bloat the `Log` table. **Set minimum log level to Warning/Error in production.**
- **Containers:** prefer **Serilog → stdout** (JSON) so Docker/your log driver captures logs centrally; keeps the DB lean. Ship to a log service if available.
- Run the **Clear log** scheduled task; retain **30–90 days**.

### 10.8 DB connection pool
- Npgsql pooling is on by default. Set **`Maximum Pool Size`** in the connection string with headroom under PostgreSQL `max_connections` (default 100). For a single app instance, `Maximum Pool Size=100` vs PG `max_connections=100` is fine; if you add instances or PgBouncer, size accordingly.
- **PgBouncer** (transaction pooling) helps at scale/many instances — but disable Npgsql server-side prepared statements (`No Reset On Close`/`Max Auto Prepare=0`) to avoid prepared-statement conflicts. Not needed at MVP.

---

## 11. Backup, Restore & Disaster Recovery

### 11.1 PostgreSQL backup
- **Logical, nightly:** `pg_dump -Fc` (custom format) via the **backup sidecar**, retained locally + **pushed off-site** to S3-compatible object storage. (`prodrigestivill/postgres-backup-local` does scheduled dumps + retention out of the box.)
- **PITR (production/growth):** add **WAL archiving** or use **pgBackRest** for point-in-time recovery if RPO must be minutes, not a day.

### 11.2 File / download backup
- **If downloads are in the DB (recommended/default):** they're inside the `pg_dump` — one backup covers catalog + files. (Watch DB size as the catalog grows.)
- **If downloads are on disk/object storage:** back up the `nop_downloads` volume / bucket on the same schedule; verify object-store versioning.

### 11.3 App_Data / config / plugin backup
- Back up the **`nop_appdata`** volume (`dataSettings.json`, `appsettings.json`, data-protection keys, install state) and **`nop_plugins`** and **`nop_images`** volumes. A simple `tar` of the named volumes to off-site storage nightly is enough; these change rarely.

### 11.4 Redis persistence decision
- **Recommendation: run Redis as a pure cache with persistence OFF.** Losing the cache is harmless (it rebuilds). This keeps Redis simple and avoids backup scope.
- *If* you later use Redis for data-protection keys/session, **don't rely on Redis persistence for those** — instead keep data-protection keys on the persisted `nop_appdata` volume (or DB) so a Redis flush never logs everyone out. Then Redis can stay non-persistent.

### 11.5 Restore-test checklist (run quarterly — untested backups don't exist)
- [ ] Spin up a throwaway environment.
- [ ] `pg_restore` the latest dump into a fresh PostgreSQL.
- [ ] Restore `nop_appdata` / `nop_plugins` / `nop_images` (and `nop_downloads` if on disk).
- [ ] Boot nopCommerce against the restored DB/volumes.
- [ ] Verify: admin login, a known order, **a paid eBook actually downloads**, images render, settings intact.
- [ ] Record restore time (your real RTO) and any gaps.

### 11.6 Retention policy (suggested)
- **Daily** dumps kept **7 days**, **weekly** kept **4–5 weeks**, **monthly** kept **6–12 months**. Off-site copy mandatory. Encrypt backups at rest.

### 11.7 Migrating to a larger VPS or managed DB later
- **Bigger VPS:** snapshot/restore volumes, or stand up new VPS, restore volumes + DB dump, repoint DNS. Because everything is in named volumes + a DB dump, this is a low-drama move.
- **Managed PostgreSQL:** `pg_dump` → `pg_restore` into the managed instance (pre-create `citext`), update `dataSettings.json` connection string, restart. For near-zero downtime, use **logical replication** to sync then cut over. Validate `citext`/extensions exist on the target.

---

## 12. Admin Operations

### 12.1 Uploading new eBooks
Staff with the right role (§12.6) follow §5: create product → mark downloadable → upload file (ZIP of formats) → disable shipping → don't track inventory → activation "When order is paid" → set download cap → cover image → publish. Provide a **one-page SOP** so uploads are consistent.

### 12.2 Updating an eBook file after purchase
- Edit the product → **upload a new version** of the download file. Existing buyers get the **updated file on their next download** (within their remaining count/validity). For major updates, **notify buyers** (email/WhatsApp) and consider **resetting download counts** (below) so they can re-fetch.

### 12.3 Revoke or reissue downloads
- **Reissue / extend:** `Sales → Orders → [order] → product/download` — staff can **re-activate downloads** and **reset the download count** for that order item (e.g. customer changed devices, hit the cap legitimately, or file was updated).
- **Revoke:** for abuse/chargeback, set the order's **payment status to Refunded/Voided** and/or **deactivate the download** so access stops. (Also relevant for fraud handling.)

### 12.4 Customer support requests
- Channels: WhatsApp (primary, §4.6) + email. Common asks: can't log in (password reset / staff resend), hit download cap (reset count), wrong file/format (re-upload + reissue), refund (per §7 policy). Keep a support SOP mapping each ask to the admin action.

### 12.5 Visibility: orders, payments, downloads, failed payments, abandoned carts
- **Orders & payments:** `Sales → Orders` (filter by payment/order status; see Paid/Pending/Refunded).
- **Downloads:** per-order item download status/count on the order page; build a **custom download-audit report (plugin)** if you need per-download IP/time analytics (§9.3).
- **Failed/abandoned:** `Sales → Abandoned carts` (carts that never converted — follow up / discount), and Pending orders that never paid (manual-transfer no-shows). **Reports** (`Reports → Sales/Bestsellers/Customer`) for revenue and top titles.

### 12.6 Recommended admin roles & permissions
`Customers → Customer roles` + **ACL** (`Configuration → Access control list`):
- **Administrator** — full access (founder/lead only).
- **Store manager / Catalog editor** — manage products, content, orders; **no** access to system config, payment credentials, or user management.
- **Support agent** — view/edit orders, reset downloads, customer support; **no** catalog pricing or system settings.
- **Finance (optional)** — orders/reports/refunds read + refund action.
- Principle: least privilege; only the founder holds the full Administrator + server/SSH + payment-gateway dashboard credentials. Enforce strong passwords; enable 2FA where available.

---

## 13. Theme & UX

### 13.1 Theme strategy
- **Recommendation: start from the default responsive theme + a lightweight child/custom theme overlay** (logo, colors, fonts, product-page tweaks). It's mobile-ready, maintained with core (upgrade-safe), and fastest to launch.
- **Purchased theme** (e.g. nop-templates) only if you need a polished, distinctive look quickly and you've confirmed **4.90 + PostgreSQL** compatibility.
- **Fully custom theme** only when brand differentiation clearly justifies the cost/maintenance — usually *not* for MVP. Whatever you pick, **don't modify core**; theme via the theming/override mechanism.

### 13.2 Required pages
Home · eBook catalogue (category/genre listing + search/filter) · Product detail (eBook) · **Author page** (model authors as Manufacturers or Vendors → gives you a ready-made author landing page listing their titles) · Checkout · **My downloads** (My Account → Downloadable products) · Terms · Privacy · **Refund policy** · Contact/WhatsApp support. (Legal pages = Topics, §7.4.)

### 13.3 Mobile-first UX for Indonesian buyers
- **Mobile-first, lightweight, fast** — most traffic is mobile, often on variable networks. Minimize JS/CSS, compress images (§10.5), lazy-load.
- **Frictionless checkout:** few steps, big tap targets, **QRIS/VA prominent** at payment, **WhatsApp support visible** throughout.
- Clear **"how to read your eBook"** guidance and obvious **download access** post-purchase.
- Bahasa Indonesia by default; price as **Rp** with no decimals.

### 13.4 Product page layout for eBooks
Cover image (prominent) · title + author (linked to author page) · **price in Rp** · **format badges (PDF/EPUB/MOBI)** · **"Download sample"** button · short synopsis (expandable) · "What you get / formats / file size" · license summary + link to full terms · trust signals (secure payment, instant access after payment) · **Add to cart / Buy now**. Below: full description, author bio, related titles. Keep above-the-fold tight for mobile.

---

## 14. Email & Notification Setup

### 14.1 SMTP provider recommendations
- **Transactional ESP (recommended):** **Amazon SES** (cheap, reliable, SG region), **Postmark** (best deliverability for transactional), or **Brevo/Mailgun**. Use **SMTP relay** credentials in nopCommerce's Email account.
- Avoid sending from a self-hosted SMTP on the VPS (poor deliverability, IP reputation pain).

### 14.2 SPF / DKIM / DMARC
- **SPF:** DNS TXT authorizing the ESP to send for your domain.
- **DKIM:** add the ESP's DKIM CNAME/TXT records (signing).
- **DMARC:** start `p=none` (monitor), tighten to `quarantine`/`reject` once SPF+DKIM pass consistently. This is essential for inbox placement (and WhatsApp/social link trust).

### 14.3 Required transactional emails (enable + localize per language)
- **Account confirmation / welcome** (registration).
- **Order confirmation** (order placed).
- **Payment received** (payment status → Paid).
- **Download available** ("your eBook is ready" — customize order-completed template with download link + instructions, §6.5).
- **Password reset.**
Configure under `Content Management → Message templates`; set the **Email account** sender; test each (§17).

### 14.4 WhatsApp notifications — MVP or later?
- **MVP: click-to-chat support link only** (§4.6) — no transactional WhatsApp.
- **Later: transactional WhatsApp** (order paid / download ready) via a provider (Twilio or local Fonnte/Wablas/Qontak) as a **custom plugin** on order events (§15). High value in Indonesia, but not required to launch — email covers fulfillment initially.

---

## 15. Plugin vs Core-Change Decision Matrix

| Requirement | Admin only? | Existing plugin? | Custom plugin? | Core change? | Recommendation |
|---|---|---|---|---|---|
| **Indonesian payment gateway** (Midtrans/Xendit/DOKU) | No | No official nopCommerce plugin | **Yes** | **No** | Build a **custom `IPaymentMethod` plugin** (Midtrans Snap recommended). Contained, no core edits. |
| **QRIS** | No | No (delivered via aggregator) | **Yes** (part of Midtrans/Xendit plugin) | No | Comes "free" inside the Midtrans/Xendit plugin's Snap/invoice flow. MVP: static QRIS image + manual verify. |
| **PDF watermarking** (per-buyer) | No | Possibly niche/commercial | **Yes (recommended)** | No | Custom plugin hooking the download pipeline; stamp+cache per order item (§9.4). Phase 6. |
| **WhatsApp order notification** | No | Provider-specific maybe | **Yes** | No | Custom plugin consuming order-paid event → provider API. Post-MVP. |
| **eBook license enforcement** | **Yes** (mostly) | No | Optional | No | Use built-in **per-product user agreement** + **download cap** + **validate-user** (§5–§7). Custom only for advanced/seat licensing. |
| **Custom download expiry rules** | **Partly** (built-in count + days) | No | Only if rules are non-standard | No | Built-in **max-downloads + expiry-days** covers most needs. Custom plugin only for unusual logic (e.g. signed time-boxed links). |
| **Bilingual storefront** (ID/EN) | **Yes** | Language pack (resources) | No | No | **Admin config + import language pack** (§4.2). No plugin/core change. |
| **Invoice customization** (PPN/NPWP layout) | **Yes** (PDF settings) | No (light needs) | Only for heavy redesign | No | Use **PDF settings + localized templates**. Custom plugin only if the accountant requires a bespoke legal invoice. |
| **Analytics integration** (GA4/Meta Pixel) | **Yes** (widget zones / header scripts) | Yes (GA/marketing plugins) | Rarely | No | Add via **widget/script settings** or a marketing plugin. No core change. |

**Pattern:** everything maps to **admin config or a self-contained plugin**. The only *required* custom build is the **payment plugin**. **No core source changes are warranted** for any requirement — preserve this to keep the .NET 10 / nopCommerce 5.0 upgrade cheap (§2.2).

---

## 16. Implementation Phases

### Phase 0 — Technical spike
- **Goal:** de-risk the unknowns before committing.
- **Tasks:** stand up 4.90 + PostgreSQL + Redis in Docker locally; run the install wizard; confirm `citext`/collations; create one downloadable test product; **prototype the Midtrans Snap flow in sandbox** (the riskiest item); confirm a language pack imports.
- **Output:** a working throwaway store + a proven Midtrans sandbox payment + a go/no-go note on payment effort.
- **Risks:** payment plugin effort larger than expected; PostgreSQL/plugin edge cases.
- **Acceptance:** install completes on PostgreSQL; test product downloads after a mock "paid"; Midtrans sandbox returns a successful settlement webhook.

### Phase 1 — Base nopCommerce + PostgreSQL + Docker
- **Goal:** reproducible, persistent base deployment.
- **Tasks:** finalize `docker-compose.yml`, volumes (`nop_appdata`/`plugins`/`images`/`downloads`/`pg_data`), Caddy + TLS, env/secret handling, backup sidecar.
- **Output:** the store running on the VPS over HTTPS, install state persisted across redeploys.
- **Risks:** volume misconfiguration (data loss), proxy/HTTPS mistakes.
- **Acceptance:** redeploy the app container → no re-install, users stay logged in, HTTPS + security headers pass.

### Phase 2 — Store configuration
- **Goal:** Indonesia-ready storefront settings.
- **Tasks:** IDR currency, ID/EN languages + pack, Asia/Jakarta TZ, tax category (rate TBD w/ accountant), store info, legal Topics, email account + templates, SEO settings, WhatsApp link.
- **Output:** a fully localized, legally-scaffolded store (minus payment).
- **Risks:** tax assumptions wrong (flag for accountant); translation gaps.
- **Acceptance:** storefront renders in Bahasa Indonesia, prices show `Rp`, emails send, legal pages live.

### Phase 3 — eBook product setup
- **Goal:** the digital catalog model.
- **Tasks:** category/author taxonomy; create real eBook products per §5 (downloadable, no shipping, no inventory, activation on paid, capped downloads, sample, ZIP-bundled formats, user agreement).
- **Output:** catalog of purchasable eBooks with samples.
- **Risks:** inconsistent product setup → publish an SOP.
- **Acceptance:** a buyer can purchase and (post-mock-payment) download; sample works; access shows under My Account.

### Phase 4 — Payment integration
- **Goal:** real Indonesian payments with auto-activation.
- **Tasks:** MVP = configure manual Bank Transfer/QRIS; Production = build/test the **Midtrans plugin** (Snap + verified webhook → mark Paid). Optionally PayPal for international.
- **Output:** live payment with downloads activating only after confirmed payment.
- **Risks:** webhook verification bugs (the critical correctness issue); refund/void handling.
- **Acceptance:** sandbox→production test buys via QRIS + VA succeed; webhook marks Paid; download activates; refund path works.

### Phase 5 — Theme / UX
- **Goal:** clean, fast, mobile-first storefront.
- **Tasks:** theme overlay (brand), product-page layout (§13.4), author pages, WhatsApp button, mobile checkout polish.
- **Output:** branded, mobile-optimized store.
- **Risks:** theme/plugin compatibility with 4.90; performance regressions from heavy themes.
- **Acceptance:** Lighthouse mobile passes targets; checkout smooth on a real phone.

### Phase 6 — Security hardening
- **Goal:** lock down downloads and the platform.
- **Tasks:** validate-user-on-download ON, guest checkout OFF, security headers (HSTS/CSP/X-Content-Type/Referrer-Policy), `/download/*` rate-limiting, least-privilege admin roles, 2FA, secrets review, **(optional) PDF watermarking plugin**.
- **Output:** hardened store; (optional) watermarked PDFs.
- **Risks:** CSP breaking scripts; over-tight rate limits hurting legit users.
- **Acceptance:** logged-out/other-user download attempts fail; headers scan clean; watermark (if built) stamps correctly.

### Phase 7 — Performance & backup
- **Goal:** fast and recoverable.
- **Tasks:** cache tuning, PostgreSQL params, asset/CDN setup (bypass `/download/*`), scheduled-task tuning, logging level, **backup sidecar + off-site + first restore test**.
- **Output:** tuned store with proven backups.
- **Risks:** untested backups; CDN caching a paid file (config error).
- **Acceptance:** restore test passes (§11.5); CDN never serves a paid download; pages fast.

### Phase 8 — UAT
- **Goal:** business sign-off.
- **Tasks:** full buyer journeys in ID/EN on mobile (browse→buy via QRIS/VA→download→re-download→hit cap→support); admin journeys (upload, reissue, refund); email/notification checks.
- **Output:** signed-off test report.
- **Risks:** edge cases in payment/download; localization gaps.
- **Acceptance:** all critical journeys pass on a real Indonesian mobile device/network.

### Phase 9 — Production launch
- **Goal:** go live safely.
- **Tasks:** DNS cutover, production payment keys, final backup + restore verification, monitoring/alerts, run the launch checklist (§17), staff trained on SOPs.
- **Output:** live store.
- **Risks:** last-mile DNS/TLS/payment-key issues; first-week support load.
- **Acceptance:** §17 checklist fully green; first real order completes end-to-end.

---

## 17. Launch Checklist (go-live)

- [ ] **Domain** resolves (apex + www redirect), canonical store URL set in nopCommerce.
- [ ] **SSL/TLS** valid (Let's Encrypt), HTTPS forced, HSTS on, auto-renew confirmed.
- [ ] **Payment test:** real low-value purchase via **QRIS** and **Virtual Account** (and PayPal if enabled) succeeds; webhook marks **Paid**.
- [ ] **Email test:** account confirmation, order confirmation, payment received, **download-available**, password reset all deliver (check spam; SPF/DKIM/DMARC pass).
- [ ] **Downloadable product test:** buy → download works; **My Account → Downloadable products** shows it; **re-download** works within cap.
- [ ] **Refund/cancellation test:** refund flow updates status and (per policy) revokes/limits download.
- [ ] **Backup/restore test:** latest backup **restored** into a clean env and a paid eBook downloaded there (§11.5).
- [ ] **Admin access:** roles least-privilege, 2FA on admin, founder holds full creds; no default/sample admin left.
- [ ] **Security headers** present (HSTS, X-Content-Type-Options, Referrer-Policy, CSP), `/download/*` rate-limited, validate-user-on-download ON, guest checkout OFF, sample data removed.
- [ ] **Error pages** (404/500) themed and not leaking stack traces (prod env).
- [ ] **Analytics** (GA4 + any pixel) firing on key events.
- [ ] **Legal pages** live & linked: Terms, Privacy, **Refund**, Contact; per-product license agreement on.
- [ ] **Indonesian buyer test flow on mobile:** full journey in Bahasa Indonesia on a real phone/mobile network — browse → buy via QRIS → download → WhatsApp support reachable.

---

## 18. Risk Register

| # | Risk | Likelihood | Impact | Mitigation | Owner |
|---|---|---|---|---|---|
| R1 | **No official ID payment plugin** → custom build needed | **High** | High | Spike Midtrans Snap in Phase 0; launch on manual transfer; budget the custom plugin; keep PayPal for intl | Tech lead / dev |
| R2 | **Download file leakage** (sharing or misconfig) | Medium | High | DB-stored files + validate-user-on-download + guest checkout off + download cap + rate-limit + **PDF watermarking**; never use public URLs | Tech lead |
| R3 | **Poor mobile checkout** (Indonesian mobile-first) | Medium | High | Mobile-first theme, minimal steps, QRIS prominent, real-device UAT on local networks | UX / tester |
| R4 | **Failed email delivery** (downloads/receipts not arriving) | Medium | High | Reputable ESP (SES/Postmark) + SPF/DKIM/DMARC + deliverability test in UAT; WhatsApp as backup channel | Ops |
| R5 | **No backup testing** (backups that don't restore) | Medium | **Critical** | Backup sidecar + off-site + **quarterly restore test** (§11.5) as a hard gate | Ops |
| R6 | **Customers share eBooks publicly** | Medium | Medium | Per-buyer **watermarking** (social DRM) + license agreement + download cap; monitor/audit; takedown process | Business + tech |
| R7 | **PostgreSQL misconfiguration** (citext, perms, params) | Medium | Medium | Pre-create `citext`; tested install runbook (§2.5); tuned params (§10.2); pin PG version | DBA / tech lead |
| R8 | **Docker volume loss** (App_Data/DB/downloads) | Low–Med | **Critical** | Named volumes + provider snapshots + off-site backups; never store state in container layer; document volumes | Ops |
| R9 | **.NET 9 runtime EOL** (STS support closing ~mid-2026) | High (time-based) | Medium | Patched base images; app not internet-facing directly; **planned upgrade to nopCommerce 5.0 / .NET 10 LTS** (§2.2) | Tech lead |
| R10 | **Tax (PPN) misconfiguration** | Medium | Med–High | **Confirm rate & applicability with accountant**; single-field-editable tax category; correct invoices before launch | Owner + accountant |
| R11 | **Payment webhook spoofing / mis-marking Paid** | Medium | High | Server-side signature/amount verification; idempotent handling; never trust client redirect params (§8.4) | Dev |

---

## 19. Final Recommendation

**Is nopCommerce + PostgreSQL suitable for this eBook store? — Yes, strongly.** The downloadable-product model (file stored in the DB, served only through an authorized controller, activated on payment, with per-product license agreements, download caps, and a My-Account download area) maps almost one-to-one onto a secure digital-eBook business — at zero license cost, self-hosted on Docker, on a database (PostgreSQL) that is cheap, Linux-native, and well-supported in 4.90.

**Configure only (no code):**
- Indonesia localization (IDR, ID/EN + language pack, Asia/Jakarta, SEO, hreflang).
- The entire eBook product model (downloadable, no shipping, no inventory, activate-on-paid, download cap, samples, ZIP-bundled formats, per-product user agreement).
- Security toggles (guest checkout off, validate-user-on-download on).
- Manual bank-transfer/QRIS payment for MVP.
- Legal pages, email templates, invoice/PDF, WhatsApp click-to-chat, analytics.

**Customize with a plugin (no core changes):**
- **Indonesian payment gateway — the one required build** (Midtrans Snap → QRIS + VA + e-wallets + cards, with verified webhook → auto-activate downloads).
- **PDF watermarking** (Phase 6, recommended deterrent).
- **WhatsApp transactional notifications** (post-MVP).
- Custom download-audit reporting / unusual licensing — only if a concrete need appears.

**Avoid:**
- **Forking/modifying nopCommerce core** — kills upgradeability and the cheap path to .NET 10.
- **Exposing paid eBook files as public/static/CDN-cached URLs.**
- **Heavyweight DRM** that punishes honest buyers (use social DRM/watermarking instead).
- **Running without a tested backup/restore.**
- **Guessing on PPN** — confirm with the accountant.
- **Treating .NET 9 as a forever runtime** — plan the 5.0 / .NET 10 LTS upgrade.

**MVP vs later:**
- **MVP:** single 2 vCPU/4 GB VPS; Docker (app + PostgreSQL + Redis + Caddy + backup sidecar); IDR/ID-EN store; eBook catalog with samples; **manual bank-transfer/QRIS**; downloads in DB; guest checkout off + validate-user-on-download; legal pages; transactional email; daily off-site backup; WhatsApp support link.
- **Later (production/growth):** Midtrans plugin (auto-activation); PayPal for intl; PDF watermarking; WhatsApp notifications; Cloudflare CDN (bypass `/download/*`); performance tuning; split/managed PostgreSQL + Redis scale-out; **nopCommerce 5.0 / .NET 10 upgrade**.

**Bottom line:** proceed with **nopCommerce 4.90.x on PostgreSQL + Redis in Docker**, launch lean on manual payments, invest the one meaningful custom-dev effort in the **Midtrans payment plugin**, keep core untouched, test your backups, and confirm tax with the accountant. This delivers a secure, low-maintenance, high-performance Indonesian eBook store with a clean growth path.

---

### Appendix A — Deploy scaffold in this repo
- `deploy/docker-compose.yml` — app + PostgreSQL + Redis + Caddy + backup sidecar, internal network, named volumes.
- `deploy/.env.example` — all secrets/config as env vars (copy to `.env`, never commit `.env`).
- `deploy/Caddyfile` — auto-TLS, security headers, gzip/brotli, `/download/*` rate-limit, static caching.
- `deploy/app/Dockerfile` — builds nopCommerce 4.90 from source on .NET 9.
- `deploy/app/entrypoint.sh` — optional headless seeding of `dataSettings.json` from secrets.
- `deploy/config/dataSettings.template.json` — PostgreSQL connection template.
- `deploy/config/appsettings.redis-snippet.json` — Redis/hosting/cache block to merge into `App_Data/appsettings.json`.

### Appendix B — Sources (verified May 2026)
- nopCommerce releases (4.90.4, 16 Mar 2026): https://github.com/nopSolutions/nopCommerce/releases
- nopCommerce repo / system requirements (.NET 9; SQL Server/MySQL/PostgreSQL): https://github.com/nopSolutions/nopCommerce
- nopCommerce technology & system requirements: https://docs.nopcommerce.com/en/installation-and-upgrading/technology-and-system-requirements.html
- What's new in 4.90 (AI, Cloudflare Images, DB collation improvements): https://www.nop-templates.com/whats-new-in-nopcommerce-490-ai-driven-enterprise-ready-ecommerce-for-2025
- nopCommerce 5.0 / .NET 10 timeline discussion: https://www.nopcommerce.com/en/boards/topic/102721/estimated-release-timeline-for-nopcommerce-50-and-net-10-support
- PostgreSQL support & `citext` caveat (issues/board): https://github.com/nopSolutions/nopCommerce/issues/4288 · https://www.nopcommerce.com/en/boards/topic/96478/installing-with-postgresql-errors-relating-to-citext-and-or-fluent-migrator
- nopCommerce payment extensions marketplace: https://www.nopcommerce.com/en/extensions?category=payment-modules
- Indonesian gateway comparison (Midtrans/Xendit/DOKU, 2026): https://albatech.id/blog/midtrans-vs-xendit-vs-doku-perbandingan-payment-gateway-indonesia-2026
- Midtrans QRIS / Snap docs: https://docs.midtrans.com/reference/qris

> **Disclaimer:** Tax (PPN) and legal/refund wording are *assumptions to confirm* with the customer's Indonesian accountant and lawyer — not professional tax/legal advice.
