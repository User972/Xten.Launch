# Deploy — quick start

Runnable scaffold for the nopCommerce eBook store. Full rationale lives in
[`../docs/nopcommerce-ebook-indonesia-blueprint.md`](../docs/nopcommerce-ebook-indonesia-blueprint.md).

## Prerequisites
- A Linux VPS (Ubuntu 24.04 LTS recommended), Docker + Docker Compose v2.
- A domain with `A`/`AAAA` records pointing at the VPS (Caddy needs this to issue TLS).

## 1. Configure secrets
```bash
cd deploy
cp .env.example .env
# edit .env: DOMAIN, ACME_EMAIL, POSTGRES_PASSWORD, REDIS_PASSWORD, ...
```
`.env` is git-ignored — never commit it.

## 2. Bring up the stack
```bash
docker compose up -d --build
```
This starts: `reverse-proxy` (Caddy, auto-TLS), `nopcommerce` (.NET 9 app), `postgres`,
`redis`, and `db-backup`. Only Caddy publishes ports (80/443); everything else is on the
internal `nopnet` network.

## 3. Install nopCommerce (once)
Browse to `https://YOUR_DOMAIN`. The first time, the nopCommerce **install wizard** appears:
- **Database type:** PostgreSQL
- **Connection:** Host `postgres`, Port `5432`, DB `nopcommerce`, user/password from `.env`
- **Uncheck** "Create sample data" for a clean store
- Set the admin email + a strong password

`citext` is pre-created by `config/init-citext.sql`, so the schema build succeeds.

The wizard writes `App_Data/dataSettings.json` + `App_Data/appsettings.json` into the
persisted `nop_appdata` volume — so you **install once** and survive every redeploy.

## 4. Enable Redis cache
Redis is already wired via the `DistributedCacheConfig__*` env vars in `docker-compose.yml`
(password injected from `.env`). To set values directly in the file instead, merge
`config/appsettings.redis-snippet.json` into `App_Data/appsettings.json` and restart:
```bash
docker compose restart nopcommerce
```

## 5. Verify
- HTTPS works and redirects from HTTP.
- Admin login works; create a test downloadable product (blueprint §5).
- A paid order activates the download (blueprint §6, §9).

## Backups
`db-backup` writes nightly `pg_dump -Fc` archives to the `pg_backups` volume with retention
from `.env`. **You must also copy these off the VPS** (object storage) and run the
**quarterly restore test** in blueprint §11.5.

## Post-deploy QA

After deploy (and after enabling the theme), run the smoke test and follow the checklist:

```bash
deploy/qa/smoke.sh https://YOUR_DOMAIN --product /your-ebook-seo-name --category /c/your-genre --webhook
```

It verifies the theme is active, security headers, the editorial template overrides, the
**download-auth guard**, and the **Midtrans webhook signature guard** (read-only; safe on prod).
See **[deploy/qa/QA-CHECKLIST.md](qa/QA-CHECKLIST.md)** for the manual visual/functional/security passes.

## Notes / caveats
- The `Dockerfile` builds nopCommerce from source; cross-check plugin-copy steps against the
  official `nopSolutions/nopCommerce_docker` repo for your exact version.
- Caddy `rate_limit` needs a custom build (xcaddy); otherwise rate-limit `/download/*` at
  Cloudflare. See `Caddyfile` comments.
- Headless install (`NOP_SEED_DATASETTINGS=true`) is optional/advanced; the wizard is the
  recommended path.
