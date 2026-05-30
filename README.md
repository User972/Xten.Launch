# Xten.Launch

Implementation blueprint and deployment scaffold for a **digital-eBook nopCommerce store** targeting **Indonesia**, on **PostgreSQL + Redis**, deployed via **Docker**.

## Start here

📘 **[docs/nopcommerce-ebook-indonesia-blueprint.md](docs/nopcommerce-ebook-indonesia-blueprint.md)** — the full 19-section architecture & implementation blueprint (versioning, PostgreSQL, Indonesia config, eBook product setup, payments, download security, performance, backup/DR, phasing, launch checklist, risk register, final recommendation).

## Deploy scaffold

The [`deploy/`](deploy/) folder contains a runnable starting point referenced by the blueprint:

| File | Purpose |
|---|---|
| `deploy/docker-compose.yml` | App + PostgreSQL + Redis + Caddy + backup sidecar |
| `deploy/.env.example` | All secrets/config as env vars (copy to `.env`, never commit) |
| `deploy/Caddyfile` | Auto-TLS, security headers, gzip, static caching |
| `deploy/app/Dockerfile` | Builds nopCommerce 4.90 (.NET 9) from source |
| `deploy/app/entrypoint.sh` | Optional headless seeding of `dataSettings.json` |
| `deploy/config/*` | PostgreSQL/Redis config templates + `citext` init |

See **[deploy/README.md](deploy/README.md)** for the quick-start.

## Key decisions at a glance

- **Platform:** nopCommerce **4.90.x** on **.NET 9** (plan the upgrade to 5.0 / .NET 10 LTS — see blueprint §2).
- **Database:** **PostgreSQL** (needs `citext`; pre-created by `deploy/config/init-citext.sql`).
- **Payments:** MVP = manual bank-transfer/QRIS (built-in, no code); Production = **custom Midtrans Snap plugin** (QRIS + VA + e-wallets) — the one required custom build.
- **Downloads:** stored in the DB, served only through authorized controller logic; guest checkout OFF + validate-user-on-download ON.
- **Core code:** **not modified** — admin config + plugins only.

> Tax (PPN) and legal/refund wording in the blueprint are *assumptions to confirm* with the customer's Indonesian accountant and lawyer.
