# eBook Indonesia theme — implementation plan & progress

Resume-able progress log. Strategy: **CSS-led design system + faithful view overrides
(Head, Header, Home, Footer, LanguageSelector, Product, Category, + the live HomepageProducts/
HomepageCategories components) + admin/widget/content playbook + Docker integration**. Every default
widget zone and dynamic component is preserved → upgrade-safe, plugin-safe, no core changes.

Legend: ✅ done · 🟡 partial (CSS/recipe done; optional deeper override pending) · ⬜ not started

> **Branch / PR status (current):** work continues on `claude/jolly-gates-626mK` (sits at the `main`
> tip, no open PR yet). Lineage `zen-bell → epic-sagan → modest-tesla` (**PR #11, merged**) →
> `magical-cannon` (**PR #14, merged**) — all on `main`. Dependabot's .NET-10 PRs (#12, #13) were
> correctly closed; the .NET-9 pin + Dependabot guard are on `main`.

## Session log — LIVE eBook grid + multi-tenant deploy + cart-drawer & visual fixes — ✅ (PR #14 merged → `main`)
Everything below landed via **PR #14** (`claude/magical-cannon-H56L5`) on top of the now-merged PR #11.
- **LIVE eBook store** (`3758e8f`): the homepage "Ebook store" was static placeholder HTML (fake
  titles/prices, View → `/search`). Made dynamic with TWO component overrides — `HomepageProducts/
  Default.cshtml` renders "Show on home page" products as the theme's **book-cards** (real cover image,
  price, star rating, working Add-to-cart that opens the drawer, "New" badge from MarkAsNew), and
  `HomepageCategories/Default.cshtml` renders "Show on home page" categories as **filter chips**.
  `Index.cshtml` now renders the live `#ebooks` section **between two topics** to keep its mid-page spot;
  the homepage topic was **split** into `HomepageText` (upper: hero → testimonials) and `HomepageTextLower`
  (lower: locations → FAQ → final CTA), and the static ebook block was removed. New
  `storefront/home/homepage-lower.{en,id}.html`; `.xt-bookcard__cover`/`.xt-bookgrid` CSS added;
  `storefront/README.md` updated with the new wiring. **Admin:** tick "Show on home page" on ebook
  products + categories, and paste the **two** topic halves. No core changes. (This closes the previously
  "optional, NOT done" live-grid item.)
- **Multi-tenant deploy refactor** (`aae5ad3`): replaced the single-stack `docker-compose.yml` + `Caddyfile`
  + combined `.env.example` with `deploy/proxy/` (shared `nginx-proxy` + `acme-companion`, auto-TLS,
  host-based routing), `deploy/customers/template/` (isolated per-tenant stack: app + postgres + nginx +
  nightly `db-backup` sidecar), `deploy/scripts/new-customer.sh` (provision a tenant with random secrets),
  and `deploy/azure/` (host-VM provisioning). Reverse proxy moved **Caddy → nginx-proxy**. Added
  `.gitattributes` (force LF on scripts/compose/conf) and `.gitignore` entries for `proxy/` + per-tenant
  `.env`. `deploy/README.md` fully rewritten for the multi-tenant flow.
- **Cart drawer fixed for real** (`3a1fbd8`, `c4eebaa`, `51d7156`): the slide-over opened the dim overlay
  but the panel stayed invisible. Three root causes, all solved (see AGENT-HANDOFF gotcha #6): (1) the
  header's sticky bar + `_Root` wrappers create stacking contexts, so the body overlay painted OVER the
  drawer → **portal `#flyout-cart` to `<body>`** on init/open (overlay z:9998, drawer z:9999); (2)
  nopCommerce renders the flyout with an inline `display:none` → **`display:flex !important`** is the only
  author rule that beats it; (3) at `>=1001px` DefaultClean ships `.flyout-cart{position:absolute;z-index:
  100;width:300px}` which won by load order → **`!important` on position/top/right/height/width/z-index +
  the open transform**. Verified with headless repros against real release-4.90.4 DefaultClean CSS + `_Root`
  layout. Real cart/checkout/Midtrans flow unchanged.
- **Visual polish, CSS-only, headless-verified** (`9f46544`, `0f301fe`, `f09d1ba`): fixed the invisible
  "not sure?" programs card (compound selector restores the teal in both modes), widened the **faculty grid
  to 4-up** on desktop, restored the **beige homepage bands' side gutters** (clamp 24–64px, scoped under
  `.xt-home .xt-s--band`), and pinned the **dark-mode teal accent panels** to deep `#0F3D3E` so cream text
  stays readable (the `--xt-brand` token lightens in dark mode — gotcha #7). Verified by local headless
  render (light/dark, desktop/mobile); **no HomepageText re-paste needed**.
- **Verification status:** the homepage + cart drawer are now iterated against **headless browser renders**
  of real 4.90.4 markup — a real step past "never rendered" — but **a deployed instance has still not been
  screenshotted or smoke-tested end-to-end** (live cart → /checkout → Midtrans). That's the top next check.

## Session log — Merge main + pin .NET 9 + Dependabot guard + PR #11 — ✅ (branch `claude/modest-tesla-BvVLM`, merged)
- Merged latest `main` (Dependabot updates + GitHub Actions bumps: checkout v6, setup-dotnet v5, trivy 0.36).
- **Re-pinned Docker base images to .NET 9** (`39725ec`): Dependabot had bumped sdk+aspnet to 10; nopCommerce
  4.90.4 is a net9.0 app and won't start on a .NET-10-only runtime (and it breaks the .NET 9 constraint).
  Added Dockerfile comments to deter re-bumping.
- **`.github/dependabot.yml`**: added an `ignore` for `version-update:semver-major` on
  `mcr.microsoft.com/dotnet/{sdk,aspnet}` so the 9→10 bump can't recur (minor/patch stays allowed). Rides into
  `main` when PR #11 merges. _(Update: PR #11 has since **merged**, so the .NET 9 pin + this guard are now on
  `main` and Dependabot's .NET-10 PRs #12/#13 were closed.)_
- **PR #11 → `main`** was opened from the Claude Code UI and went green (Static checks ✅, Trivy ✅, smoke
  skipped — no STAGING_URL; `mergeable_state: clean`), then **merged** (followed by PR #14). _(This entry is
  the point-in-time record from when PR #11 was still open.)_
- Docs refreshed: `docs/AGENT-HANDOFF.md` (current state / next steps / design source / branch) + this log.

## Session log — Full "Check Homepage" build from the REAL design bundle — ✅ (branch `claude/modest-tesla-BvVLM`)
Fetched the actual Claude Design handoff (`xten-customer-portal` tar.gz via the design API), read its
README + chat transcript, and rebuilt the design understanding from the real `Check Homepage.html` (not a
secondhand description). Key facts: **"Check" is an IELTS/TOEFL/PTE tutoring brand** (Bahasa Indonesia)
whose homepage includes ONE ebook-store section; the design's cart/checkout is a front-end **mock** (the
chat itself says wiring a real gateway is "beyond this prototype" — that is this project). Per user
decision (**Full Check homepage**): reproduce the whole homepage; eBooks use the **real nopCommerce cart**;
course/trial CTAs → **WhatsApp/contact**.
- **`storefront/home/homepage.{id,en}.html` fully rewritten** to the Check structure: hero (band 7+) +
  lead-CTA card (replaces the mock signup form — real WhatsApp/phone, no dead inputs), partner strip,
  why-choose (4 numbered, last inverted), programs (5 + diagnostic helper), 5-step methodology stepper,
  faculty (4), student results (3), testimonials (teal band), **ebook showcase** (6 designed `.xt-jacket`
  covers + bundle; cards link to `/search` → real product pages/cart), branches (3), certifications/payment
  strip, 8-Q FAQ, final CTA. ID = the design's real copy; EN = translation.
- **Theme CSS §24** (`styles.css`): new components (`.xt-s/.xt-kicker/.xt-h2/.xt-headrow/.xt-lift/.xt-why/
  .xt-prog/.xt-stepper/.xt-fac/.xt-result/.xt-branch/.xt-partners/.xt-cert/.xt-lead-card/.xt-bookcard/
  .xt-stripe/.xt-tag/.xt-pill`) on the existing §21 tokens; reuses `.xt-hero/.xt-stats/.xt-quote-band/
  .xt-cta-band/.xt-pay/.xt-faq/.xt-jacket/.xt-chip/.xt-bundle`. Contained rounded panels (topic content
  sits inside the centered container — no full-bleed bands).
- **WhatsApp float now ALWAYS-ON** (`theme.js`): prefers an on-page `wa.me` link, else falls back to the
  merchant number (`61457068647`) so the pulsing button shows site-wide — fixes "no WhatsApp icon".
- **Header** (`_Header.cshtml`): nav → Program/Pengajar/Tentang/Lokasi/Ebook/FAQ (homepage anchors +
  `/search`); CTA → "Daftar Trial Gratis" (`/#trial`); utility strip → hours + phone.
- `static-checks` green. ⚠️ NEEDS a deployed **screenshot pass** to tune spacing/visuals. All stats, names,
  prices, the phone `(021)…`, addresses and the `+61` WhatsApp number are **design placeholders** — replace
  with real data before launch. Order note: the single HomepageText topic renders the whole page; keep
  nopCommerce's homepage product/category/news components empty (or they appear after the final CTA).

## Session log — EN/ID toggle + footer social icons + cart-flow verification — ✅ (branch `claude/modest-tesla-BvVLM`)
Continues the (now-merged) `epic-sagan` line — this branch contains all of it. Every view built against
real `release-4.90.4` source; `deploy/qa/static-checks.sh` green.
- **EN/ID language toggle** — added `Views/Shared/Components/LanguageSelector/Default.cshtml` (an ALLOWED
  override; not in the forbidden Download/Checkout/Customer/Order set, and static-checks confirms it).
  Renders the switch as a compact "EN / ID" segmented pill instead of stock `<select>`/flags; each option
  still links to the real `CHANGE_LANGUAGE` route + returnUrl, so localisation behaviour is unchanged.
  The 2-letter code is derived from the language Name (nopCommerce's public `LanguageModel` exposes only
  `Name`/`FlagImageFileName`, no ISO code) with a first-two-letters fallback. Styled in §23 under
  `.xt-utility` (active code = terracotta); dropped the now-dead `.language-list`/`select`/`.selector-title`
  rules it replaced.
- **Footer social → icon-circles** — CSS-only, NO view override. SocialButtons already emits
  `ul.networks > li.<network> > a`; §6 now turns each link into a 40px circle with an outline glyph via a
  CSS mask keyed on the per-network class (facebook/twitter/rss/youtube/instagram), matching the header's
  account/cart icons. No-ops for any network left blank in admin.
- **Cart flow — verified at code level against real 4.90.4 markup.** FlyoutShoppingCart emits
  `#flyout-cart .mini-shopping-cart > .count/.items/.item/.totals/.buttons`, and its View-cart/Checkout
  buttons are nopCommerce's OWN (→ CART route / CHECKOUT-or-login-as-guest route) — no mock cart. Header
  cart is `.header-links .ico-cart` with `.cart-qty`; stock JS runs `AjaxCart.init(..., '.header-links
  .cart-qty', ..., '#flyout-cart', ...)`, so our MutationObserver bumps the badge + opens the drawer on
  add. DefaultClean's only flyout rule is `.flyout-cart{display:none}` (no `.active` positioning), so our
  later `display:flex` + `html.xt-cart-open` transform win — no specificity fight. STILL NEEDS a deployed
  smoke test for the live slide-in + Midtrans redirect (`deploy/qa/smoke.sh`); admin must enable "mini
  shopping cart". (Optional admin nicety: set the `ShoppingCart.HeaderQuantity` string to `{0}` so the
  badge reads "2" not "(2)".)

