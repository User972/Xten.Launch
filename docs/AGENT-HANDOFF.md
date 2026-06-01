# Agent handoff — continue from here

> Paste this whole file as context to a fresh AI coding agent, or read it directly. It captures the
> project, the hard constraints, the repo layout, the theme architecture (including the non-obvious
> gotchas), the exact current state, and how to continue.
>
> **Branch:** `claude/epic-sagan-qINMg` · **Latest commit:** `b713fe3` · commit + push here;
> don't open a PR unless asked. The user prefers **plain-language** explanations.
> (Older docs say `claude/zen-bell-G2jyN`; that same line of work now continues on `claude/epic-sagan-qINMg`.)

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

**Design source:** a Claude Design handoff (the "Check" bundle — originally an IELTS/tutoring brand).
We applied its **visual system + homepage/eBook-store/cart structure** to this eBook shop; we did NOT
copy the tutoring content or its mock cart/checkout.

## Current state / open thread (start here)

The storefront **renders correctly** (header, body, footer) and we've been aligning it to the "Check"
reference the user supplied (the original IELTS/tutoring design — match the LOOK, not the words). Work
this session (branch `claude/epic-sagan-qINMg`):

- **Footer FIXED** (`664438b`, `6c2e1f6`) — it had styled legacy `.footer-block` classes; retargeted to
  4.90's real `.footer-menu__*` and scoped every footer rule under `.xt-footer` so it beats DefaultClean.
  Footer is now the deep-teal editorial panel (terracotta mono labels, readable parchment links, styled
  newsletter + outlined social chips, copyright/powered-by row), columns in an even row. Dropped the dead
  §5 legacy nav rules.
- **`.xt-cta` collision FIXED + body widened** (`4bf2f93`) — the header CTA reused `.xt-cta` (the
  homepage's button-GROUP container) and boxed the hero buttons in terracotta; renamed it `.xt-headcta`.
  Widened the page body to `--xt-wrap` (~1160) to match header/footer, inside DefaultClean's
  `min-width:1001px` breakpoint via `body .master-wrapper-content`.
- **Header action cluster cleaned** (`df7d686`) — hid wishlist/inbox/register; account + cart are now
  **circular icon buttons** (SVG embedded as base64 masks; cart count badge kept); light/dark toggle
  moved next to the cart. ⚠️ icons render (valid base64) but were NOT visually verified — eyeball them.
- **Homepage hero made paste-ready** (`b713fe3`) — `storefront/home/homepage.{en,id}.html` finalized:
  catalogue → `/search`, `[STORE_NAME]` reworded out, **one** placeholder left (`[WHATSAPP_E164]`).

**What still needs the USER (admin, not code):**
- **Render the hero:** paste `homepage.en.html` / `homepage.id.html` into the `HomepageText` topic (HTML
  source view), per language, and fill `[WHATSAPP_E164]`. Takes effect on save — **no rebuild**.
- **Curate the footer** (it shows nopCommerce defaults incl. compare/recently-viewed/vendor): disable
  product comparison + recently-viewed + vendors; place Terms/Privacy/Refund/About/Contact topics into the
  footer columns; create a `FooterInfo` topic (per language) with the brand blurb + a `wa.me` link (also
  powers the WhatsApp float); set the real Store name + social URLs.
- Rebuild on `b713fe3` and confirm footer / header icons / width render.

**Immediate next steps (code):**
1. **Header nav links** (`_Header.cshtml`) are fixed hrefs: `/search`, `/free-resources`, `/blog`,
   `/about-us`, `/contactus`. `/free-resources` + `/about-us` need topics created (or repoint them);
   the rest are real routes. (NOT done — offered.)
2. Optional **EN/ID text toggle**: nopCommerce's LanguageSelector renders a `<select>` (or flag images),
   not the reference's "EN / ID" text toggle — needs a small `LanguageSelector` view override (allowed;
   not in the forbidden Download/Checkout/Customer/Order list). Held off per "don't override unless needed".
3. Optional: footer social as true icon-circles (currently text chips); verify account/checkout/blog page
   CSS against real 4.90 markup (low-risk — no-ops if renamed, nothing fights them like the footer did).
4. Confirm **cart**: add-to-cart → badge bump → flyout opens as the right drawer → Checkout → Midtrans.
5. After deploy, run `deploy/qa/smoke.sh` + the QA checklist.

## How to build / verify

- **Rebuild & run:** `cd deploy && cp .env.example .env` (set secrets) `&& docker compose build
  --no-cache nopcommerce && docker compose up -d` (first run: nopCommerce install wizard → PostgreSQL).
- **Enable theme:** Admin → Configuration → Settings → General → Theme → **eBook Indonesia**. Also
  enable the **"mini shopping cart"** (Shopping cart settings) for the drawer.
- **Homepage hero (no rebuild):** paste `storefront/home/homepage.{en,id}.html` into the **HomepageText**
  topic per language via the editor's **HTML source view**; fill `[WHATSAPP_E164]`. These `.html` files are
  admin-paste content, not served directly — editing them doesn't change the live site until pasted.
- **Static checks (before every commit):** `bash deploy/qa/static-checks.sh`
- **Runtime smoke (after deploy):** `deploy/qa/smoke.sh https://YOUR_DOMAIN --product /seo --category /c/x`
- **Compile plugin+theme against real nop:** run `.github/workflows/build-nopcommerce.yml`
  (workflow_dispatch).

## Working agreements

- Branch `claude/epic-sagan-qINMg`; clear commits; push; no PR unless asked.
- Validate with `deploy/qa/static-checks.sh` before committing (JSON/CSS/JS/Razor balance, storefront
  HTML, no secrets, download-security guardrail).
- Never commit nopCommerce core or secrets. Don't weaken download security or the Midtrans webhook.
- When you override a view, base it on the real `release-4.90.4` source and preserve all
  components/widget zones. Theme views won't compile-check locally — they validate at runtime.
- Tax (PPN) and legal/refund text are **drafts** to be confirmed with the customer's accountant/lawyer.
- Explain changes to the user in plain, non-jargon language.

**START HERE:** read `README.md`, `themes/EbookIndonesia/README.md` and `IMPLEMENTATION-PROGRESS.md`,
then the theme files (`Views/Shared/Head.cshtml` + `_Header.cshtml` + `Content/css/styles.css` +
`Content/js/theme.js`) and the "Current state" section above. The build renders; the open work is
design-alignment + the admin content steps. Ask the user for a fresh screenshot (and whether they've
pasted the `HomepageText` topic) before changing anything, then continue from "Immediate next steps".
