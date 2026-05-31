# eBook Indonesia theme — implementation plan & progress

Resume-able progress log. Strategy: **CSS-led design system + 3 faithful view overrides
(Head, Home, Footer) + admin/widget/content playbook + Docker integration**. Every default
widget zone and dynamic component is preserved → upgrade-safe, plugin-safe, no core changes.

Legend: ✅ done · 🟡 partial (CSS/recipe done; optional deeper override pending) · ⬜ not started

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

## Phase 3 — Product page — 🟡
- ✅ CSS landing-page treatment: cover, headline, subtitle, **price (IDR)**, prominent Buy button,
  **free-preview** (`download-sample-button`) styling, editorial `.full-description` (what-you'll-
  learn / who-for / TOC / FAQ via admin HTML), `.product-collateral`, author (manufacturer) block,
  sticky mobile Buy bar.
- ✅ Recipe documented (README): drive landing sections via product **Full description** + **Sample
  download** + widget zones (ProductDetailsBottom etc.).
- ⬜ Optional: override `Views/Product/ProductTemplate.Simple.cshtml` for a structural inline buy
  panel / dedicated TOC / author component (deferred — fragile across upgrades; not required).

## Phase 4 — Category / topic pages — 🟡
- ✅ CSS: editorial **category description** typography (serif, admin HTML) above content-led **book
  cards** (`.item-box .product-item` with cover, title, benefit summary, price, CTA; free-preview &
  format badges via `.xt-badge`).
- ✅ Recipe: fill category Description for the editorial intro; use sub-categories/topics for guides.
- ⬜ Optional: override `Views/Catalog/CategoryTemplate.*` to add a hero band / FAQ region (deferred).

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
  added to keep context lean. Midtrans plugin copy provided as an optional commented line.
- ✅ Docs: theme `README.md`, `docs/default-elements-decision-table.md`, this progress file; root
  README/blueprint pointers updated.

## Phase 8 — QA & polish — 🟡
- ✅ Static checks: JSON valid, CSS balanced, JS/Razor syntax sanity, no core edits, no secrets,
  download security unchanged.
- ⬜ Runtime QA (needs a deployed instance): enable theme → verify homepage/product/category/blog/
  account/checkout render; mobile Lighthouse; sticky Buy bar; FAQ accordion; EN/ID switch.

## Definition of done for the optional ⬜ items
Only pursue a view override when a concrete need can't be met by CSS + admin content. If so, base it
on the real `release-4.90.4` source for that view, preserve every component/widget zone, and add it
here.
