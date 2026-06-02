# Storefront content — home page, legal pages & emails

Ready-to-use content for the eBook store: a polished, mobile-first **home page**, the
**Terms / Privacy / Refund** pages, and the **transactional email templates** (order
confirmation / payment received / download available) — all in **English + Bahasa Indonesia**.

```
storefront/
├─ home/
│  ├─ homepage.css              # reference styles (the EbookIndonesia theme already ships these)
│  ├─ homepage.en.html          # UPPER half — English    → Topic "HomepageText"
│  ├─ homepage.id.html          # UPPER half — Indonesia   → Topic "HomepageText"
│  ├─ homepage-lower.en.html    # LOWER half — English    → Topic "HomepageTextLower"
│  └─ homepage-lower.id.html    # LOWER half — Indonesia   → Topic "HomepageTextLower"
├─ legal/
│  ├─ terms.en.html   / terms.id.html      → Topic "Conditions of use"
│  ├─ privacy.en.html / privacy.id.html    → Topic "Privacy info"
│  └─ refund.en.html  / refund.id.html     → new Topic "Refund Policy"
└─ emails/                                  → Message templates (see emails/README.md)
   ├─ order-placed.en.html    / .id.html    → OrderPlaced.CustomerNotification
   ├─ order-paid.en.html      / .id.html    → OrderPaid.CustomerNotification (enable it!)
   └─ order-completed.en.html / .id.html    → OrderCompleted.CustomerNotification
```

> **Email templates** have their own step-by-step wiring guide in
> **[emails/README.md](emails/README.md)** (system names, which one to enable, per-language paste, tokens, testing).

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

The **EbookIndonesia theme** renders the home page in three parts — no core changes:

1. **Topic `HomepageText`** — upper marketing (hero → … → testimonials).
2. **Live "Ebook store"** — rendered by the theme *between* the two topics (dynamic, see below).
3. **Topic `HomepageTextLower`** — lower marketing (locations → certifications → FAQ → final CTA).

### Paste the two topics
**Admin → Content management → Topics** (enable the HTML/source view `<>` before pasting):
- **`HomepageText`** (exists by default) — edit → paste `homepage.en.html` into the **English**
  locale tab and `homepage.id.html` into the **Bahasa Indonesia** tab.
- **`HomepageTextLower`** — **Add new** → System name `HomepageTextLower`, **Published** ✔ →
  paste `homepage-lower.en.html` (English) and `homepage-lower.id.html` (Indonesia).

> Paste only the HTML — the CSS already lives in the theme stylesheet (no `<style>` block needed).

### The "Ebook store" section is dynamic — no HTML editing
Between the two topics the theme shows your **real catalogue**, in the same book-card design:
- **Books** = products you tick **Catalog → Products → (edit) → “Show on home page”**. Each card uses
  the product's first picture as the cover, plus its price, star rating, and a working **Add-to-cart**
  button (it opens the cart drawer). “New” products get a badge automatically.
- **Filter chips** = categories you tick **Catalog → Categories → (edit) → “Show on home page”**;
  each chip links to that category's catalog page.
- Curate entirely from admin — add a product, tick the box, done. Until something is ticked the
  section shows a friendly “coming soon” line.

### Wire up the links (inside the two topics)
- Legal links point to `/conditions-of-use`, `/privacy-policy`, `/refund-policy` — adjust if your
  topic SE names differ.
- The WhatsApp buttons use `https://wa.me/[WHATSAPP_E164]?text=…`.
- Numbers, names, prices, phone & addresses in the topics are design **placeholders** — replace them.

### Localization
Paste the English HTML on the English locale tab and the Bahasa Indonesia HTML on the ID tab for
**both** topics. The dynamic Ebook-store heading switches language automatically.

> The legal drafts are a starting point — have an Indonesian lawyer review Terms/Privacy/Refund,
> and confirm tax/invoice wording with your accountant (blueprint §4.4, §7).
