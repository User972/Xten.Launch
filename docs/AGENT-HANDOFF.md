# Agent handoff — continue from here

> Paste this whole file as context to a fresh AI coding agent, or read it directly. It captures the
> project, the hard constraints, the repo layout, the theme architecture (including the non-obvious
> gotchas), the exact current state, and how to continue.
>
> **Branch:** `claude/modest-tesla-BvVLM` · commit + push here; don't open a PR unless asked.
> The user prefers **plain-language** explanations.
> (Earlier docs say `claude/zen-bell-G2jyN` then `claude/epic-sagan-qINMg`; both were merged to `main`
> and that same line of work now continues on `claude/modest-tesla-BvVLM`, which contains all of it.)

## Project

A production-oriented, digital-**eBook** nopCommerce store for an **Indonesia-only** merchant selling
to Indonesian consumers. Stack: **nopCommerce 4.90.x** (latest stable) on **.NET 9** + **PostgreSQL**
+ **Redis**, **Docker** on a VPS behind **Caddy**. Currency **IDR**. Bilingual **English + Bahasa
Indonesia**. Digital eBooks (downloadable products) are the first use case.

The repo holds **only our own code** (blueprint, Docker scaffold, a payment plugin, a theme,
storefront content, CI). **nopCommerce core is NOT committed** — it is cloned at build time (git tag
`release-4.90.4`) by `deploy/app/Dockerfile`. Keep it that way (a CI guard fails if core is committed).

## Non-negotiable constraints (don't change without explicit approval)

- nopCommerce **4.90.x / .NET 9**; **PostgreSQL** (needs `citext`, pre-created); **Redis**.
- **No nopCommerce core modifications.** Admin config, plugins, theme/view overrides only.
- **Payments:** a custom **Midtrans Snap** plugin (`plugins/Nop.Plugin.Payments.Midtrans`) is the rail
  (QRIS/VA/e-wallets/cards). Stripe is NOT viable (Indonesia-only entity). Payment truth is the
  **signature-verified server webhook**; never trust the browser redirect.
- Cart/checkout/payment/downloads **MUST use the REAL nopCommerce flow.** Do **not** build a mock cart
  or front-end checkout. Where a design shows a cart/drawer, restyle nopCommerce's real flyout/cart.
- **Download security stays intact:** downloadable files served only via nopCommerce's authorized
  controller; guest checkout **OFF**; "validate user when downloading downloadable products" **ON**.
  The theme must add **no** Download/Checkout/Customer/Order view overrides (static-checks enforces this).
- Don't commit nopCommerce core or secrets/`.env`. Don't change DB provider or Docker infra unless the
  task requires it.

## Repo map (read these)

