#!/bin/sh
# Runs once, when the database volume is first created.
# Creates the read-only role used by the analytics service. Tables do not exist yet (the C# API's
# migrations create them), so privileges are granted through DEFAULT PRIVILEGES for future tables.
set -e
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<SQL
CREATE ROLE wms_ro LOGIN PASSWORD '${WMS_RO_PASSWORD}' NOSUPERUSER NOCREATEDB NOCREATEROLE;
ALTER ROLE wms_ro SET default_transaction_read_only = on;
GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO wms_ro;
GRANT USAGE ON SCHEMA public TO wms_ro;
ALTER DEFAULT PRIVILEGES FOR ROLE ${POSTGRES_USER} IN SCHEMA public GRANT SELECT ON TABLES TO wms_ro;
SQL
