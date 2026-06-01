# eBook Indonesia theme — implementation plan & progress

Resume-able progress log. Strategy: **CSS-led design system + 3 faithful view overrides
(Head, Home, Footer) + admin/widget/content playbook + Docker integration**. Every default
widget zone and dynamic component is preserved → upgrade-safe, plugin-safe, no core changes.

Legend: ✅ done · 🟡 partial (CSS/recipe done; optional deeper override pending) · ⬜ not started

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

## Phase 2 — Homepage — ✅
- ✅ `Views/Home/Index.cshtml` — editorial narrative (brand story → value → featured → topics →
  popular → articles); renders the bilingual **`HomepageText` topic** + all components + all widget
  zones. Content comes from `storefront/home/homepage.{en,id}.html` (enhanced with who-it's-for,
  benefits, FAQ, final CTA).
- Admin step: paste the homepage content into the `HomepageText` topic (per language).

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

## Phase 7 — Docker integration & docs — ✅
- ✅ `deploy/docker-compose.yml` build context → repo root; `deploy/app/Dockerfile` copies the theme
  into the published `Nop.Web/Themes/EbookIndonesia`; runtime COPY paths fixed; root `.dockerignore`
  added to keep context lean. The Midtrans plugin is copied in and added to the solution so it
  compiles into the image (single `docker compose up --build` → theme **and** plugin).
- ✅ Docs: theme `README.md`, `docs/default-elements-decision-table.md`, this progress file; root
  README/blueprint pointers updated.

## Phase 8 — QA & polish — 🟡
- ✅ Static checks: JSON valid, CSS balanced, JS/Razor syntax sanity, no core edits, no secrets,
  download security unchanged.
- ✅ Runtime QA is now **repeatable**: `deploy/qa/smoke.sh` (theme active, security headers, the
  product/category overrides, the **download-auth guard**, and the **Midtrans webhook guard**) +
  `deploy/qa/QA-CHECKLIST.md` (manual mobile visual / functional / security / perf / a11y passes).
- ⬜ Execute the QA pass against a deployed instance and record sign-off in the checklist.

## Definition of done for the optional ⬜ items
Only pursue a view override when a concrete need can't be met by CSS + admin content. If so, base it
on the real `release-4.90.4` source for that view, preserve every component/widget zone, and add it
here.
