# Nop.Plugin.Payments.Midtrans

A nopCommerce **4.90 / .NET 9** payment plugin that accepts Indonesian payments via
**Midtrans Snap** — **QRIS, Virtual Account (bank transfer), e-wallets (GoPay/ShopeePay/etc.)
and cards** through a single redirect integration. The order is marked **Paid automatically**
when Midtrans sends a **signature-verified** settlement notification, which in turn
**activates downloadable products** (eBooks) configured with *Download activation type =
"When order is paid"*.

> This is the one piece of custom development called for in the implementation blueprint
> (`docs/nopcommerce-ebook-indonesia-blueprint.md`, §8). It touches **no nopCommerce core code**.

## How it works

```
Checkout (method = Midtrans)
   └─ ProcessPayment: order placed, payment status = Pending
   └─ PostProcessPayment: create Snap transaction (order_id = OrderGuid, gross_amount = rounded IDR)
        └─ redirect customer to Midtrans Snap (QRIS / VA / e-wallet / card)
Customer pays on Midtrans
   ├─ Midtrans → /Plugins/PaymentMidtrans/Notify   (server-to-server, SIGNATURE VERIFIED)
   │     └─ status settlement|capture(accept) ⇒ CanMarkOrderAsPaid ⇒ MarkOrderAsPaidAsync
   │           └─ downloadable eBooks activate; "download available" email sent
   └─ Midtrans → /Plugins/PaymentMidtrans/Return    (browser redirect ⇒ order-completed page)
```

Payment truth comes from the **Notify webhook**, never the browser redirect. The signature is
`SHA512(order_id + status_code + gross_amount + ServerKey)` and is checked on every notification;
invalid signatures are rejected with `400`. Marking-as-paid is **idempotent** (safe on retries).

## Project layout

```
Nop.Plugin.Payments.Midtrans/
├─ Nop.Plugin.Payments.Midtrans.csproj   # net9.0; outputs to Nop.Web/Plugins/Payments.Midtrans
├─ plugin.json                           # SupportedVersions: ["4.90"]
├─ MidtransDefaults.cs                    # system name, route names, Snap URLs
├─ MidtransPaymentSettings.cs            # UseSandbox, ServerKey, ClientKey, fees
├─ MidtransPaymentProcessor.cs           # BasePlugin + IPaymentMethod (redirection)
├─ Infrastructure/
│  ├─ NopStartup.cs                       # registers MidtransService typed HttpClient
│  └─ RouteProvider.cs                    # admin Configure + public Notify/Return routes
├─ Services/
│  ├─ MidtransService.cs                  # Snap create-transaction + signature verification
│  ├─ SnapTransactionResponse.cs
│  └─ MidtransNotification.cs
├─ Controllers/
│  ├─ PaymentMidtransController.cs        # admin Configure (GET/POST)
│  └─ PaymentMidtransWebhookController.cs # public Notify (POST) + Return (GET)
├─ Models/ConfigurationModel.cs
├─ Components/MidtransViewComponent.cs
└─ Views/ (Configure.cshtml, PaymentInfo.cshtml, _ViewImports.cshtml)
```

## Build

This plugin builds **inside the nopCommerce source solution** (it references `Nop.Web` and the
shared `Build/ClearPluginAssemblies.proj`, exactly like the official plugins):

1. Clone the matching tag, e.g. `git clone --branch release-4.90.4 https://github.com/nopSolutions/nopCommerce`.
2. Copy this folder to `src/Plugins/Nop.Plugin.Payments.Midtrans`.
3. Add it to the solution (optional) or just build it; output lands in
   `src/Presentation/Nop.Web/Plugins/Payments.Midtrans`.
4. Build the solution (or this project) in **Release**. In Docker, this happens as part of the
   image build in `deploy/app/Dockerfile`.

> Optional: drop a 90×90 `logo.png` next to the `.csproj` and re-enable the `logo.png`
> `<Content>` item in the csproj to brand the plugin in admin.

## Install & configure

1. **Admin → Configuration → Local plugins** → find *Midtrans (Snap)* → **Install**.
2. **Admin → Configuration → Payment methods** → mark it active (downloadable carts only need this one).
3. Open the plugin's **Configure** page and set:
   - **Use sandbox** — ON while testing, OFF for production.
   - **Server key** / **Client key** — from your Midtrans dashboard (Sandbox vs Production keys).
   - **Additional fee** (optional).
4. Copy the **Payment Notification URL** shown on the Configure page
   (`https://yourdomain/Plugins/PaymentMidtrans/Notify`) into the **Midtrans dashboard →
   Settings → Configuration → Payment Notification URL**.

## Test (sandbox)

1. Set **Use sandbox = ON** with your **sandbox** Server/Client keys.
2. Place an order for a downloadable eBook; choose **Midtrans**; you're redirected to Snap.
3. Pay using a Midtrans **sandbox** method (e.g. the sandbox QRIS/VA simulator).
4. Confirm the **Notify** webhook marks the order **Paid** (see the order's notes), and the eBook
   appears under **My account → Downloadable products**.
5. Verify an **invalid/tampered** notification is rejected (signature check) — e.g. POST a bogus body.

## Scope / not included (intentional, v1)

- **Online refund/void** is not implemented — refund in the Midtrans dashboard, then use
  nopCommerce **Refund (offline)** on the order to keep statuses aligned.
- **Recurring/subscriptions** not supported (not needed for one-off eBook sales).
- Sends `transaction_details` + `customer_details` + `callbacks` only (no `item_details`), to
  avoid Midtrans' sum-mismatch validation on rounded IDR totals.

## Security notes

- The **Server Key never reaches the browser**; it's used only server-side for API auth and
  signature verification.
- The **Notify** endpoint ignores antiforgery (it's a machine endpoint) but **requires a valid
  signature**; unsigned/invalid calls get `400`.
- Always run behind HTTPS (the blueprint's Caddy/Cloudflare setup) so notifications and redirects
  are encrypted.
