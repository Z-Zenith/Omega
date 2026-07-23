# @campus/direct-messaging

**Direct Messaging (DMS)** — the cross-container one-to-one student-teacher messaging
component consumed by the **Teacher Web App** (TWA, `TWA-18`) and the **Student Desktop App**
(SDA, `SDA-24`).

## Feature covered

| ID | Feature | Status in this package |
|---|---|---|
| [DMS-01](../docs/Campus%20platform%20architecture.md#features--direct-messaging-dms) | One-to-one student-teacher messaging | **Implemented** — `MessageInbox` + `MessageThreadView` |

## Consumers

- **TWA (React + TypeScript):** imports the components directly.
- **SDA (Avalonia / .NET 10):** integrated via a NativeWebView host bundle, same approach as
  the Shared Editor Kit's SDA integration — see `packages/shared-editor-kit/README.md`.
  `apps/student-desktop/StudentDesktop.csproj` copies this package's `dist/host/**` (built by
  `npm run build:host`) into the app's output under `DmsHost/`, where SDA-24's
  `MessageInbox`/`MessageThreadView` load through `Avalonia.Controls.WebView`.

## Design rules baked into the interface

Same philosophy as the Shared Editor Kit (`packages/shared-editor-kit`), applied here:

1. **DMS owns no persistence.** Every read/write goes through an embedder-supplied
   callback (`onListThreads`, `onListMessages`, `onSendMessage`). The Backend API
   (`services/backend-api/Controllers/MessagingController.cs`) is the source of truth.
2. **DMS owns no auth.** Every component takes a `UserContext` and forwards the session
   token; DMS never opens or refreshes a session itself.
3. **DMS does not enforce the "one thread per pair" rule — the Backend API does**, via a
   unique index on `(student_id, teacher_id)`. This component only renders threads it's
   given; it never fabricates or merges one.
4. **Errors are `Result<T, DmsError>`, not thrown exceptions** — same reasoning as the
   Shared Editor Kit's wikilink-resolution contract: a failed fetch or send should be
   something the embedder can branch on, not a crash.
5. **The compose box disables itself while a send is in flight** (`MessageThreadView`'s
   `sending` state) so a double-click or double-Enter cannot persist the same message twice.

## Usage

```ts
import {
  MessageInbox,
  MessageThreadView,
  getOtherPartyId,
  type UserContext,
  type ThreadSummary,
  type DirectMessage,
  type Result,
} from '@campus/direct-messaging';
```

## Scripts

```bash
npm run typecheck   # tsc --noEmit — verifies the contract compiles in isolation
npm run build       # tsc — emits .d.ts + .js to ./dist (kept out of git)
npm test            # typecheck, then build (pretest), then runtime tests via `node --test`
```

Runtime tests are split into pure-logic tests (`tests/otherParty.test.ts`, run directly
against `.ts` sources via Node's native type stripping) and render smoke tests
(`tests/render.smoke.test.mjs`, run against the *compiled* `dist/` output since Node's
type stripping doesn't perform a JSX transform). See the file-header comment in
`render.smoke.test.mjs` for what the smoke tests do and don't verify.
