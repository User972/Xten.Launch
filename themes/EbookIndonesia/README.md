# eBook Indonesia — nopCommerce 4.90 publisher theme

A custom nopCommerce **4.90 / .NET 9** theme that makes the storefront feel like a **modern
publisher / curated eBook platform** rather than a generic product-grid shop. Soft commerce,
editorial content, mobile-first, fast, bilingual (English + Bahasa Indonesia).

> **Naming:** nopCommerce themes are plain folders whose name **must equal** the `SystemName`
> in `theme.json` (e.g. `DefaultClean`). Themes aren't .NET projects, so we drop the
> `Nop.Plugin.`-style prefix and use the clean name **`EbookIndonesia`** (also avoids dotted
> static paths like `/Themes/Nop.Theme.../...`).

## How it works (design philosophy)

The theme is **CSS-led with three small, faithful view overrides** — the most upgrade-safe
approach. It does **not** rewrite nopCommerce's big templates; it restyles their existing markup
and drives editorial content through **admin-editable topics, widget zones, category/product
descriptions, and settings**. This keeps it resilient across nopCommerce/.NET upgrades and never
breaks plugin hooks (every default widget zone is preserved).

```
themes/EbookIndonesia/
├─ theme.json                                  # descriptor (SystemName=EbookIndonesia)
├─ preview.jpg                                 # ADD: admin theme-picker thumbnail (optional)
├─ Content/
│  ├─ css/styles.css                           # the whole design system (tokens→pages→a11y)
│  ├─ js/theme.js                              # vanilla: FAQ accordion, sticky Buy bar, smooth-scroll
│  └─ images/README.md                         # what images to add (none shipped)
├─ Views/
│  ├─ Shared/Head.cshtml                        # injects the theme CSS + JS (override)
│  ├─ Shared/Components/Footer/Default.cshtml   # modern footer (override; keeps all components)
│  └─ Home/Index.cshtml                         # editorial homepage (override; keeps all zones)
├─ docs/default-elements-decision-table.md      # keep/remove/replace decisions
├─ IMPLEMENTATION-PROGRESS.md                   # phased plan + status (resume here)
└─ README.md                                    # this file
```

## Enable the theme

1. Build/deploy with the theme in the image (see **Docker** below) — or copy
   `themes/EbookIndonesia/` to `…/Nop.Web/Themes/EbookIndonesia/` in a running install.
2. **Admin → Configuration → Settings → General settings → "Theme"** → choose **eBook Indonesia
   (Publisher)** → Save. (Optionally set it per store.)
3. Hard-refresh the storefront. Done — no restart needed for theme switch.

## Admin configuration that brings it to life

The theme is the *frame*; content is admin-managed (so it stays editable and bilingual):

| Goal | Admin action |
|---|---|
| **Homepage brand story / hero / value / FAQ / WhatsApp** | Paste `storefront/home/homepage.en.html` and `…id.html` into the **`HomepageText`** topic (Content management → Topics), per language. The theme styles the `.xt-*` markup automatically. |
| **Featured / popular eBooks on home** | Mark products *Show on home page* / *Best seller*; they render as editorial book cards. |
| **Topic/genre cards on home** | Mark categories *Show on home page*. |
| **Editorial category pages** | Fill each category's **Description** with editorial intro + value copy (HTML). The book grid follows below. |
| **Landing-page product pages** | Put *What you'll learn / Who it's for / Table of contents / FAQ* in the product **Full description** (HTML; use `<h2>/<h3>/<ul>` and `.xt-faq`). Upload a **Sample download** for the free preview button. |
| **Footer brand blurb + WhatsApp** | Create a **`FooterInfo`** topic (per language) with a short blurb + a `https://wa.me/<number>` link. Flag Terms/Privacy/Refund topics *Include in footer*. |
| **"Buy now" wording** | Relabel `Products.AddToCart` (and cart labels) via **Configuration → Languages → string resources** to "Buy now" / "Beli sekarang". |
| **De-emphasise shop bits** | Disable compare + recently-viewed in **Catalog settings** (CSS hides them too). See the decision table. |
| **Checkout reassurance** | Add a short "no shipping — instant download after payment" note via a checkout widget zone or order-summary content. |

> Security/commerce rules are untouched: guest checkout stays OFF, validate-user-on-download stays
> ON, downloadable files keep flowing through nopCommerce's authorized download controller, and the
> Midtrans payment flow is unchanged.

## Docker / deployment

The build context is the **repository root** (`deploy/docker-compose.yml`), and the Dockerfile
copies the theme into the cloned source before publish:

```dockerfile
COPY themes/EbookIndonesia/ src/Presentation/Nop.Web/Themes/EbookIndonesia/
```

nopCommerce globs `Themes/**` as content, so the theme's Views/CSS/JS are published exactly like
the built-in `DefaultClean` theme — no extra wiring, no static-file config. A repo-root
`.dockerignore` keeps the context lean. After deploy, enable the theme in admin (above).

> If a future nopCommerce csproj ever stops globbing `Themes/**`, add the theme folder to the
> Nop.Web content include — but this has been the default for years.

## Known limitations

- **Razor views compile at runtime** (nopCommerce uses runtime view compilation for themes), so the
  three `.cshtml` overrides are validated when first served, not at `docker build`. Smoke-test the
  homepage, a product page, and the footer after deploy.
- Deeper page restructures (full product/category/checkout template rewrites) were intentionally
  **not** done — they'd be fragile across upgrades. The editorial feel is achieved via CSS + admin
  content. If a future requirement truly needs a structural change, override the specific partial
  (e.g. `Views/Product/ProductTemplate.Simple.cshtml`) — tracked in `IMPLEMENTATION-PROGRESS.md`.
- `preview.jpg` and the store logo are not shipped (add `preview.jpg`; upload the logo in admin).
- CSS targets nopCommerce 4.90 class names; selectors that don't match are simply inert (safe), but
  re-verify after a major nopCommerce upgrade.

## Future improvements

- Optional partial overrides for an even stronger product "landing page" (sticky buy panel inline,
  TOC component, author block) once content patterns settle.
- A small "HTML widget" usage guide for richer category/blog CTAs.
- `preview.jpg` + brand logo + OG image set.
- Optional dark-mode token set (the CSS is already variable-driven).
