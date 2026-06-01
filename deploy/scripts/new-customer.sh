#!/usr/bin/env bash
# Provision a new tenant: a per-customer stack (nopCommerce app + Postgres + nginx) routed by the
# shared nginx-proxy. Scaffolds deploy/customers/<slug>/ from the template, generates secrets, and
# brings the stack up. Idempotent guard: refuses to clobber an existing tenant.
#
# Usage:
#   deploy/scripts/new-customer.sh <slug> <domain> [acme_email]
# Example:
#   deploy/scripts/new-customer.sh acme acme.example.co.id ops@acme.co.id
set -euo pipefail

SLUG=${1:-}
DOMAIN=${2:-}
if [ -z "$SLUG" ] || [ -z "$DOMAIN" ]; then
  echo "usage: $0 <slug> <domain> [acme_email]" >&2
  exit 2
fi
ACME_EMAIL=${3:-admin@${DOMAIN#*.}}

# slug must be a valid compose project name / volume prefix
if ! printf '%s' "$SLUG" | grep -Eq '^[a-z0-9][a-z0-9-]*$'; then
  echo "ERROR: slug must be lowercase letters, digits and dashes (got '$SLUG')" >&2
  exit 1
fi

# Resolve repo layout relative to this script (works from any CWD).
SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
DEPLOY_DIR=$(cd "$SCRIPT_DIR/.." && pwd)
REPO_ROOT=$(cd "$DEPLOY_DIR/.." && pwd)
TEMPLATE_DIR="$DEPLOY_DIR/customers/template"
DEST_DIR="$DEPLOY_DIR/customers/$SLUG"
NOP_VERSION=${NOP_VERSION:-release-4.90.4}
IMAGE="nop-ebook:${NOP_VERSION}"

[ -d "$DEST_DIR" ] && { echo "ERROR: tenant '$SLUG' already exists at $DEST_DIR" >&2; exit 1; }

# 1) The shared proxy network must exist (created by deploy/proxy/).
if ! docker network inspect webproxy >/dev/null 2>&1; then
  echo "ERROR: 'webproxy' network not found — start the shared proxy first:" >&2
  echo "  (cd $DEPLOY_DIR/proxy && cp -n .env.example .env && docker compose up -d)" >&2
  exit 1
fi

# 2) Build the shared app image once; every tenant reuses it.
if ! docker image inspect "$IMAGE" >/dev/null 2>&1; then
  echo ">> Building $IMAGE (first tenant only; compiles nopCommerce + plugin from source)…"
  docker build -t "$IMAGE" -f "$DEPLOY_DIR/app/Dockerfile" --build-arg "NOP_VERSION=$NOP_VERSION" "$REPO_ROOT"
fi

# 3) Scaffold the tenant directory from the template.
echo ">> Creating $DEST_DIR"
cp -r "$TEMPLATE_DIR" "$DEST_DIR"
rm -f "$DEST_DIR/.env.example"

# 4) Generate the per-tenant .env with a strong random DB password.
gen_secret() { openssl rand -base64 30 | tr -dc 'A-Za-z0-9' | cut -c1-32; }
umask 077
cat > "$DEST_DIR/.env" <<EOF
CUSTOMER=$SLUG
DOMAIN=$DOMAIN
ACME_EMAIL=$ACME_EMAIL
NOP_VERSION=$NOP_VERSION
POSTGRES_DB=nopcommerce
POSTGRES_USER=nopapp
POSTGRES_PASSWORD=$(gen_secret)
NOP_SEED_DATASETTINGS=false
EOF

# 5) Launch the tenant stack. -p <slug> isolates volumes/network per tenant.
echo ">> Starting tenant '$SLUG' ($DOMAIN)"
docker compose --project-directory "$DEST_DIR" -p "$SLUG" up -d

cat <<DONE

============================================================
Tenant '$SLUG' is up.
  Storefront : https://$DOMAIN
  Config     : $DEST_DIR/.env   (random POSTGRES_PASSWORD; keep safe — git-ignored)

Next:
  1. Point DNS for $DOMAIN at this VM's public IP (A / AAAA).
     acme-companion issues the TLS cert automatically once the name resolves here.
  2. Browse https://$DOMAIN and run the nopCommerce install wizard once:
        Database: PostgreSQL | Host: postgres | Port: 5432 | DB: nopcommerce
        user/password: from $DEST_DIR/.env  | uncheck "Create sample data"
  3. Watch cert issuance:   docker logs -f nginx-proxy-acme
  4. Tenant logs:           docker compose -p $SLUG logs -f nopcommerce
============================================================
DONE
