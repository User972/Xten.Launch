# Runtime QA checklist (Phase 8)

Repeatable post-deploy QA for the eBook Indonesia store + theme. Run after every deploy or theme
change. Complements the go-live list in `docs/nopcommerce-ebook-indonesia-blueprint.md` §17 — this
one focuses on the **theme** and the **runtime behaviours** that static checks can't catch (Razor
compiles at runtime in nopCommerce).

## 1. Automated smoke test (1 command)

```bash
deploy/qa/smoke.sh https://YOUR_DOMAIN \
  --product /your-ebook-seo-name \
  --category /c/your-genre \
  --webhook            # once the Midtrans plugin is installed
# staging with self-signed TLS:  QA_INSECURE=1 deploy/qa/smoke.sh https://staging.local ...
```

It checks (✅ = must pass): homepage 200 ✅ · HTTP→HTTPS redirect · security headers (HSTS ✅,
X-Content-Type-Options ✅, Referrer-Policy, X-Frame-Options, Server suppressed) · theme CSS/JS
served ✅ · **theme active** (`xt-footer`) ✅ · homepage editorial content present · product page +
`xt-pd-grid` override + add-to-cart ✅ · category page + `xt-cat-hero` override ✅ · **My downloads
requires login** ✅ · **anonymous download denied** ✅ · Midtrans webhook rejects unsigned calls ·
sitemap/robots. Exit code is non-zero if any ✅ check fails.

> For a definitive download-guard test, pass `--order-item-guid <guid of a REAL paid order item>`
> and confirm it's **still denied** while logged out.

## 2. Theme visual QA — mobile first (manual)

Test on a real phone (or DevTools device mode) **and** desktop, in **both EN and ID**:

- [ ] **Homepage** reads top→bottom as a story: hero → value → how-it-works → who-it's-for →
      featured eBooks → topics → FAQ → WhatsApp. FAQ items expand/collapse (theme.js). Language
      switch flips the HomepageText content.
- [ ] **Product page (desktop):** content left, **buy box sticky** on scroll, cover above content;
      auto **Table of Contents** appears (if the description has 2+ `<h2>`); **author block** shows.
- [ ] **Product page (mobile):** order is **cover → buy box → description**; the **sticky Buy bar**
      appears at the bottom when the main button scrolls out and triggers add-to-cart when tapped.
- [ ] **"Download free preview"** button shows when a sample is uploaded.
- [ ] **Category page:** editorial **hero band** (name + description) above content-led book cards
      (cover, title, benefit summary, price, CTA; free-preview/format badges if used).
- [ ] **Blog/article:** readable serif body; in-article `.xt-cta-inline` links to a relevant eBook.
- [ ] **Footer:** brand + FooterInfo topic + WhatsApp, topic columns, newsletter; "Powered by
      nopCommerce" still present.
- [ ] No layout shift / broken grids; images lazy-load; no console errors.

## 3. Functional QA (manual, Midtrans sandbox)

- [ ] Browse → buy an eBook → redirected to Midtrans → pay via **QRIS** and **Virtual Account**.
- [ ] Order auto-marked **Paid** (order notes show the webhook); **download appears** in
      *My account → Downloadable products*; **download-available email** arrives (EN/ID).
- [ ] **Re-download** works within the limit; exceeding the cap is blocked.
- [ ] **Guest checkout is impossible** — checkout requires login/registration.
- [ ] **Password reset** email works.

## 4. Security QA (manual, confirms the script)

- [ ] With a **real paid order**, copy the download link, open it in a **logged-out/incognito**
      browser → **denied** (not the file). Repeat as a **different** logged-in customer → denied.
- [ ] Admin → Order settings: **"Validate user when downloading downloadable products" = ON**,
      **guest checkout = OFF**.
- [ ] Tamper a Midtrans notification (wrong `signature_key`) → endpoint returns **400** (no mark-as-paid).
- [ ] Security headers present (smoke test) and no stack traces on errors (prod env).

## 5. Performance / SEO (manual)

- [ ] Lighthouse **mobile** Performance/Best-Practices/SEO/Accessibility — note scores; aim ≥ 90.
- [ ] Exactly one `<h1>` per page; sensible H2/H3 order; product/category/article meta set.
- [ ] `sitemap.xml` + Google Search Console submitted; `hreflang` present for `id`/`en`.
- [ ] No render-blocking bloat; Redis cache hit after warm-up (pages fast on repeat).

## 6. Accessibility (manual)

- [ ] Keyboard-only: nav, FAQ accordion, TOC links, buy button reachable with visible focus rings.
- [ ] Color contrast OK; product cover images have alt text.

## Sign-off
- Date: ________  ·  Build/commit: ________  ·  By: ________
- smoke.sh: PASS / FAIL (attach output)  ·  Blockers: ________