## Session log — Check-reference alignment + nopCommerce 4.90 markup fixes — ✅ (branch `claude/epic-sagan-qINMg`)
After the custom header landed, a clean rebuild surfaced real issues, fixed in order:
- `664438b` **Footer** — re-skin had styled **legacy** `.footer-block .title/.list`; 4.90's FooterMenu
  emits `.footer-navigation > .footer-menu > .footer-menu__title/__list/__link`, so DefaultClean's own
  footer CSS (light-grey `.footer{#eee}`, white list box, blue Subscribe) was winning. Retargeted the real
  classes and **scoped every footer rule under `.xt-footer`** (specificity beats stylesheet load order).
  Removed dead §5 nav rules (`.top-menu/.menu-toggle/.sublist` — gone in 4.90).
- `4bf2f93` **`.xt-cta` collision + width** — header CTA reused `.xt-cta` (the homepage's button-GROUP
  container) and boxed the hero buttons; renamed header button → `.xt-headcta`. Widened the page body to
  `--xt-wrap` (~1160) to match header/footer, inside DefaultClean's `min-width:1001px` breakpoint via
  `body .master-wrapper-content` (+ defensive full-bleed `.header.xt-header`).
- `6c2e1f6` **Footer layout** — menu columns now an even row (`flex:1 1 0; min-width:0`); social → outlined chips.
- `df7d686` **Header action cluster** — hid wishlist/inbox/register; account/cart → circular icon buttons
  (SVG via base64 mask; cart badge kept); toggle moved next to the cart. ⚠️ icons render but weren't visually verified.
