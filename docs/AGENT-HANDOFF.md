# Agent handoff — continue from here

> Paste this whole file as context to a fresh AI coding agent, or read it directly. It captures the
> project, the hard constraints, the repo layout, the theme architecture (including the non-obvious
> gotchas), the exact current state, and how to continue.
>
> **Branch:** `claude/jolly-gates-626mK` · commit + push here; **don't open a PR unless asked.**
> This branch currently sits exactly at the `main` tip (nothing extra committed yet) — your commits
> will be the first on it.
> **Branch lineage:** `zen-bell` → `epic-sagan` → `modest-tesla` (**PR #11**, merged) → `magical-cannon`
> (**PR #14**, merged). All of that work is now on `main`; this branch continues from the `main` tip.
> The user prefers **plain-language** explanations and is **not** a developer.

## Project

A production-oriented, digital-**eBook** nopCommerce store for an **Indonesia** merchant. Per the user's
decision the brand is **"Check"** — an IELTS/TOEFL/PTE tutoring studio whose homepage markets the courses
**and** sells digital eBooks. Stack: **nopCommerce 4.90.4** on **.NET 9** + **PostgreSQL** (needs `citext`)
+ **Redis** (shared-proxy/dev only). **Docker on a Linux VM** behind a **shared `nginx-proxy` + `acme-companion`**
(automatic Let's Encrypt TLS). Currency **IDR**. Bilingual **English + Bahasa Indonesia**.

**The deployment is now multi-tenant:** one VM hosts many independent stores. A single shared reverse proxy
owns ports 80/443 and routes each domain to its own isolated per-tenant stack (nopCommerce app + PostgreSQL
+ nginx + a nightly db-backup sidecar). See `deploy/README.md` for the full picture.

The repo holds **only our own code** (blueprint, Docker scaffold, a payment plugin, a theme, storefront
content, CI). **nopCommerce core is NOT committed** — it is cloned at build time (git tag `release-4.90.4`)
by `deploy/app/Dockerfile`. Keep it that way (a CI guard fails if core is committed).

## Non-negotiable constraints (don't change without explicit approval)

- nopCommerce **4.90.4 / .NET 9**; **PostgreSQL** (needs `citext`, pre-created); **Redis** where used.
  Do **not** bump the Docker base images to **.NET 10** — 4.90.4 is a `net9.0` app and won't start on a
  .NET-10-only runtime. A **Dependabot guard** (`.github/dependabot.yml`, now on `main`) blocks the major
  bump; Dependabot's two .NET-10 PRs (#12, #13) were correctly closed.
- **No nopCommerce core modifications.** Admin config, plugins, theme/view overrides only.
- **Payments:** a custom **Midtrans Snap** plugin (`plugins/Nop.Plugin.Payments.Midtrans`) is the rail
  (QRIS/VA/e-wallets/cards). Stripe is NOT viable (Indonesia-only entity). Payment truth is the
  **signature-verified server webhook**; never trust the browser redirect.
- Cart/checkout/payment/downloads **MUST use the REAL nopCommerce flow.** Do **not** build a mock cart
  or front-end checkout. Where a design shows a cart/drawer, restyle nopCommerce's real flyout/cart.
- **Download security stays intact:** downloadable files served only via nopCommerce's authorized
  controller; guest checkout **OFF**; "validate user when downloading downloadable products" **ON**.
  The theme must add **no** Download/Checkout/Customer/Order view overrides (static-checks enforces this).
- Don't commit nopCommerce core or secrets/`.env`. Don't change DB provider or the deploy topology unless
  the task requires it.

## Repo map (read these)

```
README.md                         Overview + pointers.
docs/nopcommerce-ebook-indonesia-blueprint.md   19-section architecture/impl blueprint (read §2,§8,§9).
docs/AGENT-HANDOFF.md             This file.
deploy/                           MULTI-TENANT Docker scaffold:
                                  proxy/            shared nginx-proxy + acme-companion (run once per VM)
                                  customers/template/   per-tenant stack template (app + postgres + nginx +
                                                        db-backup sidecar + nginx.conf)
                                  customers/<slug>/  a live tenant (scaffolded from the template; .env git-ignored)
                                  scripts/new-customer.sh   provisions a tenant end-to-end (random secrets)
                                  azure/            provision the host VM on Azure (README + provision-vm.sh)
                                  app/Dockerfile    builds the SHARED nopCommerce image (clones release-4.90.4,
                                                    bakes in the THEME + the Midtrans PLUGIN, builds, publishes)
                                  config/*, qa/ (static-checks.sh, smoke.sh, QA-CHECKLIST.md)
                                  (the old single-stack docker-compose.yml + Caddyfile + combined .env.example
                                   were REMOVED — superseded by the multi-tenant layout above)
plugins/Nop.Plugin.Payments.Midtrans/   Custom IPaymentMethod (redirect + verified webhook). Built into
                                  the shared image. README has build/install/sandbox steps.
themes/EbookIndonesia/            The custom theme (folder name == SystemName). See THEME section below.
storefront/                       Admin-paste content: home/homepage{,-lower}.{en,id}.html (TWO homepage
                                  topics, see below), legal/ (Terms/Privacy/Refund EN+ID), emails/ templates.
.github/workflows/                ci.yml (static-checks always + smoke if vars.STAGING_URL set);
                                  build-nopcommerce.yml (manual: clones nop, compiles plugin+theme).
.github/dependabot.yml            ignores major Docker `dotnet` base-image bumps (the .NET 9→10 guard).
```

## Theme — architecture and the gotchas we learned the hard way

`themes/EbookIndonesia/` provides a "Check" editorial design: teal `#0F3D3E` + terracotta `#D97757`
on cream `#FAF6EE`, ink `#15201F`; dark mode page `#0C1111` / cards `#161E1D` / parchment text; fonts
**Fraunces** (display) + **Plus Jakarta Sans** (body) + **JetBrains Mono** (labels); **light/dark
toggle** via `[data-theme]` + `localStorage 'xt-theme'` + a no-flash inline script in `Head.cshtml`;
pulsing **WhatsApp float**.

**Critical mechanics — do not regress these:**

1. A nopCommerce theme is loaded as the **active theme's stylesheet only**. Our re-skin `styles.css`
   does NOT contain the full base layout, so `Head.cshtml` loads **DefaultClean's base CSS FIRST, then
   our re-skin LAST** (override):
   `~/Themes/DefaultClean/Content/css/styles.css` → `~/Themes/{themeName}/Content/css/styles.css`.
   nopCommerce serves the whole `/Themes` dir statically, so the cross-theme reference works.
2. Theme views live **outside** the app `/Views` folder, so they do NOT inherit the global
   `_ViewImports`. **`themes/EbookIndonesia/Views/_ViewImports.cshtml` is REQUIRED** (NopRazorPage base,
   `NopHtml`/`NopUrl`, tag helpers, `@using` namespaces). Without it every override fails to compile →
   **blank storefront**.
3. DefaultClean is **mobile-first**; its DESKTOP header/menu layout is in `@media (min-width:1001px)`.
   Do NOT put layout/width rules in the re-skin on DefaultClean structural selectors (`.header`,
   `.header-upper/lower`, `.header-links`, `.header-menu`, `.center-1`, containers) — a non-media
   re-skin rule overrides DefaultClean's desktop media rules and **breaks the layout**. The re-skin
   should only set colors/fonts/components on stock markup; **own the markup** (like `_Header`) if you
   need to control layout.
4. nopCommerce compiles theme Razor **at runtime**. You CANNOT compile-check views locally. Build every
   override against the **real `release-4.90.4` source** (fetch raw files from
   `raw.githubusercontent.com/nopSolutions/nopCommerce/release-4.90.4/...`), preserve every partial/
   view-component/widget-zone, then validate with `deploy/qa/static-checks.sh` and smoke-test after deploy.
5. **DefaultClean 4.90 renamed a lot of markup** — verify class names against the real source before you
   style them, or your CSS silently misses (this bit us on the footer, which looked *broken* because
   DefaultClean's own `.footer{background:#eee}` filled the gap). Confirmed renames: footer columns are
   now `.footer-navigation > .footer-menu > .footer-menu__title / __list / __link` (NOT the legacy
   `.footer-block .title/.list`); the main menu is `.menu__item / .menu__link / .menu__toggle` (NOT
   `.top-menu/.sublist`). When a SHARED class (`.footer`, `.header`, `.master-wrapper-content`) fights
   you, **scope the rule under our own class** (`.footer.xt-footer`, `.header.xt-header`) or an ancestor
   (`body .master-wrapper-content`) so SPECIFICITY wins regardless of stylesheet load order — never rely
   on source order. Confirmed-CURRENT (safe to target) classes: `.header-links/.cart-qty/.ico-cart/#topcartlink`,
   the flyout cart (`#flyout-cart/.mini-shopping-cart/.count/.items/.item/.totals/.buttons`), and book
   cards (`.item-box/.product-item/.add-info/.product-box-add-to-cart-button`).
6. **The cart drawer fights nopCommerce on three fronts — all now solved, don't undo them** (CSS §22 +
   `theme.js`). nopCommerce renders `#flyout-cart` (a) **inside the header**, where the sticky bar + the
   `_Root` page wrappers each open a **stacking context**, so a full-screen overlay on `<body>` paints
   OVER the drawer; (b) with an **inline `style="display:none"`**, which outranks any class selector; and
   (c) at `>=1001px` DefaultClean ships `@media(min-width:1001px){.flyout-cart{position:absolute;top:100%;
   z-index:100;width:300px}}`, which (equal specificity) won the load-order battle and rendered the cart as
   a 300px box behind the overlay. Fixes: `theme.js` **portals `#flyout-cart` to `<body>`** on init (and
   re-portals + re-adds the close button on each open, surviving AjaxCart replacing the cart link after an
   add); CSS uses **`display:flex !important`** (the only thing that beats the inline `display:none`) plus
   **`!important` on position/top/right/height/width/z-index and the open transform** (to beat the desktop
   dropdown rule). Overlay `z:9998`, drawer `z:9999`. Verified with headless repros against the real
   release-4.90.4 DefaultClean CSS + `_Root` layout. **The real nopCommerce cart/checkout/Midtrans flow is
   unchanged** — only the flyout's position/visibility is restyled.
7. **Dark mode flips `--xt-brand` lighter** (`#0F3D3E` → `#6FB7AE`, correct for links/text). Inverted
   **teal accent panels** that use the brand token as a *background* (testimonial band, eBook bundle,
   why-choose #04, methodology step #5, the teal results card, the "not sure?" programs card, hero proof
   pill, active filter chip) therefore went pale + low-contrast in dark mode — pin those panels to deep
   teal `#0F3D3E` in dark mode (CSS §24) so cream text stays crisp. When an "accent" surface reuses a
   layout class whose own background is declared later in the cascade, use a **compound selector**
   (e.g. `.xt-prog.xt-why__c--invert`) so the accent wins in both modes.

**Theme files:**

| File | Purpose |
|---|---|
| `theme.json` | descriptor (SystemName `EbookIndonesia`). |
| `Content/css/styles.css` | re-skin, sections **§1–§24** (tokens §21, dark mode, components, product/category/blog/account/checkout restyle, eBook book-cards, **cart drawer §22**, **custom header §23**, **full Check homepage §24**). |
| `Content/js/theme.js` | vanilla: dark toggle, WhatsApp float (always-on — prefers an on-page `wa.me` link, else falls back to `61457068647`), **cart drawer** (portals `#flyout-cart` to `<body>`, opens it as a right slide-over on cart click + on AJAX add via `MutationObserver`; Esc/overlay close), FAQ accordion, sticky mobile Buy bar, auto TOC. |
| `Views/_ViewImports.cshtml` | **REQUIRED** (see gotcha #2). |
| `Views/Shared/Head.cshtml` | loads DefaultClean base CSS + re-skin + Google Fonts + no-flash theme script. |
| `Views/Shared/_Header.cshtml` | **custom header**: top utility strip (EN/ID; currency/tax hidden) + sticky bar (logo, INLINE fixed nav, compact search, account/cart w/ terracotta badge, terracotta CTA). Hides the separate `.header-menu` bar via CSS. Keeps real components + widget zones. **Nav links are FIXED — edit hrefs.** |
| `Views/Shared/Components/Footer/Default.cshtml` | modern footer (keeps all components). |
| `Views/Shared/Components/LanguageSelector/Default.cshtml` | EN/ID segmented pill (real CHANGE_LANGUAGE route). |
| `Views/Shared/Components/HomepageProducts/Default.cshtml` | **LIVE eBook grid** — renders "Show on home page" products as theme book-cards (real cover/price/rating/Add-to-cart → opens the drawer; "New" badge from MarkAsNew). |
| `Views/Shared/Components/HomepageCategories/Default.cshtml` | **LIVE filter chips** — renders "Show on home page" categories as chips linking to their catalog pages. |
| `Views/Home/Index.cshtml` | editorial homepage. Renders **`HomepageText`** (upper) → the **live `#ebooks` store** (HomepageCategories chips + HomepageProducts grid) → **`HomepageTextLower`** (lower) → bestsellers → news → polls. **All widget zones preserved.** |
| `Views/Product/ProductTemplate.Simple.cshtml` | product landing (sticky buy box, auto TOC, author). |
| `Views/Catalog/CategoryTemplate.ProductsInGridOrLines.cshtml` | category editorial hero. |
| `IMPLEMENTATION-PROGRESS.md`, `README.md`, `docs/default-elements-decision-table.md` | read these. |

**Design source:** the real Claude Design bundle (`xten-customer-portal` / `Check Homepage.html`, fetched
from the design API; the brand is the IELTS/TOEFL/PTE tutoring studio "Check"). Per the user's decision we
implemented the **full Check homepage** (course marketing + an integrated eBook showcase). eBooks use the
**real nopCommerce cart**; the design's mock signup form became a real **WhatsApp/contact** CTA, and its
mock cart/checkout was deliberately NOT copied.

## Current state / open thread (start here)

Branch `claude/jolly-gates-626mK`, sitting at the `main` tip (`6ed14a5`) with **no open PR yet** — your
commits will be the first on it. **PR #11** (full Check homepage) and **PR #14** (multi-tenant refactor)
are both **merged to `main`**; the `main`-vs-.NET-9 Dockerfile issue is resolved (the .NET 9 pin + the
Dependabot guard are on `main`, and Dependabot's .NET-10 PRs were closed).

Work landed since the original homepage build (now on `main`):
- **LIVE eBook store** (`3758e8f`, PR #14) — the homepage "Ebook store" is no longer static placeholder
  HTML. Two new component overrides render **real admin data**: `HomepageProducts` → "Show on home page"
  products as book-cards (live cover/price/star-rating/Add-to-cart that opens the drawer; "New" badge),
  `HomepageCategories` → "Show on home page" categories as filter chips. `Index.cshtml` renders the live
  `#ebooks` section **between two topics** so it keeps its mid-page spot. The homepage topic was **split**:
  `HomepageText` = upper (hero → testimonials), `HomepageTextLower` = lower (locations → FAQ → final CTA);
  the static ebook block was removed. New `storefront/home/homepage-lower.{en,id}.html` hold the lower half.
- **Multi-tenant deploy refactor** (`aae5ad3`, PR #14) — replaced the single-stack `docker-compose.yml` +
  `Caddyfile` + combined `.env.example` with: `deploy/proxy/` (shared `nginx-proxy` + `acme-companion`,
  auto-TLS, host routing), `deploy/customers/template/` (isolated per-tenant stack: app + postgres + nginx
  + nightly `db-backup` sidecar), `deploy/scripts/new-customer.sh` (provision a tenant end-to-end with random
  secrets), and `deploy/azure/` (VM provisioning). The reverse proxy moved **Caddy → nginx-proxy**.
- **Cart drawer fixed for real** (`3a1fbd8`, `c4eebaa`, `51d7156`) — the slide-over now actually appears:
  portaled to `<body>` to escape the header's stacking context, `display:flex !important` to beat the inline
  `display:none`, and `!important` layout to beat DefaultClean's desktop dropdown. See gotcha #6. Verified
  with headless repros against real 4.90.4 markup.
- **Visual polish via headless renders** (`9f46544`, `0f301fe`, `f09d1ba`) — fixed the invisible "not sure?"
  card (teal restored), a 4-up faculty row, the beige homepage bands' missing side gutters, and dark-mode
  teal accent panels going pale (see gotcha #7). All CSS-only; verified by local headless render
  (light/dark, desktop/mobile) — but **not yet on a real deployed instance**.

Earlier on this line of work (already on `main`): the full "Check" homepage build, the always-on WhatsApp
float, the brand-aligned header, the EN/ID toggle, footer social icon-circles, the Docker .NET-9 re-pin +
Dependabot guard, and code-level cart-flow verification.

**Verification status:** the homepage + cart drawer have now been iterated against **headless browser
renders** of the real 4.90.4 markup (a big step past "never rendered"), but **a real deployed instance has
still not been screenshotted or smoke-tested end-to-end** (live cart → /checkout → Midtrans). That is the
top remaining check.

**What still needs the USER (admin, not code):**
- **Paste TWO homepage topics** (HTML-source view, per language): `homepage.{id,en}.html` → **`HomepageText`**
  (upper); create a new **`HomepageTextLower`** topic (Published ✔) and paste `homepage-lower.{id,en}.html`
  (lower). The static ebook section is gone from the topic — it now comes from products/categories.
- **Tick "Show on home page"** on the eBook **products** (they become the live grid cards) and on the eBook
  **categories** (they become the filter chips). Until you do, the grid shows a friendly "new ebooks on the
  way" fallback. Curate/order books here — no HTML editing.
- **Keep nopCommerce's other homepage components** (best-sellers / news / polls) empty unless wanted, or they
  render after the lower topic.
- **Replace the design placeholders** (NOT real data): stats (2,400+, 93%…), teacher/student names, course
  prices, phone `(021) 5099-9000`, branch addresses, and confirm the `+61` WhatsApp number for an Indonesia
  store.
- Curate the footer (disable compare/recently-viewed/vendor; place Terms/Privacy/Refund/About/Contact topics;
  create a `FooterInfo` topic per language with the brand blurb + a `wa.me` link; set store name + social URLs).
- **Enable the theme** (Admin → Settings → General → Theme → **eBook Indonesia**) and the **mini shopping cart**.

**Immediate next steps:**
1. **Deployed screenshot + cart smoke pass** — stand up a real tenant (see build/verify), enable the theme +
   mini cart, paste both topics, mark a few books "Show on home page", then screenshot light+dark / desktop+mobile
   and confirm the live cart end-to-end (badge → right drawer → /cart → /checkout → Midtrans). Tune §24
   spacing/responsive against `Check Homepage.html`. Run `deploy/qa/smoke.sh` + the QA checklist.
2. **Rehearse tenant provisioning** — `deploy/proxy` up once, then `new-customer.sh <slug> <domain>`; verify
   DNS + acme TLS + the install wizard + the Midtrans webhook registration per tenant.
3. (Optional) Self-host/subset the Google Fonts to claw back the request; small download-page / checkout
   reassurance via widget-zone content (admin, no override needed).

## How to build / verify (multi-tenant)

- **Shared proxy (once per VM):** `cd deploy/proxy && cp .env.example .env` (set `ACME_EMAIL`)
  `&& docker compose up -d`. Creates the external `webproxy` network every tenant attaches to.
- **Add a tenant:** `deploy/scripts/new-customer.sh <slug> <domain> [acme_email]` — builds the shared
  `nop-ebook:release-4.90.4` image once, scaffolds `deploy/customers/<slug>/`, writes a random
  `POSTGRES_PASSWORD`, and brings the isolated stack up (compose project `<slug>`).
- **Install nopCommerce (once per tenant):** browse to `https://<domain>` → wizard → PostgreSQL host
  `postgres`, uncheck sample data. `citext` is pre-created; install state persists in the tenant's volume.
- **Enable theme:** Admin → Settings → General → Theme → **eBook Indonesia**; also enable the **mini
  shopping cart** for the drawer.
- **Homepage (no rebuild):** paste `storefront/home/homepage.{id,en}.html` → `HomepageText` and
  `homepage-lower.{id,en}.html` → `HomepageTextLower`; mark eBook products/categories "Show on home page".
- **Static checks (before every commit):** `bash deploy/qa/static-checks.sh`
- **Runtime smoke (after deploy):** `deploy/qa/smoke.sh https://<domain> --product /seo --category /c/x --webhook`
- **Compile plugin+theme against real nop:** run `.github/workflows/build-nopcommerce.yml` (workflow_dispatch).

## Working agreements

- Branch `claude/jolly-gates-626mK`; clear commits; push (updates the branch — **no open PR yet**, and
  **don't open one unless asked**). PR #11 and #14 are already merged to `main`.
- Validate with `deploy/qa/static-checks.sh` before committing (JSON/CSS/JS/Razor balance, storefront
  HTML, no secrets, download-security guardrail).
- Never commit nopCommerce core or secrets. Never weaken download security or the Midtrans webhook. Stay
  on **.NET 9** (the Dependabot guard is on `main` — don't remove it).
- When you override a view, base it on the real `release-4.90.4` source and preserve all
  components/widget zones. Theme views won't compile-check locally — they validate at runtime.
- Tax (PPN) and legal/refund text are **drafts** to be confirmed with the customer's accountant/lawyer.
- The design's stats/names/prices/phone/addresses are **placeholders, not real data**. Explain changes to
  the user in plain, non-jargon language.

**START HERE:** read `README.md`, `themes/EbookIndonesia/IMPLEMENTATION-PROGRESS.md`, this file's
"Current state" section, and the design (`Check Homepage.html` — fetch the `xten-customer-portal` bundle
from the design API). Then the theme files (`Views/Shared/Head.cshtml` + `_Header.cshtml` +
`Content/css/styles.css` §21–§24 + `Content/js/theme.js` + `Views/Home/Index.cshtml` + the
`HomepageProducts`/`HomepageCategories` components). The homepage + cart drawer have been iterated against
**headless renders** but **not a real deployed instance** — the top next step is a deployed screenshot +
cart smoke pass. Ask the user for a fresh deployed screenshot (and whether they've pasted both topics +
marked books "Show on home page") before tuning visuals.
