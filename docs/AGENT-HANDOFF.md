# Agent handoff — continue from here

> Paste this whole file as context to a fresh AI coding agent, or read it directly. It captures the
> project, the hard constraints, the repo layout, the theme architecture (including the non-obvious
> gotchas), the exact current state, and how to continue.
>
> **Branch:** `claude/zen-bell-G2jyN` · **Latest commit at handoff:** `63afd17` · commit + push here;
> don't open a PR unless asked. The user prefers **plain-language** explanations.

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

**Design source:** a Claude Design handoff (the "Check" bundle — originally an IELTS/tutoring brand).
We applied its **visual system + homepage/eBook-store/cart structure** to this eBook shop; we did NOT
copy the tutoring content or its mock cart/checkout.

## Current state / open thread (start here)

We just fixed a series of rendering failures, in order:
- **blank storefront** → added `Views/_ViewImports.cshtml`
- **stacked / no-layout** → `Head.cshtml` now loads DefaultClean's base CSS before the re-skin
- **layout conflicts** → removed re-skin width rules that fought DefaultClean
- **header didn't match the design** → added the custom `_Header.cshtml` (commit `63afd17`)

The user is doing a **clean rebuild on the latest commit to confirm it renders**. **Awaiting their
confirmation/screenshot.**

**Immediate next steps once they confirm:**
1. Verify the homepage renders with a **horizontal header** (utility strip + logo/nav/search/cart/CTA),
   correct columns, working light/dark toggle, WhatsApp float, and the cart drawer.
2. **Polish to match the design:** DefaultClean constrains content to ~980px; the Check design is wider
   (~1160–1280). Widen the container WITHOUT refighting DefaultClean's media queries (own the wrapper,
   or override `.center-1` width inside the SAME `@media (min-width:1001px)`).
3. Point the **fixed header nav links** (`_Header.cshtml`) at real pages; create the topics they reference.
4. eBook **product cards** already restyled (`.item-box` → book-cards) — verify with real products.
5. Confirm **cart**: add-to-cart → badge bump → flyout opens as the right drawer → Checkout → Midtrans.

## How to build / verify

- **Rebuild & run:** `cd deploy && cp .env.example .env` (set secrets) `&& docker compose build
  --no-cache nopcommerce && docker compose up -d` (first run: nopCommerce install wizard → PostgreSQL).
- **Enable theme:** Admin → Configuration → Settings → General → Theme → **eBook Indonesia**. Also
  enable the **"mini shopping cart"** (Shopping cart settings) for the drawer.
- **Homepage content:** paste `storefront/home/homepage.{en,id}.html` into the **HomepageText** topic
  per language.
- **Static checks (before every commit):** `bash deploy/qa/static-checks.sh`
- **Runtime smoke (after deploy):** `deploy/qa/smoke.sh https://YOUR_DOMAIN --product /seo --category /c/x`
- **Compile plugin+theme against real nop:** run `.github/workflows/build-nopcommerce.yml`
  (workflow_dispatch).

## Working agreements

- Branch `claude/zen-bell-G2jyN`; clear commits; push; no PR unless asked.
- Validate with `deploy/qa/static-checks.sh` before committing (JSON/CSS/JS/Razor balance, storefront
  HTML, no secrets, download-security guardrail).
- Never commit nopCommerce core or secrets. Don't weaken download security or the Midtrans webhook.
- When you override a view, base it on the real `release-4.90.4` source and preserve all
  components/widget zones. Theme views won't compile-check locally — they validate at runtime.
- Tax (PPN) and legal/refund text are **drafts** to be confirmed with the customer's accountant/lawyer.
- Explain changes to the user in plain, non-jargon language.

**START HERE:** read `README.md`, `themes/EbookIndonesia/README.md` and `IMPLEMENTATION-PROGRESS.md`,
then `themes/EbookIndonesia/Views/Shared/Head.cshtml` + `_Header.cshtml` + `Content/css/styles.css`.
Then ask the user for the latest homepage screenshot/console output and continue the "Current state" thread.