- `b713fe3` **Homepage hero paste-ready** — `storefront/home/homepage.{en,id}.html` finalized
  (catalogue→/search, `[STORE_NAME]` reworded out, one `[WHATSAPP_E164]` placeholder, in-file paste steps).

**Legacy-class audit:** confirmed-current (safe) — header-links/cart/`#topcartlink`, flyout cart, book
cards, language selector. Only the footer (fixed) + dead §5 nav targeted legacy classes.
**Open (admin):** paste the `HomepageText` topic; curate the footer (disable compare/recently-viewed/vendor;
populate footer columns + a `FooterInfo` topic; set store name + social URLs). **Open (code):** header nav
topics (`/free-resources`, `/about-us`); optional EN/ID toggle override; optional footer icon-circles.

## Applied design — "Check" system (Claude Design handoff) — ✅
Re-skinned the theme to the `Check Homepage.html` visual system (bundle `xten-customer-portal`):
- **Palette** (styles.css §21): deep teal `#0F3D3E` + terracotta `#D97757` on cream `#FAF6EE`, ink `#15201F`;
  tokens re-pointed so the whole storefront adopts it. **Light/dark toggle** via `[data-theme]` token
  flip (dark page `#0C1111`, cards `#161E1D`, parchment text), persisted to `localStorage` with a
  no-flash inline script in `Head.cshtml`; toggle button injected by `theme.js`.
