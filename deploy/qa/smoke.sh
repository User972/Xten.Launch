#!/usr/bin/env bash
#
# eBook Indonesia — post-deploy smoke test (dependency: curl only).
# Verifies: site reachable, custom theme active + assets served, security headers,
# the editorial templates render, downloads are auth-gated, and the Midtrans webhook
# rejects unsigned calls. Read-only and safe to run against production.
#
# Usage:
#   deploy/qa/smoke.sh https://books.example.co.id \
#       [--product /your-ebook-seo-name] \
#       [--category /c/your-genre] \
#       [--order-item-guid <guid-of-a-REAL-paid-order-item>] \
#       [--webhook]                # also probe /Plugins/PaymentMidtrans/Notify (plugin must be installed)
#
# Env: QA_INSECURE=1  -> allow self-signed TLS (staging only).
#
# Exit code: 0 if no FAIL, 1 otherwise. WARN never fails the run.

set -u

BASE="${1:-}"
if [ -z "$BASE" ] || [ "$BASE" = "-h" ] || [ "$BASE" = "--help" ]; then
  grep -E '^#( |$)' "$0" | sed 's/^# \{0,1\}//'; exit 2
fi
shift || true
BASE="${BASE%/}"   # strip trailing slash

PRODUCT="" ; CATEGORY="" ; GUID="00000000-0000-0000-0000-000000000000" ; DO_WEBHOOK=0
while [ $# -gt 0 ]; do
  case "$1" in
    --product)         PRODUCT="${2:-}"; shift 2 ;;
    --category)        CATEGORY="${2:-}"; shift 2 ;;
    --order-item-guid) GUID="${2:-}"; shift 2 ;;
    --webhook)         DO_WEBHOOK=1; shift ;;
    *) echo "Unknown arg: $1"; exit 2 ;;
  esac
done

CURLOPTS="${QA_INSECURE:+--insecure}"
HOST="$(printf '%s' "$BASE" | sed -E 's#^https?://##; s#/.*##')"
PASS=0; FAIL=0; WARN=0
pass(){ printf '  \033[32m[PASS]\033[0m %s\n' "$1"; PASS=$((PASS+1)); }
fail(){ printf '  \033[31m[FAIL]\033[0m %s\n' "$1"; FAIL=$((FAIL+1)); }
warn(){ printf '  \033[33m[WARN]\033[0m %s\n' "$1"; WARN=$((WARN+1)); }
sec(){  printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

code(){  curl -s -o /dev/null -w '%{http_code}' --max-time 25 $CURLOPTS "$1"; }            # no redirect-follow
codeL(){ curl -s -o /dev/null -w '%{http_code}' --max-time 25 -L $CURLOPTS "$1"; }         # follow redirects
hdrs(){  curl -s -D - -o /dev/null --max-time 25 -L $CURLOPTS "$1"; }
bodyL(){ curl -s --max-time 25 -L $CURLOPTS "$1"; }

echo "Target: $BASE   (host: $HOST)"

# ---------------------------------------------------------------- reachability
sec "Reachability & TLS"
hc="$(codeL "$BASE/")"
[ "$hc" = "200" ] && pass "homepage loads (HTTP 200)" || fail "homepage HTTP $hc (expected 200)"

red="$(curl -s -o /dev/null -w '%{http_code}|%{redirect_url}' --max-time 25 $CURLOPTS "http://$HOST/" || true)"
case "$red" in
  30[1278]*"|https://"*) pass "HTTP -> HTTPS redirect ($red)";;
  *) warn "HTTP -> HTTPS redirect not detected ($red) — confirm Caddy/Cloudflare forces TLS";;
esac

# ------------------------------------------------------------- security headers
sec "Security headers (homepage)"
H="$(hdrs "$BASE/")"
grep -iq '^strict-transport-security:' <<<"$H" && pass "HSTS present" || fail "missing Strict-Transport-Security"
grep -iq '^x-content-type-options: *nosniff' <<<"$H" && pass "X-Content-Type-Options: nosniff" || fail "missing X-Content-Type-Options: nosniff"
grep -iq '^referrer-policy:' <<<"$H" && pass "Referrer-Policy present" || warn "missing Referrer-Policy"
grep -iq '^x-frame-options:' <<<"$H" && pass "X-Frame-Options present" || warn "missing X-Frame-Options (or using CSP frame-ancestors)"
grep -iq '^server: *' <<<"$H" && warn "Server header exposed ($(grep -i '^server:' <<<"$H" | tr -d '\r'))" || pass "Server header suppressed"

# ----------------------------------------------------------------- theme active
sec "Custom theme (EbookIndonesia)"
cc="$(code "$BASE/Themes/EbookIndonesia/Content/css/styles.css")"
[ "$cc" = "200" ] && pass "theme CSS served (styles.css 200)" || fail "theme CSS not served (HTTP $cc) — theme not deployed?"
jc="$(code "$BASE/Themes/EbookIndonesia/Content/js/theme.js")"
[ "$jc" = "200" ] && pass "theme JS served (theme.js 200)" || fail "theme JS not served (HTTP $jc)"
HOME_HTML="$(bodyL "$BASE/")"
grep -q 'xt-footer' <<<"$HOME_HTML" && pass "theme ACTIVE (xt-footer rendered)" \
  || fail "theme not active — enable 'eBook Indonesia (Publisher)' in Admin → Settings → General → Theme"
