# Transactional email templates — wiring guide

Ready-to-paste **subject + HTML body** for the three digital-fulfilment emails, in
**English + Bahasa Indonesia**. These are nopCommerce **Message templates** (admin-editable,
DB-persisted content) — no code or core changes.

## What maps to what

| Business event | Template system name | Files | Default state | Action |
|---|---|---|---|---|
| **Order confirmation** (order placed, awaiting payment) | `OrderPlaced.CustomerNotification` | `order-placed.en.html` / `.id.html` | **Active** | Paste subject+body per language |
| **Payment received** (your eBooks are ready) | `OrderPaid.CustomerNotification` | `order-paid.en.html` / `.id.html` | **⚠️ Disabled** | **Tick "Is active"** + paste |
| **Download available** (how to read your eBook) | `OrderCompleted.CustomerNotification` | `order-completed.en.html` / `.id.html` | **Active** | Paste subject+body per language |

> Already built-in & active (no new content needed, just confirm they send and look right):
> **account welcome / email validation** (`Customer.WelcomeMessage`, `Customer.EmailValidationMessage`)
> and **password reset** (`Customer.PasswordRecovery`). Together with the three above, this covers the
> full transactional set in blueprint §14.

## The digital fulfilment flow these wire up

```
Order placed  → OrderPlaced  (Pending: "we got your order, complete payment")
Payment paid  → OrderPaid    (downloads ACTIVATED → %Order.Product(s)% now shows Download links)
Auto-complete → OrderCompleted ("how to read your eBook", formats, support)
```

The Midtrans webhook marks the order **Paid**, which (with *Download activation = When order is paid*)
activates downloads — so **`OrderPaid` is the key fulfilment email**. `%Order.Product(s)%` renders the
itemised table **and a per-item Download link** automatically once the order is paid.

> If you'd rather send **one** fulfilment email, keep `OrderPaid` and **disable**
> `OrderCompleted.CustomerNotification` (untick "Is active"). Default recommendation: keep both —
> Paid = "ready + links", Completed = "how to read".

## How to apply each template

1. **Admin → Content management → Message templates.**
2. Open the template by its **system name** (column above).
3. **Email account:** select your configured sending account (set one up first under
   **Configuration → Email accounts** — use a reputable SMTP relay, e.g. Amazon SES / Postmark / Brevo,
   and configure SPF/DKIM/DMARC; blueprint §14).
4. **Is active:** ensure ticked (**required for `OrderPaid`**, which ships disabled).
5. **Subject:** copy the `SUBJECT:` line from the matching `.html` file.
6. **Body:** in the rich-text editor switch to **HTML/source view**, then paste the markup from the file
   (everything below the comment block).
7. **Languages:** use the **language selector** on the template edit page — paste the **`.en.html`**
   content for English and the **`.id.html`** content for Bahasa Indonesia. Customers receive the email
   in their language.
8. **Save.**

## Before you publish — replace placeholders

- `[WHATSAPP_E164]` → your WhatsApp number in international format **without `+`** (e.g. `6281234567890`).
  (Appears once in each Body's footer.)
- Everything else is a live nopCommerce **token** and is filled automatically — do **not** replace tokens.

## Tokens used (all valid in 4.90)

`%Store.Name%` · `%Store.URL%` · `%Order.CustomerFullName%` · `%Order.OrderNumber%` ·
`%Order.OrderURLForCustomer%` · `%Order.CreatedOn%` · `%Order.PaymentMethod%` ·
`%Order.Product(s)%` (itemised table **with per-item Download links once paid**).

Links built from tokens:
- My downloads → `%Store.URL%customer/downloadableproducts`
- Legal → `%Store.URL%conditions-of-use`, `%Store.URL%privacy-policy`, `%Store.URL%refund-policy`

> The `%Order.Product(s)%` table headers (Name/Price/Qty/Total/Download) localise automatically from the
> recipient's language resources (`Messages.Order.Product(s).*`), which ship with the Indonesian language pack.

## Test

- **Quick:** use the **"Send test email"** button on the template edit page (renders with sample data).
- **End-to-end (recommended):** place a real sandbox order for a downloadable product, pay via Midtrans
  sandbox, and confirm all three emails arrive, render on mobile, and the **Download link works** from the
  `OrderPaid` email and from *My account → Downloadable products*.

## Optional: manage as code

These live in the database (persisted on the `nop_appdata`/PostgreSQL volumes), so you paste them **once**
and they survive redeploys. If you later want them applied automatically on install (reproducible as code),
they can be set programmatically from a small plugin via `IMessageTemplateService` +
`ILocalizedEntityService` — ask and it can be added.