```
README.md                         Overview + pointers.
docs/nopcommerce-ebook-indonesia-blueprint.md   19-section architecture/impl blueprint (read §2,§8,§9).
docs/AGENT-HANDOFF.md             This file.
deploy/                           Docker scaffold: docker-compose.yml (app+postgres+redis+caddy+backup),
                                  app/Dockerfile (clones nopCommerce release-4.90.4, copies in the THEME
                                  and the Midtrans PLUGIN + `dotnet sln add`, builds, publishes),
                                  Caddyfile, config/*, qa/ (static-checks.sh, smoke.sh, QA-CHECKLIST.md).
plugins/Nop.Plugin.Payments.Midtrans/   Custom IPaymentMethod (redirect + verified webhook). Built into
                                  the image. README has build/install/sandbox steps.
themes/EbookIndonesia/            The custom theme (folder name == SystemName). See THEME section below.
storefront/                       Admin-paste content: home/homepage.{en,id}.html (the HomepageText
                                  topic), legal/ (Terms/Privacy/Refund EN+ID), emails/ (order
                                  placed/paid/completed message templates EN+ID).
.github/workflows/                ci.yml (static-checks always + smoke if vars.STAGING_URL set);
                                  build-nopcommerce.yml (manual: clones nop, compiles plugin+theme).
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

**Theme files:**

| File | Purpose |
|---|---|
| `theme.json` | descriptor (SystemName `EbookIndonesia`). |
| `Content/css/styles.css` | re-skin, sections §1–§23 (tokens, dark mode, components, product/category/blog/account/checkout restyle, eBook book-cards, cart drawer, custom header). |
| `Content/js/theme.js` | vanilla: dark toggle, WhatsApp float (clones first `wa.me` link), cart drawer (opens nopCommerce flyout as a right slide-over; opens on cart click + on AJAX add via `MutationObserver` on `.cart-qty`; Esc/overlay close), FAQ accordion, sticky mobile Buy bar (triggers the real add-to-cart button), auto TOC. |
| `Views/_ViewImports.cshtml` | **REQUIRED** (see #2). |
| `Views/Shared/Head.cshtml` | loads DefaultClean base CSS + re-skin + Google Fonts + no-flash theme script. |
| `Views/Shared/_Header.cshtml` | **custom header** matching the design: top utility strip (EN/ID; currency/tax hidden) + sticky bar (logo, INLINE fixed nav, compact search, account/cart w/ terracotta badge, terracotta CTA). Hides the separate `.header-menu` bar via CSS. Keeps real components + widget zones. **Nav links are FIXED — edit hrefs.** |
| `Views/Shared/Components/Footer/Default.cshtml` | modern footer (keeps all components). |
| `Views/Home/Index.cshtml` | editorial homepage; renders the bilingual `HomepageText` topic + featured/category/news components + all widget zones. |
| `Views/Product/ProductTemplate.Simple.cshtml` | product landing (sticky buy box, auto TOC, author). |
| `Views/Catalog/CategoryTemplate.ProductsInGridOrLines.cshtml` | category editorial hero. |
| `IMPLEMENTATION-PROGRESS.md`, `README.md`, `docs/default-elements-decision-table.md` | read these. |

**Design source:** the real Claude Design bundle (`xten-customer-portal` / `Check Homepage.html`, fetched
from the design API; the original brand is an IELTS/TOEFL/PTE tutoring studio, "Check"). Per the user's
decision we implemented the **full Check homepage** (course marketing + an integrated eBook showcase) as
this store's homepage. eBooks use the **real nopCommerce cart**; the design's mock signup form became a real
**WhatsApp/contact** CTA, and its mock cart/checkout was NOT copied.

## Current state / open thread (start here)

Branch `claude/modest-tesla-BvVLM`; **PR #11 → `main` is OPEN** (CI green: Static checks ✅, Trivy ✅, smoke
skipped — no STAGING_URL; `mergeable_state: clean`; no review comments). **Pushing to this branch updates
PR #11 — do NOT open new PRs.** `main` has been merged in; the Docker base images were re-pinned to **.NET 9**.

Work done across recent sessions (all on this branch):
- **Full "Check" homepage built** (`655c7ff`) from the real design bundle. `storefront/home/homepage.{id,en}.html`
  rewritten to the whole Check structure: hero (band 7+) + lead-CTA card (real WhatsApp/phone — replaces the
  design's mock form), partner strip, why-choose (4 numbered, last inverted), 5 programs + diagnostic helper,
  5-step methodology stepper, faculty (4), student results (3), testimonials, **eBook showcase** (6 `.xt-jacket`
  covers + bundle; cards link to `/search` → real product pages/cart), branches (3), certs/payment strip, 8-Q
  FAQ, final CTA. ID = the design's native copy; EN = translation. New theme components live in **styles.css §24**.
- **WhatsApp float now always-on** (`theme.js`) — prefers an on-page `wa.me` link, else falls back to the
  merchant number `61457068647`; fixes the previously-missing float.
- **Header aligned to the brand** (`_Header.cshtml`) — nav → Program/Pengajar/Tentang/Lokasi/Ebook/FAQ
  (homepage anchors + `/search`), CTA "Daftar Trial Gratis" (`/#trial`), utility strip with hours + phone.
- **Docker re-pinned to .NET 9** (`39725ec`) — Dependabot had bumped sdk+aspnet base images to 10; nopCommerce
  4.90.4 is a net9.0 app that won't start on a .NET-10-only runtime. ⚠️ `main` still carries the bump (merging
  PR #11 — which has the pin — resolves it; otherwise the next merge re-introduces it).
- Earlier this arc: **EN/ID toggle** (`LanguageSelector/Default.cshtml`), **footer social icon-circles** (CSS §6),
  **cart flow verified** at code level, **WhatsApp number filled** into the homepage content.