- **Type**: Fraunces (display) + Plus Jakarta Sans (body) + JetBrains Mono (labels) via Google Fonts.
- **Components**: cream editorial hero (eyebrow pill, terracotta emphasis), trust stats, numbered
  differentiators (last inverted), teal testimonial band, deep-teal final CTA band, payment strip,
  pulsing **WhatsApp float** + terracotta-pill CTAs, terracotta focus ring.
- **Homepage content** (`storefront/home/homepage.{en,id}.html`) rebuilt to the Check structure, bilingual.
- ⚠️ Trade-off: switched from system fonts to **Google Fonts** (`display=swap` + preconnect) per the
  design — self-host/subset later if you need to claw back the request. Smoke-test light **and** dark.
- **eBook-store + cart revision** (styles.css §22, theme.js): nopCommerce product cards restyled as
  Check book-cards (small card add-to-cart = teal pill; terracotta reserved for big/bundle CTAs);
  reusable designed cover jackets (`.xt-jacket` ×6 colorways), filter chips (`.xt-chip`), bundle
  banner (`.xt-bundle`); header cart → icon + **terracotta count badge + bump on add**; nopCommerce's
  **flyout mini-cart restyled as a right slide-over drawer** (overlay/Esc close, opens on cart-click
  and on add). **The REAL nopCommerce cart/checkout/Midtrans flow is used — no mock cart/checkout,
  no localStorage cart**; the drawer's View-cart/Checkout buttons are nopCommerce's own (→ /cart, /checkout).

