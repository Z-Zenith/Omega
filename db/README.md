# PostgreSQL setup

Local Postgres container seeded from [`docs/Schema.md`](../docs/Schema.md).

## Start the container

```bash
docker compose up -d postgres
```

The first run executes everything in `db/init/` alphabetically on a fresh
data volume. The schema lives in `01_schema.sql`; default roles/permissions
in `02_seed_roles_and_permissions.sql`.

Connection (matches the credentials in `docker-compose.yml`):

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
