# Storefront content — home page & legal pages

Ready-to-use content for the eBook store: a polished, mobile-first **home page** and the
**Terms / Privacy / Refund** pages, in **English + Bahasa Indonesia**.

```
storefront/
├─ home/
│  ├─ homepage.css        # styles (mobile-first, prefix xt-)
│  ├─ homepage.en.html    # home markup (English)
│  └─ homepage.id.html    # home markup (Bahasa Indonesia)
└─ legal/
   ├─ terms.en.html   / terms.id.html      → Topic "Conditions of use"
   ├─ privacy.en.html / privacy.id.html    → Topic "Privacy info"
   └─ refund.en.html  / refund.id.html     → new Topic "Refund Policy"
```

> Replace the bracketed placeholders everywhere before publishing:
> `[STORE_NAME] [LEGAL_ENTITY] [DOMAIN] [SUPPORT_EMAIL] [WHATSAPP] [CITY] [DATE] [N] [CATALOGUE_URL] [WHATSAPP_E164]`.
> `[WHATSAPP_E164]` is the number in international format without `+` (e.g. `6281234567890`).

## Legal pages (Topics) — pure admin config

1. **Admin → Content management → Topics.**
2. **Conditions of use** and **Privacy info** already exist — edit each and paste the matching
   `*.en.html` into the English locale and `*.id.html` into the Bahasa Indonesia locale
   (use the language tabs; enable the HTML/source view in the rich-text editor before pasting).
3. **Refund Policy** does not exist by default — **Add new**:
   - System name: `RefundPolicy` · Title: *Refund Policy* / *Kebijakan Pengembalian Dana*
   - URL/SE name: `refund-policy` · **Published** ✔ · **Include in footer** ✔
   - Paste `refund.en.html` / `refund.id.html` per locale.
4. Make sure Terms, Privacy and Refund all appear in the **footer**, and that
   **Order settings → "Terms of service enabled"** points at the Conditions of use topic
   (blueprint §6–§7).

## Home page

The home markup is theme-agnostic and uses only `xt-`-prefixed classes. Two ways to use it:

**Option A — HTML widget (no code).** Install an HTML-content widget (e.g. a marketplace
"HTML widget" plugin), target the **`home_page_top`** widget zone, paste `homepage.en.html`
(and the Bahasa Indonesia version on the ID-language widget), and put the contents of
`homepage.css` inside a `<style>…</style>` block at the top of the markup.

**Option B — theme override (recommended for production).** In your custom theme, add the
markup to `Views/Home/Index.cshtml` (or a partial) and move `homepage.css` into the theme's
stylesheet/bundle. This keeps HTML and CSS separate and is the cleanest, upgrade-safe route —
**no nopCommerce core changes**.

### Wire up the links
- `[CATALOGUE_URL]` → your catalogue/search entry point (e.g. `/search`, or a top category `/c/…`).
- Legal links point to `/conditions-of-use`, `/privacy-policy`, `/refund-policy` — adjust if your
  topic SE names differ.
- The WhatsApp button uses `https://wa.me/[WHATSAPP_E164]?text=…`.

### Localization
Show the English markup on the English store and the Bahasa Indonesia markup on the ID store.
With the HTML-widget approach, create one widget per language; with the theme approach, switch
copy by the current working language (nopCommerce localized resources or a language check).

> The legal drafts are a starting point — have an Indonesian lawyer review Terms/Privacy/Refund,
> and confirm tax/invoice wording with your accountant (blueprint §4.4, §7).
