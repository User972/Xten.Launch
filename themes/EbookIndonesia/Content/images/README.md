# Theme images

This theme ships **no binary images** in git (keeps the repo clean). Add these where noted:

| Asset | Where it goes | Notes |
|---|---|---|
| `preview.jpg` | `themes/EbookIndonesia/preview.jpg` (theme root) | ~600×400 screenshot shown in **Admin → Configuration → Settings → General → Theme** picker. Cosmetic — a missing file only shows a broken thumbnail in admin; the theme still works. |
| **Store logo** | Uploaded in **admin**, not the theme | nopCommerce manages the logo via the store logo setting (Appearance). Do not hardcode a logo in the theme. |
| Hero / OG image (optional) | `themes/EbookIndonesia/Content/images/` | Reference from the `HomepageText` topic or social/OG settings. Keep it optimized (WebP, < 150 KB) and lazy-loaded — see the performance notes in the theme README. |

The theme's visual design is CSS-driven (gradients, type, spacing), so it looks complete with
zero images. Images are purely optional brand polish.
