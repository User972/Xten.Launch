# Xten.Launch

Implementation blueprint and deployment scaffold for a **digital-eBook nopCommerce store** targeting **Indonesia**, on **PostgreSQL + Redis**, deployed via **Docker**.

## Start here

📘 **[docs/nopcommerce-ebook-indonesia-blueprint.md](docs/nopcommerce-ebook-indonesia-blueprint.md)** — the full 19-section architecture & implementation blueprint (versioning, PostgreSQL, Indonesia config, eBook product setup, payments, download security, performance, backup/DR, phasing, launch checklist, risk register, final recommendation).

🤝 **[docs/AGENT-HANDOFF.md](docs/AGENT-HANDOFF.md)** — full context to resume work (constraints, repo map, theme architecture + gotchas, current state, how to build/verify). Hand this to a new agent or read it first.

## Deploy scaffold

The [`deploy/`](deploy/) folder is a **multi-tenant** scaffold — one VM hosts many independent stores
behind a shared reverse proxy:

| Path | Purpose |
|---|---|
| `deploy/proxy/` | Shared `nginx-proxy` + `acme-companion` (auto-TLS, host routing) — run once per VM |
| `deploy/customers/template/` | Isolated per-tenant stack (nopCommerce app + PostgreSQL + nginx + nightly db-backup) |
| `deploy/scripts/new-customer.sh` | Provisions a new tenant end-to-end (random secrets) |
| `deploy/app/Dockerfile` | Builds the **shared** nopCommerce 4.90 (.NET 9) image (theme + Midtrans plugin baked in) |
| `deploy/azure/` | Provision the host VM on Azure (`provision-vm.sh`) |
| `deploy/config/*` | PostgreSQL config templates + `citext` init |

See **[deploy/README.md](deploy/README.md)** for the quick-start.
(The earlier single-stack `docker-compose.yml` + `Caddyfile` + combined `.env.example` were replaced by
the layout above; reverse proxy moved Caddy → nginx-proxy.)

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
re-skins the storefront as the **"Check"** tutoring brand (IELTS/TOEFL/PTE) with an integrated **eBook
showcase** (soft commerce, editorial, mobile-first, EN/ID, light/dark). CSS-led design system + faithful
view overrides (Head, Header, Home, Footer, LanguageSelector, Product, Category) + **live HomepageProducts/
HomepageCategories** components that drive the homepage eBook grid from real admin data + admin/widget/
content playbook. **No core changes**; every widget zone preserved.
The Docker build copies it into `Nop.Web/Themes/EbookIndonesia`. See
**[its README](themes/EbookIndonesia/README.md)**, the
[decision table](themes/EbookIndonesia/docs/default-elements-decision-table.md), and
[progress/plan](themes/EbookIndonesia/IMPLEMENTATION-PROGRESS.md).

## Continuous integration

[`.github/workflows/`](.github/workflows/):
- **`ci.yml`** (every push/PR): `static-checks` runs [`deploy/qa/static-checks.sh`](deploy/qa/static-checks.sh)
  (JSON/CSS/JS/shell/Razor/HTML validation, no-secrets, and the download-security guardrail);
  `smoke` runs [`deploy/qa/smoke.sh`](deploy/qa/smoke.sh) against staging **when** the repo variable
  `STAGING_URL` is set (skipped otherwise).
- **`build-nopcommerce.yml`** (manual / `workflow_dispatch`): clones nopCommerce 4.90.4, builds the
  **Midtrans plugin** against `Nop.Web`, publishes (theme included), and asserts both land in the output.

**To gate merges:** in **Settings → Branches → Branch protection**, mark **`Static checks`** (and,
once `STAGING_URL` is configured, **`Smoke test (staging)`**) as **required status checks**.
Configure staging under **Settings → Secrets and variables → Actions → Variables**:
`STAGING_URL` (+ optional `STAGING_PRODUCT_PATH`, `STAGING_CATEGORY_PATH`, `STAGING_INSECURE`).

## Key decisions at a glance

- **Platform:** nopCommerce **4.90.x** on **.NET 9** (plan the upgrade to 5.0 / .NET 10 LTS — see blueprint §2).
- **Database:** **PostgreSQL** (needs `citext`; pre-created by `deploy/config/init-citext.sql`).
- **Payments:** MVP = manual bank-transfer/QRIS (built-in, no code); Production = **custom Midtrans Snap plugin** (QRIS + VA + e-wallets) — scaffolded in [`plugins/`](plugins/Nop.Plugin.Payments.Midtrans/). (Stripe is not viable for an Indonesia-only entity — see blueprint §8.)
- **Downloads:** stored in the DB, served only through authorized controller logic; guest checkout OFF + validate-user-on-download ON.
- **Core code:** **not modified** — admin config + plugins only.

> Tax (PPN) and legal/refund wording in the blueprint are *assumptions to confirm* with the customer's Indonesian accountant and lawyer.
