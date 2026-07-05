# @campus/shared-editor-kit

Public TypeScript interface for the **Shared Editor Kit (SEK)** — the cross-container component consumed by the **Student Desktop App** (SDA, `SDA-19`) and the **Teacher Web App** (TWA, `TWA-14`).

> **Status: type-only / interface-only.** This package ships the contract, not the components. Component implementations land in later PRs once both consumers agree on the shape. Defining the interface up front is a `docs/campus-platform-work-division.md` Section 2 Week 0 item — it unblocks both tracks before any editor UI is built.

## Features covered

| ID | Feature | Status in this package |
|---|---|---|
| [SEK-01](../docs/Campus%20platform%20architecture.md#features--shared-editor-kit-sek) | Code editor (C, C++, Python, Java, .NET, HTML, CSS, JS/TS, Node, SQL, JSON, YAML) | Interface only |
| [SEK-02](../docs/Campus%20platform%20architecture.md#features--shared-editor-kit-sek) | Document viewer & annotator (PDF/PPTX/DOCX, highlights/textboxes/ink, OCR) | Interface only |
| [SEK-03](../docs/Campus%20platform%20architecture.md#features--shared-editor-kit-sek) | Markdown notes (Obsidian-style linked notes) | Interface only |
| [SEK-04](../docs/Campus%20platform%20architecture.md#features--shared-editor-kit-sek) | Built-in image search (inside the notes editor) | Interface only |
| [SEK-05](../docs/Campus%20platform%20architecture.md#features--shared-editor-kit-sek) | Inking w/ block diagrams | **Not defined** — Won't priority; will reuse the shared `InkStroke` primitive when promoted |

## Consumers

- **TWA (React + TypeScript):** imports types directly from this package.
- **SDA (Avalonia / .NET 10):** a C# binding is **deferred to a later PR** because the SDA project is not yet scaffolded in this repo. Tracked as a follow-up to this PR — see `apps/student-desktop/` (currently empty).

## Usage

```ts
import {
  CodeEditorProps,
  Language,
  LANGUAGE_LABELS,
  Note,
  DocumentDescriptor,
  Annotation,
  Result,
  SekError,
} from '@campus/shared-editor-kit';
```

Subpath imports are also available for tree-shaking:

```ts
import type { CodeEditorProps, Language } from '@campus/shared-editor-kit/code-editor';
import type { DocumentViewerProps, Annotation } from '@campus/shared-editor-kit/document-viewer';
import type { NotesEditorProps, Note } from '@campus/shared-editor-kit/notes';
import type { ImageSearchProps } from '@campus/shared-editor-kit/image-search';
import type { UserContext, Result, SekError } from '@campus/shared-editor-kit/types';
```

## Design rules baked into the interface

These are the non-obvious decisions that came from the EARS requirements and acceptance criteria. Embedders (TWA, SDA) and the component implementor should follow them when the runtime code lands:

1. **Closed language list for SEK-01.** `Language` is a TypeScript string-literal union. The runtime surface returns `Result<CodeRunResult, SekError>` with `code: 'unsupported_language'` for any value not on the list — this enforces the spec's "a language outside the launch list shows a clear 'unsupported language' error, not a silent failure" acceptance criterion.
2. **SEK owns no persistence.** Every persistable entity is passed through a callback the embedder supplies (`onSave`, `onDelete`, `onAnnotationChange`, `onUploadImage`, …). The Backend API remains the source of truth; the table layout in `docs/campus-platform-db-api-schema.md` Part 1.9 (`notes`, `note_links`, `documents`) is what these callbacks write to.
3. **SEK owns no auth.** Every component takes a `UserContext` and forwards the session token; SEK never opens or refreshes a session itself.
4. **Wikilink resolution is `Result<Note, SekError>`, not a thrown exception.** This is the contract that backs SEK-03's "links resolve to not-found, not a crash" acceptance criterion.
5. **Image search returns a `content-addressed` URL, not the original `sourceUrl`.** This is the contract for SEK-04's "inserted image is embedded, not just linked" acceptance criterion. The embedder's `onUploadImage` is the step that makes the image survive the source going away.
6. **Annotation coordinates are normalized 0..1.** Survives zoom, retina displays, and PDF re-renders. The renderer in the embedder multiplies by the rendered page size.
7. **`InkStroke` is a vector primitive, defined in `types/common.ts`** (not `document-viewer`) precisely so SEK-05 can import it without reaching into SEK-02's module. When SEK-05 gets promoted from Won't, it will reuse this same shape for vector shape data ("stored as vector shapes, not raster ink").

## Contract change protocol

This package is on the shared-contract list (`docs/campus-platform-work-division.md` Section 6). Any change to a type already declared here — adding a new `Language`, changing an `Annotation.kind`, renaming a callback, etc. — requires a post in the shared log and a thumbs-up from the other track before merge.

## Scripts

```bash
pnpm typecheck     # tsc --noEmit — verifies the contract compiles in isolation
pnpm build         # tsc — emits .d.ts + .js to ./dist (kept out of git)
```

> **No runtime tests in this PR.** The package is type-only, so there is nothing to exercise at runtime. Tests for the component implementations will live alongside those implementations in later PRs.
