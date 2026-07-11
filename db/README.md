# PostgreSQL setup

Local Postgres container seeded from [`docs/Schema.md`](../docs/Schema.md).

## Start the container

`POSTGRES_PASSWORD` is required — `docker compose up` fails fast without it
(#137, no default committed to `docker-compose.yml` anymore). Set it in a
local `.env` (copy `.env.example`; the documented dev value is `campus_dev`)
or export it in your shell before starting:

```bash
docker compose up -d postgres
```

The first run executes everything in `db/init/` alphabetically on a fresh
data volume. The schema lives in `01_schema.sql`; default roles/permissions
in `02_seed_roles_and_permissions.sql`; `03_create_app_role.sh` provisions a
least-privilege `campus_app` role (see that script's header comment — it's
additive-only today, backend-api still connects as `campus`).

Connection (matches the credentials in `docker-compose.yml`, assuming the
documented `.env.example` dev value):

| Setting      | Value        |
|--------------|--------------|
| host         | `localhost`  |
| port         | `5432`       |
| database     | `campus`     |
| user         | `campus`     |
| password     | `campus_dev` |

Connection string for the .NET backend:

```
Host=localhost;Port=5432;Database=campus;Username=campus;Password=campus_dev
```

## Useful commands

```bash
# Tail logs
docker compose logs -f postgres

# Open a psql shell
docker compose exec postgres psql -U campus -d campus

# Tear down (keeps the named volume)
docker compose down

# Nuke the volume too — next `up` will re-seed
docker compose down -v
```

## Resetting the schema without losing the volume

The init scripts only run on a fresh data directory. To re-apply after editing
`db/init/`, drop the volume:

```bash
docker compose down -v && docker compose up -d postgres
```
