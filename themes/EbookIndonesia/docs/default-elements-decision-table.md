# Default nopCommerce elements — keep / remove / replace

How the **eBook Indonesia** theme treats stock nopCommerce storefront elements to achieve a
publisher/editorial feel with *soft* commerce. "Hidden via CSS" = `display:none` in
`Content/css/styles.css` §15. "Admin" = a catalog/setting toggle is the preferred, cleaner control
(CSS is the belt-and-braces). **No core files are modified.**

| Element | Decision | Reason | Replacement / how |
|---|---|---|---|
| **Homepage product grid** | **Replace** | Default "featured products" grid feels shop-y. | Kept as a component but restyled into editorial **book cards** (§11/§12), placed *after* the brand story in the editorial homepage (`Views/Home/Index.cshtml`). |
| **Category grid (genres)** | **Replace** | Generic grid → curated topic feel. | `HomepageCategories` kept; restyled as **topic cards**; category pages led by an editorial **category description** (admin HTML) above the book cards (§12). |
| **Manufacturer navigation** (sidebar/menu) | **Remove (nav) / Repurpose (entity)** | The *navigation block* is clutter. But Manufacturers are **repurposed as Authors/Publishers** per the blueprint. | Sidebar `block-manufacturer-navigation` hidden via CSS; the **author** still shows on the product page (`.product-manufacturers`, restyled as credibility, §11). |
| **Compare products** | **Remove** | Meaningless for eBooks. | Compare buttons + page link hidden via CSS. Also disable in **Admin → Catalog settings → "Enable product comparison" = off**. |
| **Wishlist** | **Keep, de-emphasized** | Useful "save for later" for readers; just shouldn't compete with Buy. | Restyled as a **quiet secondary** outline button (§15). Disable entirely in admin if undesired. |
| **Newsletter** | **Keep, restyled** | A publisher *wants* an email list. | Restyled pill input in the modern footer. |
| **Product tags** | **Remove** | Tag clouds read as SEO spam, not editorial. | `product-tags-box` + `block-popular-tags` hidden via CSS. (Use categories/topics for discovery.) |
| **Product reviews** | **Keep (optional)** | Social proof helps conversion; reader reviews fit a book site. | Styled cleanly; controlled by **Admin → Catalog settings → reviews** (and per product). Left enabled-but-tidy. |
| **Footer links** | **Replace** | Default footer is link-soup. | Modern 3-column footer (`Views/Shared/Components/Footer/Default.cshtml`): brand + admin **FooterInfo** topic (brand blurb + WhatsApp, bilingual) · topic columns (admin footer menu) · newsletter/social. Legal links (Terms/Privacy/Refund) come from topics flagged "include in footer". |
| **Shopping cart wording** | **Replace (relabel)** | "Add to cart" feels retail; we want "Buy"/"Beli". | Relabel via **Admin → string resources**: `Products.AddToCart` → "Buy now" / "Beli sekarang", `ShoppingCart.AddToCart`, mini-cart labels. Theme keeps the button prominent (§7) + a **sticky mobile Buy bar** (theme.js). |
| **Checkout buttons** | **Keep, restyle** | Must not change checkout logic. | Restyled to the primary CTA style; trust/“no shipping — instant download” reassurance via a checkout widget zone / order-summary copy (§14). |
| **Account links** | **Keep, restyle** | Needed for login + **My downloads**. | Account nav restyled as a clean side card; **Downloadable products** emphasised with a guidance note (`.xt-note`, §14). |
| **Recently viewed products** | **Remove** | Adds shop clutter, weak value for a small catalog. | Hidden via CSS; also **Admin → Catalog settings → "Recently viewed products" = off**. |
| Homepage **best-sellers** | **Keep, restyled** | "Popular eBooks" is editorial-friendly. | Restyled book cards, placed after featured. Toggle in admin. |
| Homepage **polls** | **Keep (low priority)** | Harmless; rarely used. | Component call preserved so no admin feature is lost; place last. |
| **"Powered by nopCommerce"** | **KEEP** | Removing it **requires the official copyright-removal key** — must not be CSS-hidden without it. | Left intact in the footer override. Hide only via the official key + admin setting. |
| **EU cookie law / GDPR** bar | **Keep** | Compliance. | Inherited from layout; styled by base + tokens. |

> Preferred order of control: **admin setting → CSS de-emphasis**. The CSS rules are a safety net so the
> store looks right even before an admin flips every toggle; the toggles remain the source of truth.
