# Authorization Service (OpenFGA)

> **Status: not currently invoked.** `Backend API` enforces every permission
> check directly against the `role_bindings`/`roles`/`permission_grants`
> Postgres tables via `Services/PermissionService.cs` — no OpenFGA client
> exists anywhere in `services/backend-api` (see #76). `model.fga` below
> documents the intended ReBAC shape and stays in the repo as a reference for
> the permission catalog, but it is not loaded into a running store by any
> code path, and nothing keeps it in sync with `PermissionService.cs` if the
> latter changes. Treat `PermissionService.cs` + the DB tables as the single
> source of truth for authorization behavior until/unless this service is
> actually wired in.

Self-hosted OpenFGA, running via `docker compose up -d authz` (in-memory
datastore for local dev — the `campus-authz` container in `docker-compose.yml`).
The model in `model.fga` mirrors the ReBAC design in the architecture doc's
Section 9: `college` is the tenant boundary, `department` is scoped beneath it,
and role relations (`lecturer`, `hod`, `finance`, `it`, `admin`) compute the
permission catalog from Section 9's table.

`add_external_marks` and `create_timetable`'s extra grant path
(`create_timetable_grant`) are modeled as direct `[user]` relations rather than
role-derived, matching the doc: nobody holds `add_external_marks` by role
default, it's PermissionGrant-only. Backend API is responsible for syncing
`permission_grants` rows (including `expires_at`) to/from these tuples —
OpenFGA only answers "does this tuple exist right now."

## Loading the model

The DSL (`model.fga`) needs to be transformed to OpenFGA's JSON model format
and pushed to a store. From the repo root, with `docker compose up -d authz`
already running:

```bash
# 1. Transform the DSL to JSON
docker run --rm -v "$(pwd)/services/authz:/authz" openfga/cli:latest \
  model transform --file /authz/model.fga > /tmp/model.json

# 2. Create a store (once — reuse the returned id afterwards)
STORE_ID=$(curl -s -X POST http://localhost:8081/stores \
  -H "Content-Type: application/json" -d '{"name":"campus-dev"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")

# 3. Push the authorization model into that store
curl -s -X POST "http://localhost:8081/stores/$STORE_ID/authorization-models" \
  -H "Content-Type: application/json" -d @/tmp/model.json
```

The HTTP API is bound to `127.0.0.1:8081` only (not exposed beyond the host)
since this is dev-only with `authentication is disabled` — do not point this
setup at anything beyond a local machine.

## Verifying a permission check

Example: HoD approving external marks in their own department (the specific
check the Week 0 foundation checklist calls out).

```bash
# Write tuples: hod-alice is HoD of cs-dept, which belongs to cs-college
curl -s -X POST "http://localhost:8081/stores/$STORE_ID/write" \
  -H "Content-Type: application/json" -d '{
  "writes": { "tuple_keys": [
    { "user": "college:cs-college", "relation": "college", "object": "department:cs-dept" },
    { "user": "user:hod-alice", "relation": "hod", "object": "department:cs-dept" }
  ]}}'

# Check: expect {"allowed":true}
curl -s -X POST "http://localhost:8081/stores/$STORE_ID/check" \
  -H "Content-Type: application/json" -d '{
    "tuple_key": {"user":"user:hod-alice","relation":"approve_external_marks","object":"department:cs-dept"}
  }'
```

This was verified during Week 0 foundation setup, along with three more cases:
a Lecturer in the same department is correctly denied `approve_external_marks`
(HoD-only), a college-level Admin correctly inherits `create_timetable` on any
department in their college (`admin from college`), and a Lecturer with no
`PermissionGrant` is correctly denied `add_external_marks`.