**What still needs the USER (admin, not code):**
- **Paste the homepage:** `homepage.id.html` → Indonesian, `homepage.en.html` → English (or into the **Standard**
  tab for whichever language is the store default) in the `HomepageText` topic via HTML-source view. The topic now
  holds the WHOLE page, so keep the homepage product/category/news components empty or they render after the final CTA.
- **Replace the design placeholders** (NOT real data): stats (2,400+, 93%…), teacher/student names, course prices,
  phone `(021) 5099-9000`, branch addresses, and confirm the `+61` WhatsApp number on an Indonesia-only store.
- Curate the footer (disable compare/recently-viewed/vendor; place Terms/Privacy/Refund/About/Contact topics; create
  a `FooterInfo` topic per language with the brand blurb + a `wa.me` link; set the real store name + social URLs).

**Immediate next steps:**
1. **Visual tuning from a screenshot** — the homepage is a faithful but UNVERIFIED first pass (CSS/HTML are
   static-checks-clean, but nothing was rendered). Get a deployed screenshot (light+dark, desktop+mobile) and tighten
   spacing/responsive details of the §24 components.
2. (Optional, offered, NOT done) Add `.github/dependabot.yml` to ignore major Docker `dotnet` base-image bumps so the
   .NET 9→10 break can't recur; it rides into `main` via PR #11.
3. (Optional) Make the eBook showcase a LIVE grid (real `HomepageProductsViewComponent` styled as book-cards) instead
   of static cards linking to `/search` — needs `Index.cshtml` ordering work (split the topic before/after products).
4. Confirm the cart end-to-end on a deployed instance (badge → right-drawer → /checkout → Midtrans); run
   `deploy/qa/smoke.sh` + the QA checklist.

## How to build / verify

- **Rebuild & run:** `cd deploy && cp .env.example .env` (set secrets) `&& docker compose build
  --no-cache nopcommerce && docker compose up -d` (first run: nopCommerce install wizard → PostgreSQL).
- **Enable theme:** Admin → Configuration → Settings → General → Theme → **eBook Indonesia**. Also
  enable the **"mini shopping cart"** (Shopping cart settings) for the drawer.
- **Homepage (no rebuild):** paste `storefront/home/homepage.{id,en}.html` into the **HomepageText** topic
  per language via the editor's **HTML source view** (WhatsApp number already filled). These `.html` files
  are admin-paste content, not served directly — editing them doesn't change the live site until pasted.
- **Static checks (before every commit):** `bash deploy/qa/static-checks.sh`
- **Runtime smoke (after deploy):** `deploy/qa/smoke.sh https://YOUR_DOMAIN --product /seo --category /c/x`
- **Compile plugin+theme against real nop:** run `.github/workflows/build-nopcommerce.yml`
  (workflow_dispatch).

## Working agreements

- Branch `claude/modest-tesla-BvVLM`; clear commits; push (updates **PR #11 → main**); don't open new PRs.
- Validate with `deploy/qa/static-checks.sh` before committing (JSON/CSS/JS/Razor balance, storefront
  HTML, no secrets, download-security guardrail).
- Never commit nopCommerce core or secrets. Don't weaken download security or the Midtrans webhook.
- When you override a view, base it on the real `release-4.90.4` source and preserve all
  components/widget zones. Theme views won't compile-check locally — they validate at runtime.
- Tax (PPN) and legal/refund text are **drafts** to be confirmed with the customer's accountant/lawyer.
- Explain changes to the user in plain, non-jargon language.

**START HERE:** read `README.md`, `themes/EbookIndonesia/IMPLEMENTATION-PROGRESS.md`, this file's
"Current state" section, and the design (`Check Homepage.html` — fetch the `xten-customer-portal` bundle
from the design API). Then the theme files (`Views/Shared/Head.cshtml` + `_Header.cshtml` +
`Content/css/styles.css` §21–§24 + `Content/js/theme.js` + `Views/Home/Index.cshtml`). The homepage is the
full Check design — built but NOT visually verified. Ask the user for a fresh screenshot (and whether
they've pasted the `HomepageText` topic) before changing anything, then continue from "Immediate next
steps" (start with visual tuning).
