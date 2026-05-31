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

## Midtrans payment plugin

[`plugins/Nop.Plugin.Payments.Midtrans/`](plugins/Nop.Plugin.Payments.Midtrans/) — a buildable
nopCommerce 4.90 / .NET 9 payment plugin (the one required custom build). Midtrans **Snap**
redirect for **QRIS + Virtual Account + e-wallets + cards**, with a **signature-verified webhook**
that marks the order Paid → which auto-activates the downloadable eBook. No core changes.
See **[its README](plugins/Nop.Plugin.Payments.Midtrans/README.md)** for build/install/sandbox-test steps.

## Storefront content

[`storefront/`](storefront/) — a polished, mobile-first **home page** (CSS + EN/ID markup), the
**Terms / Privacy / Refund** pages, and the **transactional email templates** (order confirmation /
payment received / download available) — all in **English + Bahasa Indonesia**, ready to paste into
nopCommerce Topics / Message templates / a theme. See **[storefront/README.md](storefront/README.md)**
and **[storefront/emails/README.md](storefront/emails/README.md)**.

## Storefront theme

[`themes/EbookIndonesia/`](themes/EbookIndonesia/) — a custom nopCommerce 4.90 / .NET 9 theme that
makes the storefront feel like a **modern publisher / curated eBook platform** (soft commerce,
editorial, mobile-first, EN/ID). CSS-led design system + three faithful view overrides (Head,
Home, Footer) + admin/widget/content playbook. **No core changes**; every widget zone preserved.
The Docker build copies it into `Nop.Web/Themes/EbookIndonesia`. See
**[its README](themes/EbookIndonesia/README.md)**, the
[decision table](themes/EbookIndonesia/docs/default-elements-decision-table.md), and
[progress/plan](themes/EbookIndonesia/IMPLEMENTATION-PROGRESS.md).

## Key decisions at a glance

- **Platform:** nopCommerce **4.90.x** on **.NET 9** (plan the upgrade to 5.0 / .NET 10 LTS — see blueprint §2).
- **Database:** **PostgreSQL** (needs `citext`; pre-created by `deploy/config/init-citext.sql`).
- **Payments:** MVP = manual bank-transfer/QRIS (built-in, no code); Production = **custom Midtrans Snap plugin** (QRIS + VA + e-wallets) — scaffolded in [`plugins/`](plugins/Nop.Plugin.Payments.Midtrans/). (Stripe is not viable for an Indonesia-only entity — see blueprint §8.)
- **Downloads:** stored in the DB, served only through authorized controller logic; guest checkout OFF + validate-user-on-download ON.
- **Core code:** **not modified** — admin config + plugins only.

> Tax (PPN) and legal/refund wording in the blueprint are *assumptions to confirm* with the customer's Indonesian accountant and lawyer.
