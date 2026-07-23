"""Export a committed OpenAPI snapshot for AI Services.

Usage: python -m scripts.export_openapi
Regenerate after any route/schema change and commit the resulting diff.
"""
import json
from pathlib import Path

from src.main import app

OUTPUT_PATH = Path(__file__).resolve().parent.parent / "Contracts" / "openapi.snapshot.json"


def main() -> None:
    OUTPUT_PATH.parent.mkdir(exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(app.openapi(), indent=2) + "\n")
    print(f"Wrote {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