grep -q 'xt-hero\|xt-section' <<<"$HOME_HTML" && pass "homepage editorial content present (HomepageText topic populated)" \
  || warn "homepage editorial sections not found — paste storefront/home/homepage.{en,id}.html into the HomepageText topic"

# --------------------------------------------------------------- product page
sec "Product landing page"
if [ -n "$PRODUCT" ]; then
  P="$(bodyL "$BASE$PRODUCT")"
  grep -q 'product-details-page' <<<"$P" && pass "product page renders" || fail "product page missing (.product-details-page) at $PRODUCT"
  grep -q 'xt-pd-grid'           <<<"$P" && pass "product override active (xt-pd-grid)" || fail "product override NOT active (xt-pd-grid missing)"
  grep -q 'add-to-cart-button'   <<<"$P" && pass "buy/add-to-cart preserved" || warn "add-to-cart button not found — check the product is published & priced"
  grep -q 'xt-toc\|xt-author'    <<<"$P" && pass "TOC/author scaffold present" || warn "TOC/author block not found (fine if no full description/author set)"
else
  warn "skipped — pass --product /your-ebook-seo-name to test the product template"
fi

# --------------------------------------------------------------- category page
sec "Category (editorial) page"
if [ -n "$CATEGORY" ]; then
  Ct="$(bodyL "$BASE$CATEGORY")"
  grep -q 'category-page' <<<"$Ct" && pass "category page renders" || fail "category page missing (.category-page) at $CATEGORY"
  grep -q 'xt-cat-hero'   <<<"$Ct" && pass "category override active (xt-cat-hero)" || fail "category override NOT active (xt-cat-hero missing)"
else
  warn "skipped — pass --category /c/your-genre to test the category template"
fi

# ---------------------------------------------------- download security guard
sec "Download security (must stay auth-gated)"
# 1) anonymous My-downloads area must require login (no redirect-follow -> see the 302)
md="$(code "$BASE/customer/downloadableproducts")"
case "$md" in
  30[12378]) pass "My downloads requires login (HTTP $md)";;
  200)       fail "My downloads returned 200 while anonymous — guest checkout/account gate may be OFF";;
  *)         warn "My downloads returned HTTP $md (expected a 302 redirect to login)";;
esac
# 2) anonymous download link must NOT serve a file
dl="$(code "$BASE/download/getdownload/$GUID")"
case "$dl" in
  200) fail "anonymous download returned 200 — paid files may be exposed! Verify 'Validate user when downloading'";;
  30[12378]|401|403|404) pass "anonymous download denied (HTTP $dl, no file served)";;
  *) warn "anonymous download returned HTTP $dl (expected 302/403/404)";;
esac
[ "$GUID" = "00000000-0000-0000-0000-000000000000" ] && \
  warn "download check used a dummy GUID — for a real test pass --order-item-guid <guid of a paid order item> and confirm it's still denied while logged out"

# --------------------------------------------------- Midtrans webhook guard
if [ "$DO_WEBHOOK" = "1" ]; then
  sec "Midtrans webhook signature guard"
  wc="$(curl -s -o /dev/null -w '%{http_code}' --max-time 25 $CURLOPTS \
        -X POST "$BASE/Plugins/PaymentMidtrans/Notify" \
        -H 'Content-Type: application/json' \
        -d '{"order_id":"qa-bogus","status_code":"200","gross_amount":"1.00","signature_key":"deadbeef","transaction_status":"settlement"}')"
  case "$wc" in
    400) pass "unsigned webhook rejected (HTTP 400)";;
    404) warn "webhook route 404 — Midtrans plugin not installed/built into the image yet";;
    200) fail "unsigned webhook accepted (HTTP 200) — signature verification problem!";;
    *)   warn "webhook returned HTTP $wc (expected 400)";;
  esac
fi

# ------------------------------------------------------------------ SEO basics
sec "SEO basics"
sm="$(code "$BASE/sitemap.xml")"; [ "$sm" = "200" ] && pass "sitemap.xml 200" || warn "sitemap.xml HTTP $sm"
rb="$(code "$BASE/robots.txt")";  [ "$rb" = "200" ] && pass "robots.txt 200" || warn "robots.txt HTTP $rb"

# ---------------------------------------------------------------------- summary
printf '\n\033[1m== Summary ==\033[0m\n  PASS=%s  WARN=%s  FAIL=%s\n' "$PASS" "$WARN" "$FAIL"
[ "$FAIL" -eq 0 ] && { printf '  \033[32mSmoke test OK\033[0m\n'; exit 0; }
printf '  \033[31mSmoke test FAILED\033[0m\n'; exit 1
