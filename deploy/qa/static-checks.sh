#!/usr/bin/env bash
#
# Repo static checks — no external services. Used by CI (.github/workflows/ci.yml) and locally:
#   deploy/qa/static-checks.sh
#
# Validates: JSON descriptors, CSS brace balance, theme JS syntax, shell syntax, Razor brace/paren
# sanity, storefront HTML well-formedness, no committed secrets/.env, and the guardrail that the
# theme never overrides download/checkout/account views (download-security invariant).
#
# Exit 0 if all pass, 1 otherwise.

set -u
cd "$(git rev-parse --show-toplevel 2>/dev/null || (cd "$(dirname "$0")/../.." && pwd))"

FAIL=0
ok(){ printf '  \033[32mok\033[0m  %s\n' "$1"; }
no(){ printf '  \033[31mNO\033[0m  %s\n' "$1"; FAIL=$((FAIL+1)); }
sec(){ printf '\n== %s ==\n' "$1"; }

sec "JSON descriptors"
for j in \
  themes/EbookIndonesia/theme.json \
  plugins/Nop.Plugin.Payments.Midtrans/plugin.json \
  deploy/config/appsettings.redis-snippet.json
do
  if python3 -c "import json,sys; json.load(open(sys.argv[1]))" "$j" 2>/dev/null; then ok "$j"; else no "$j (invalid JSON)"; fi
done

sec "CSS brace balance"
if python3 - themes/EbookIndonesia/Content/css/styles.css <<'PY'
import sys
s=open(sys.argv[1],encoding='utf-8').read()
o,c=s.count('{'),s.count('}')
print(f"  braces {o}/{c}")
sys.exit(0 if o==c else 1)
PY
then ok "styles.css balanced"; else no "styles.css brace mismatch"; fi

sec "Theme JS syntax"
if command -v node >/dev/null 2>&1; then
  if node --check themes/EbookIndonesia/Content/js/theme.js 2>/dev/null; then ok "theme.js"; else no "theme.js syntax error"; fi
else
  printf '  -- node not installed; skipping JS syntax\n'
fi

sec "Shell syntax"
for s in deploy/qa/smoke.sh deploy/app/entrypoint.sh deploy/qa/static-checks.sh; do
  if bash -n "$s" 2>/dev/null; then ok "$s"; else no "$s syntax error"; fi
done

sec "Razor brace/paren sanity (theme views)"
if python3 - <<'PY'
import glob,sys
bad=0
for f in sorted(glob.glob('themes/EbookIndonesia/Views/**/*.cshtml', recursive=True)):
    s=open(f,encoding='utf-8').read()
    if s.count('{')!=s.count('}') or s.count('(')!=s.count(')'):
        print(f"  UNBALANCED {f}  {{}}={s.count('{')}/{s.count('}')}  ()={s.count('(')}/{s.count(')')}"); bad=1
    else:
        print(f"  ok {f}")
sys.exit(bad)
PY
then ok "theme Razor balanced"; else no "theme Razor brace/paren mismatch"; fi

sec "Storefront HTML well-formedness"
if python3 - <<'PY'
import glob,sys,html.parser
class C(html.parser.HTMLParser):
    def __init__(s): super().__init__(); s.st=[]; s.void={'br','img','hr','meta','input','link'}
    def handle_starttag(s,t,a):
        if t not in s.void: s.st.append(t)
    def handle_endtag(s,t):
        if t in s.st:
            while s.st and s.st.pop()!=t: pass
bad=0
for f in sorted(glob.glob('storefront/**/*.html', recursive=True)):
    p=C(); p.feed(open(f,encoding='utf-8').read())
    if p.st: print(f"  UNBALANCED {f} {p.st[-3:]}"); bad=1
sys.exit(bad)
PY
then ok "storefront HTML well-formed"; else no "storefront HTML not well-formed"; fi

sec "No secrets / no committed .env"
if grep -rInE '(BEGIN [A-Z ]*PRIVATE KEY|xnd_[A-Za-z0-9]|(SB-)?Mid-server-[A-Za-z0-9]|(SB-)?Mid-client-[A-Za-z0-9])' \
     --include='*.cs' --include='*.cshtml' --include='*.json' --include='*.yml' --include='*.yaml' . ; then
  no "potential secret/key literal found above"
else
  ok "no key literals in code/config"
fi
if git ls-files | grep -E '(^|/)\.env$' ; then no ".env is committed (must be git-ignored)"; else ok "no committed .env"; fi

sec "Download-security guardrail (theme adds no download/checkout/account views)"
bad="$(git ls-files 'themes/**/Views/**/*.cshtml' | grep -iE '/(Download|Checkout|OnePageCheckout|Customer|Order)[A-Za-z]*\.cshtml$' || true)"
if [ -n "$bad" ]; then printf '%s\n' "$bad"; no "theme must not override download/checkout/account views"; else ok "theme overrides only Head/Home/Footer/Product/Category"; fi

sec "No nopCommerce core committed"
if git ls-files | grep -E '(^|/)NopCommerce\.sln$|^src/Presentation/Nop\.Web/' ; then no "nopCommerce core appears committed"; else ok "no core checkout in repo"; fi

printf '\n== Summary ==\n'
[ "$FAIL" -eq 0 ] && { printf '  \033[32mAll static checks passed\033[0m\n'; exit 0; }
printf '  \033[31m%s check(s) failed\033[0m\n' "$FAIL"; exit 1
