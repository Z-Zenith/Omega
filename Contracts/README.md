# OpenAPI snapshot

`openapi.snapshot.json` is a committed snapshot of AI Services' API surface, so downstream
consumers can pin against a known contract version instead of a live running server.

Regenerate after any route/schema change and commit the resulting diff:

```bash
python -m scripts.export_openapi
```
