#!/bin/sh
set -e

APP_DATA="/app/App_Data"
DS_FILE="${APP_DATA}/dataSettings.json"
DS_TEMPLATE="/app/config/dataSettings.template.json"

# OPTIONAL: headless seeding of the DB connection file.
# Default behavior is OFF -> use the nopCommerce install wizard (recommended; blueprint §3.3).
# Seeds only when explicitly enabled AND the file does not already exist (idempotent / wizard-safe).
if [ "${NOP_SEED_DATASETTINGS}" = "true" ] && [ ! -f "${DS_FILE}" ] && [ -f "${DS_TEMPLATE}" ]; then
	echo "[entrypoint] NOP_SEED_DATASETTINGS=true -> rendering dataSettings.json from template"
	mkdir -p "${APP_DATA}"
	export NOP_DATA_PROVIDER NOP_DB_HOST NOP_DB_PORT POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD
	envsubst < "${DS_TEMPLATE}" > "${DS_FILE}"
fi

exec "$@"