## Phase 1 — Theme scaffold + layout + CSS foundation — ✅
- ✅ `theme.json` (SystemName `EbookIndonesia`, descriptor per 4.90 schema).
- ✅ `Views/Shared/Head.cshtml` — injects `Content/css/styles.css` + `Content/js/theme.js` the
  official way (`NopHtml.AppendCssFileParts` / `AppendScriptParts(ResourceLocation.Footer, …)`).
- ✅ `Content/css/styles.css` — full design system: tokens, base/typography, containers, **header,
  nav, footer**, buttons/forms, cards/badges/alerts, `.xt-*` editorial library, de-emphasis, a11y,
  print, responsive.
- ✅ `Content/js/theme.js` — vanilla: FAQ accordion, **sticky mobile Buy bar** (triggers the real
  add-to-cart button — no logic bypass), smooth-scroll.

## Phase 2 — Homepage — ✅ (now the full Check design + a LIVE eBook grid)
- ✅ `Views/Home/Index.cshtml` — editorial narrative; renders **`HomepageText`** (upper) → the **live
  `#ebooks` store** (HomepageCategories chips + HomepageProducts grid) → **`HomepageTextLower`** (lower)
  → best-sellers → news → polls. **All widget zones preserved.**
- ✅ The "Ebook store" section is **dynamic** (PR #14): `HomepageProducts`/`HomepageCategories` component
  overrides render products/categories marked "Show on home page" as book-cards + filter chips (real
  cover/price/rating/Add-to-cart). No static placeholder HTML.
- Admin step: paste **two** topics — `homepage.{en,id}.html` → `HomepageText`, `homepage-lower.{en,id}.html`
  → `HomepageTextLower` (per language) — and tick "Show on home page" on ebook products + categories.
- ⬜ Deployed screenshot pass (light/dark, desktop/mobile) to tune §24 spacing — iterated via headless
  renders, not yet on a real instance.

## Phase 3 — Product page — ✅
- ✅ CSS landing-page treatment: cover, headline, subtitle, **price (IDR)**, prominent Buy button,
  **free-preview** (`download-sample-button`) styling, editorial `.full-description` (what-you'll-
  learn / who-for / TOC / FAQ via admin HTML), `.product-collateral`, author (manufacturer) block,
  sticky mobile Buy bar.
- ✅ Recipe documented (README): drive landing sections via product **Full description** + **Sample
  download** + widget zones (ProductDetailsBottom etc.).
- ✅ `Views/Product/ProductTemplate.Simple.cshtml` override — faithful to 4.90 (all partials,
  components, widget zones, ViewDataDictionary prefixes preserved). 3-area grid: editorial content
  left + **sticky buy box** right on desktop; **cover → buy → content** on mobile. **Auto Table of
  Contents** (theme.js, from description H2s) + **dedicated author block** (localized manufacturer
  partial). The add-to-cart form still wraps the whole article → cart/checkout/download unchanged.

## Phase 4 — Category / topic pages — ✅
- ✅ CSS: editorial **category description** typography (serif, admin HTML) above content-led **book
  cards** (`.item-box .product-item` with cover, title, benefit summary, price, CTA; free-preview &
  format badges via `.xt-badge`).
- ✅ Recipe: fill category Description for the editorial intro; use sub-categories/topics for guides.
- ✅ `Views/Catalog/CategoryTemplate.ProductsInGridOrLines.cshtml` override — editorial **hero band**
  (category name + description as lead) atop the curated book grid; breadcrumb, CatalogFilters,
  subcategories, featured products, selectors, the AJAX product list and all widget zones preserved.

## Phase 5 — Blog / articles — 🟡
- ✅ CSS: modern article list + readable serif article body, dates, titles, and an in-article
  **`.xt-cta-inline`** block to link naturally into a relevant eBook.
- ✅ Recipe: paste `.xt-cta-inline` into post bodies for soft commerce; keep H1/H2 hierarchy.
- ⬜ Optional: blog list/detail view overrides for byline/related-eBooks rail (deferred).

## Phase 6 — Account / downloads / checkout — 🟡
- ✅ CSS: account nav as a side card; **My downloads** emphasised with `.xt-note` guidance; clean
  order/download tables; trustworthy checkout steps; `.xt-reassure` "no shipping — instant download"
  strip; correct IDR display preserved.
- ✅ Security untouched: guest checkout OFF, validate-user-on-download ON, authorized download flow,
  Midtrans logic unchanged.
- ⬜ Optional: small download-page guidance partial / checkout reassurance via widget zone content
  (admin can add now; no override needed).

## Phase 7 — Docker integration & docs — ✅ (now MULTI-TENANT)
- ✅ `deploy/app/Dockerfile` builds the **shared** nopCommerce image: clones `release-4.90.4`, bakes in the
  theme (`Nop.Web/Themes/EbookIndonesia`) **and** the Midtrans plugin (`dotnet sln add`), builds, publishes.
- ✅ **Multi-tenant refactor (PR #14):** one VM hosts many stores. `deploy/proxy/` = shared `nginx-proxy` +
  `acme-companion` (auto-TLS, host routing); `deploy/customers/template/` = isolated per-tenant stack (app +
  postgres + nginx + nightly `db-backup` sidecar); `deploy/scripts/new-customer.sh` provisions a tenant with
  random secrets; `deploy/azure/` provisions the host VM. The old single-stack `docker-compose.yml` +
  `Caddyfile` + combined `.env.example` were removed (reverse proxy: **Caddy → nginx-proxy**).
- ✅ Docs: `deploy/README.md` rewritten for the multi-tenant flow; `storefront/README.md` updated for the
  two-topic + "Show on home page" wiring; theme `README.md`, `docs/default-elements-decision-table.md`, this
  progress file, `docs/AGENT-HANDOFF.md`, root README + blueprint refreshed.

## Phase 8 — QA & polish — 🟡
- ✅ Static checks: JSON valid, CSS balanced, JS/Razor syntax sanity, no core edits, no secrets,
  download security unchanged.
- ✅ Runtime QA is now **repeatable**: `deploy/qa/smoke.sh` (theme active, security headers, the
  product/category overrides, the **download-auth guard**, and the **Midtrans webhook guard**) +
  `deploy/qa/QA-CHECKLIST.md` (manual mobile visual / functional / security / perf / a11y passes).
- 🟡 The homepage + cart drawer have been iterated against **headless browser renders** of real 4.90.4
  markup (light/dark, desktop/mobile) — a step past "never rendered".
- ⬜ Execute the QA pass against a **real deployed instance** (deployed screenshot + live cart → /checkout
  → Midtrans smoke) and record sign-off in the checklist.

## Definition of done for the optional ⬜ items
Only pursue a view override when a concrete need can't be met by CSS + admin content. If so, base it
on the real `release-4.90.4` source for that view, preserve every component/widget zone, and add it
here.
