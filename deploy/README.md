# Deploy — multi-tenant nopCommerce platform

One VM hosts many independent stores. Each **tenant** gets its own isolated stack
(nopCommerce app + PostgreSQL + nginx); a single **shared reverse proxy**
(`nginx-proxy` + `acme-companion`) owns ports 80/443, routes each domain to the right
tenant, and auto-provisions Let's Encrypt TLS per domain.

Full rationale lives in
[`../docs/nopcommerce-ebook-indonesia-blueprint.md`](../docs/nopcommerce-ebook-indonesia-blueprint.md).

```
                          ┌─────────────────────────────────────────┐
   :80/:443  ───────────▶ │  nginx-proxy + acme-companion (shared)   │   deploy/proxy/
                          └───────────────┬──────────────┬──────────┘
                       routes by Host →   │              │   (network: webproxy)
                    ┌──────────────────────┘              └───────────────────┐
            ┌───────▼─────────┐ tenant "acme"     ┌───────▼─────────┐ tenant "books2"
            │ nginx           │                   │ nginx           │
            │  └▶ nopcommerce │  (network:        │  └▶ nopcommerce │   deploy/customers/<slug>/
            │      └▶ postgres│   internal)       │      └▶ postgres│
            └─────────────────┘                   └─────────────────┘
```

## Layout
| Path | What |
|---|---|
| [`proxy/docker-compose.yml`](proxy/docker-compose.yml) | shared `nginx-proxy` + `acme-companion` (run once per VM) |
| [`customers/template/`](customers/template/) | per-tenant stack template (app + postgres + nginx + `nginx.conf`) |
| [`customers/<slug>/`](customers/) | a live tenant, created from the template (git-ignored `.env`) |
| [`scripts/new-customer.sh`](scripts/new-customer.sh) | provisions a new tenant end-to-end |
| [`app/Dockerfile`](app/Dockerfile) | builds the shared nopCommerce image (theme + Midtrans plugin baked in) |
| [`azure/`](azure/) | provision the host VM on Azure ([azure/README.md](azure/README.md)) |

## Prerequisites
- A Linux host with Docker + Docker Compose v2 (use [`azure/provision-vm.sh`](azure/provision-vm.sh) to get one).
- A domain per tenant, with DNS pointing at the VM (the proxy needs the name to resolve to issue TLS).

## 1. Start the shared proxy (once per VM)
```bash
cd deploy/proxy
cp .env.example .env          # set ACME_EMAIL
docker compose up -d
```
This creates the external `webproxy` network that every tenant attaches to.

## 2. Add a tenant
```bash
deploy/scripts/new-customer.sh <slug> <domain> [acme_email]
# e.g.
deploy/scripts/new-customer.sh acme acme.example.co.id ops@acme.co.id
```
The script:
1. builds the shared `nop-ebook:<version>` image **once** (reused by all tenants),
2. scaffolds `deploy/customers/acme/` from the template,
3. writes `deploy/customers/acme/.env` with a **random** `POSTGRES_PASSWORD`,
4. brings the stack up as compose project `acme` (isolated volumes/network).

## 3. DNS + TLS
Point the tenant's domain (`A`/`AAAA`) at the VM's public IP. Once it resolves, `acme-companion`
issues the certificate automatically — watch it with `docker logs -f nginx-proxy-acme`.

## 4. Install nopCommerce (once per tenant)
Browse to `https://<domain>` → install wizard:
- **Database:** PostgreSQL — Host `postgres`, Port `5432`, DB `nopcommerce`, user/password from the
  tenant's `.env`.
- Uncheck "Create sample data"; set the admin email + a strong password.

`citext` is pre-created by [`config/init-citext.sql`](config/init-citext.sql), and install state
persists in the tenant's `appdata` volume — so you install once and survive every redeploy.

## Operations
- **Per-tenant commands** — always pass the project name:
  ```bash
  docker compose -p acme logs -f nopcommerce
  docker compose -p acme restart nopcommerce
  docker compose -p acme down            # stop a tenant (keeps volumes)
  ```
- **Update the app for all tenants** — rebuild the shared image, then recreate each tenant:
  ```bash
  docker build -t nop-ebook:release-4.90.4 -f deploy/app/Dockerfile .   # from repo root
  docker compose --project-directory deploy/customers/acme -p acme up -d
  ```
- **Remove a tenant** (DESTROYS its data):
  ```bash
  docker compose -p acme down -v
  rm -rf deploy/customers/acme
  ```

## Notes / caveats
- **Cache:** each tenant runs a single app instance with in-process memory cache — no shared Redis
  in the per-tenant stack (simpler, fully isolated). Add Redis only if you scale a tenant out.
- **Sizing:** many tenants share one box, so per-tenant Postgres is tuned conservatively
  (`shared_buffers=256MB`). Rough budget ≈ 0.75–1 GB RAM per active tenant. Watch memory and move
  to a bigger VM (or split across VMs) as you add stores.
- **Backups:** each tenant runs a `db-backup` sidecar (nightly `pg_dump -Fc` with rotation, into
  the tenant's `backups` volume; retention via `BACKUP_KEEP_*` in its `.env`). ⚠️ This stays **on
  the VM** — you must still copy `/backups` off-box (object storage) and run a periodic restore
  test. A backup you've never restored doesn't exist (blueprint §11.5). To pull a tenant's latest
  dump: `docker compose -p <slug> exec -T db-backup ls /backups/last`.
- **eBook files** are stored in the database (blueprint §9), so no per-tenant file volume is needed
  for downloads — but size each tenant's Postgres accordingly.
- **Midtrans:** register each tenant's `https://<domain>/...` webhook URL in its Midtrans dashboard.

## Post-deploy QA
Run the smoke test against a tenant once it's installed:
```bash
deploy/qa/smoke.sh https://<domain> --product /your-ebook-seo-name --category /c/your-genre --webhook
```
See [`qa/QA-CHECKLIST.md`](qa/QA-CHECKLIST.md) for the manual visual/functional/security passes.
