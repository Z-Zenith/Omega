#!/bin/sh
# Least-privilege application role for backend-api (#137 part 3).
#
# The official postgres image's default POSTGRES_USER (`campus`) is always
# granted superuser — this script provisions a non-superuser `campus_app`
# role scoped to exactly the tables backend-api touches, and
# docker-compose.yml's backend-api service now connects as `campus_app`
# (ConnectionStrings__Campus), not the `campus` superuser. `campus` is still
# used for 01_schema.sql/this init script and for local `dotnet run` without
# docker compose (appsettings.json's dev placeholder) — only the containerized
# app connection was cut over, per CLAUDE.md's "ask before changing the DB
# schema" rule (explicitly approved) and reviewed here.
#
# Verified: docker compose config renders the interpolated connection string
# correctly (Username=campus_app). Not verified against a live Postgres in
# this session (Docker daemon unavailable here — docker compose config
# parses/validates but `docker compose up` needs the daemon) — same caveat as
# prior sessions' PRs. Whoever next has Docker available should bring up
# `postgres` + `backend-api` and confirm login/CRUD-through-the-app works
# end-to-end before treating this as fully verified in a real environment.
#
# Scope verification: every GRANT below targets `ALL TABLES IN SCHEMA public`
# rather than an enumerated list, because that scope was cross-checked against
# services/backend-api/Data/AppDbContext.cs — all 49 tables created in
# 01_schema.sql have a corresponding DbSet (or, for role_default_permissions,
# an EF-managed many-to-many join table) on AppDbContext, and there are no
# tables AppDbContext maps to that 01_schema.sql doesn't create. There is
# nothing in the current schema backend-api *doesn't* touch, so a hand-typed
# per-table list would carry real risk of a typo silently dropping a grant
# with no test coverage to catch it, for no added precision over the current
# schema. If a future migration adds a table backend-api does NOT need, revisit
# this file to exclude it explicitly rather than relying on ALTER DEFAULT
# PRIVILEGES to keep granting blindly.
set -e

: "${POSTGRES_USER:?POSTGRES_USER must be set (provided by the postgres image itself)}"
: "${POSTGRES_DB:?POSTGRES_DB must be set (provided by the postgres image itself)}"

# Dev-only fallback so this script never blocks `docker compose up` for
# contributors who haven't set POSTGRES_APP_PASSWORD yet — see .env.example.
# docker-compose.yml already defaults this the same way
# (${POSTGRES_APP_PASSWORD:-campus_app_dev}), this fallback only matters for
# anyone invoking postgres outside that compose file.
APP_PASSWORD="${POSTGRES_APP_PASSWORD:-campus_app_dev}"
if [ "$APP_PASSWORD" = "campus_app_dev" ]; then
  echo "db/init/03_create_app_role.sh: POSTGRES_APP_PASSWORD not set — using the" >&2
  echo "  dev-only default for the campus_app role. Rotate before any non-dev deploy." >&2
fi

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v app_password="$APP_PASSWORD" <<-'EOSQL'
	DO $$
	BEGIN
	  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'campus_app') THEN
	    CREATE ROLE campus_app LOGIN PASSWORD :'app_password';
	  ELSE
	    ALTER ROLE campus_app LOGIN PASSWORD :'app_password';
	  END IF;
	END
	$$;

	GRANT CONNECT ON DATABASE campus TO campus_app;
	GRANT USAGE ON SCHEMA public TO campus_app;

	-- Row-level access on every table backend-api's AppDbContext maps to today
	-- (see the scope-verification note at the top of this file).
	GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO campus_app;
	GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO campus_app;

	-- Keep future migrations' tables/sequences covered automatically, provided
	-- they're created by the same role (campus) that runs 01_schema.sql. This is
	-- a convenience default, not a substitute for revisiting this file's scope
	-- comment when the schema changes meaningfully.
	ALTER DEFAULT PRIVILEGES FOR ROLE campus IN SCHEMA public
	  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO campus_app;
	ALTER DEFAULT PRIVILEGES FOR ROLE campus IN SCHEMA public
	  GRANT USAGE, SELECT ON SEQUENCES TO campus_app;

	-- Explicitly no CREATE/DROP/ALTER on the schema, no role/database
	-- administration, no superuser — campus_app can read and write rows in
	-- existing tables and nothing else.
EOSQL
