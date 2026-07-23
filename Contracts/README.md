# OpenAPI snapshot

`openapi.snapshot.json` is a committed snapshot of the API surface (Contracts records +
controller routes), for downstream consumers (e.g. a future generated `campus-api-client`)
to pin against a known contract version instead of a live running server.

It is **not** regenerated on every `dotnet build` — the generator constructs the app's host to
introspect routes, which trips `Program.cs`'s fail-fast dev-placeholder guard (#137) unless
`ASPNETCORE_ENVIRONMENT` is `Development`. Regenerate it explicitly after a contract change:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet build -p:OpenApiGenerateDocumentsOnBuild=true
```

Commit the resulting `Contracts/openapi.snapshot.json` diff alongside the contract change.
