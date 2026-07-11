# @campus/api-client

Shared web API client consumed by **Teacher Web App** (TWA), **Admin Web App** (AWA), and
**Parent Portal** (PRT). Extracted per [issue #87](../../../issues/87): before this package
existed, all three apps carried their own copy of `src/lib/api.ts`, and the core HTTP client
(token storage, the fetch wrapper, the error type) was byte-for-byte duplicated between them —
a fix to one copy (e.g. a field rename on an endpoint) wouldn't propagate to the others.

## What's actually shared vs what stays app-local

Only genuinely identical logic moved here. Each app's `src/lib/api.ts` diffed differently
enough (see PR #87) that most of it stayed put:

| Module | Shared by | Why |
|---|---|---|
| `./http` (`request`, `ApiError`, `getToken`, `setToken`) | TWA, AWA, PRT | Byte-identical fetch wrapper, token key, and error shape in all three apps. |
| `./auth` (`login`, `LoginResponse`) | TWA, AWA | Staff login against `/auth/login`. PRT authenticates a parent via a different endpoint (`/parent/login`) returning ward identity fields, not a staff `accountType` — that stays in `apps/parent-portal/src/lib/api.ts`. |
| `./timetable` | TWA, AWA | Same DTOs/routes for reading, generating, and patching timetable slots. |
| `./events` | TWA, AWA | Same event-creation DTO/route (TWA-15 / AWA-11); only the page copy differs per role. |
| `./reports` | TWA, AWA | Same `/reports` resource — TWA files reports (`createReport`), AWA reads them back into a student record (`getReports`, AWA-07). |

Not extracted, and staying app-local:
- **`lib/auth.tsx`** (the React `AuthProvider`/`useAuth` context) in each app — the session
  shape genuinely differs per role: TWA's session carries `userId`, AWA's doesn't, and PRT's
  auth context tracks a ward (`wardStudentId`/`wardFullName`) rather than a signed-in user at
  all. Forcing one generic context would either lose type safety or need enough parametrization
  to not be worth it over three ~35-line files.
- Attendance, internal/external marks, section roster, section feedback, and the DMS message
  adapters (TWA-only); users, roles, permission grants, departments, fees (AWA-only); ward
  records and ward fees (PRT-only, plus `WARD_KEY`/`getStoredWard`/`setStoredWard`, which have
  no equivalent in the other two apps). Each of these calls `request()` from `./http` directly
  rather than re-implementing a fetch wrapper.
- `TimetablePage.tsx` — looked like a shared component going in, but TWA's and AWA's versions
  turned out to implement almost entirely different features (a teacher's read-only calendar +
  change-request/feedback flow vs. an admin's generate/patch engine); not worth forcing a
  shared component over.
- `EventsPage.tsx` — near-identical (differs only in two lines of copy text) but left as a
  stretch goal per the issue's scoping notes, since the core ask was the API client.

## Usage

```ts
import { login, getMyTimetable, ApiError, type LoginResponse } from '@campus/api-client';
```

Subpath imports are also available for tree-shaking:

```ts
import { request, ApiError, getToken, setToken } from '@campus/api-client/http';
import { login, type LoginResponse } from '@campus/api-client/auth';
import { getMyTimetable, generateTimetable, patchTimetableSlot, type TimetableSlotDto } from '@campus/api-client/timetable';
import { createEvent, type EventDto } from '@campus/api-client/events';
import { createReport, getReports, type TeacherReportDto } from '@campus/api-client/reports';
```

Each consuming app re-exports the pieces it uses from its own `src/lib/api.ts` alongside its
app-specific DTOs/endpoints, so nothing outside `lib/api.ts` had to change its import path.

## Scripts

```bash
npm run typecheck   # tsc --noEmit — verifies the package compiles in isolation
npm run build       # tsc — emits .d.ts + .js to ./dist (kept out of git)
npm test            # typecheck, then runs tests/contract.smoke.ts via `node --test`
```

`tests/contract.smoke.ts` follows the same pattern as
`packages/shared-editor-kit/tests/contract.smoke.ts`: a compile-time check that every barrel
and subpath export resolves, plus one runtime assertion that `ApiError.status` round-trips.
