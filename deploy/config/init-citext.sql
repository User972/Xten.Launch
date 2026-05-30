-- Pre-creates the citext extension required by nopCommerce (case-insensitive columns).
-- Runs once, as the PostgreSQL superuser, on first database initialization.
-- This avoids the install-time error when the app DB user lacks CREATE EXTENSION rights.
-- Reference: blueprint §2.3 / §2.5.
CREATE EXTENSION IF NOT EXISTS citext;
