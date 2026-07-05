# Work Division Plan — Campus Platform

This is a planning document, not part of the architecture. It references feature IDs and component names from `campus-platform-architecture.md` and table/endpoint names from `campus-platform-db-api-schema.md` — it doesn't restate their content. If either changes, re-check this doc's ownership table and Week 0 checklist.

*(This supersedes the Section 6 in `campus-platform-starting-guide.md` — that section now just points here so the two don't drift out of sync.)*

---

## 1. Why vertical slices, not layers or apps

Three ways to split were on the table:

- **By layer** (one person = all frontends, other = Backend+AI+DB) — rejected. At 1hr/day, whoever owns the frontend spends every session blocked on endpoints the backend owner hasn't built yet.
- **By app** (one = Student app, other = Teacher+Admin+Backend) — rejected. Backend ownership gets ambiguous the moment the Student-app owner also needs a backend change, and it becomes a shared file both people edit — the exact merge-conflict pattern to avoid.
- **Vertical slices** (each person owns a full domain end-to-end — UI, backend logic, everything for their features) — **chosen**. Each person can point Claude Code at their own slice and keep moving without waiting on the other person's session to finish first.

---

## 2. Foundation Phase (Week 0) — build together before splitting

This list changed since the last version of this doc — Docker, self-hosted OpenFGA, and self-hosted Judge0/Piston weren't decided yet before. Do all of this together, same week, ideally overlapping at least once in real time.

| Item | What "done" means |
|---|---|
| Repo, folder structure, git branching (`campus-platform-starting-guide.md` Sections 1–2) | Both have cloned the repo and pushed a test branch through the full PR flow |
| Tech stack filled into `CLAUDE.md` | No `[FILL IN]` left except AI Services provider details, which are already decided (Section 3 below) — just need API keys/config |
| **Docker Compose for local dev** | Backend API, AI Services, Authorization Service, Code Execution Service, and Database each have a Dockerfile; one `docker-compose.yml` brings all of them up together on both machines identically (architecture doc Section 11) |
| **Self-hosted OpenFGA running, with the authorization model written and tested** | The permission catalog in architecture doc Section 9 is no longer just a table — it's an actual OpenFGA model file, and a few real permission checks (e.g. "can this HoD approve external marks in their department") return the right answer against test data |
| **Self-hosted Judge0/Piston running**, sandboxed | A test submission in at least 2-3 languages from the SEK-01 launch list actually executes and returns output — and it's configured with real resource limits, not defaults, since this container runs untrusted student code |
| Database schema | The tables in `campus-platform-db-api-schema.md` exist as actual EF Core migrations, applied to a real Postgres instance running in Docker |
| API contract | The endpoints in `campus-platform-db-api-schema.md` Part 2 exist as real (even if empty-bodied) ASP.NET Core routes; OpenAPI spec generated from them |
| Auth (SDA-02, TWA-03) | Login works end-to-end for one role, against the real DB and OpenFGA |
| `Shared Editor Kit` public interface (SEK-01..05) | Interface/props defined, even if internals are stubs — this is what unblocks both tracks |

**Ongoing infra ownership:** Docker Compose config and the OpenFGA model are shared, cross-track dependencies — pick one person to be the point of contact for changes to either (swap anytime), so updates don't happen from both sides at once. Same escalation rule as any contract change (Section 6).

---

## 3. Track Assignment

Assumption stated openly, unchanged from before: this splits by domain cohesion, not by who's "better at frontend vs backend" — swap the names freely, the boundaries are what matters.

### Track 1 — Access, Scheduling & Records (39 features)

| Component | Feature IDs |
|---|---|
| Student Desktop App | SDA-01, 02, 11, 12, 13, 14, 15, 20, 21, 22, 23, 25 |
| Teacher Web App | TWA-01, 02, 03, 04, 08, 09, 10, 11, 12, 13, 15, 16, 17, 19, 20 |
| Admin Web App | AWA-01, 02, 03, 09, 10, 11, 13, 14 |
| Backend API | API-01 |
| Parent Portal | PRT-01, 02, 03 |

Covers: login/session/security, class-time lock, calendar/events, marks entry + approval, attendance, timetable (including RBAC-permission-gated access), account/role management, the whole Parent Portal.

### Track 2 — Content, Community, Messaging & AI (35 features)

| Component | Feature IDs |
|---|---|
| Student Desktop App | SDA-03, 04, 08, 10, 16, 17, 18, 19, 24 |
| Teacher Web App | TWA-05, 06, 07, 14, 18 |
| Admin Web App | AWA-04, 05, 06, 07, 08, 12 |
| Backend API | API-02, 03 |
| AI Services | AIS-01..07 (all) |
| Shared Editor Kit | SEK-01..05 (all) |
| Direct Messaging | DMS-01 |

Covers: whitelisted browser, assignments, community/groups, materials, fees, the whole AI Services container, the whole Shared Editor Kit, the whole Direct Messaging component.

**Notification Router** (inside Backend API) is shared code, not split by file — whichever track adds a routing rule for their own events, post it in the log before merging, same as any other shared-contract change.

**Code Execution Service (CEX) ownership:** it's used by SEK-01, which is Track 2's, but it's self-hosted infra like OpenFGA — treat setup/maintenance as shared foundation work (Section 2), day-to-day feature use as Track 2's.

**AI Services provider split, since it affects how Track 2 actually works day to day:**

| Feature | Provider | Why |
|---|---|---|
| AIS-02 (plagiarism) | Copyleaks (external API) | Needs a real web index — not self-hostable |
| AIS-05 (AI-content detection) | Pangram (external API) | Lowest documented false-positive rate of any evaluated detector — matters given the bias risk against non-native English writers (architecture doc Section 5) |
| AIS-03 (cross-class copy-check) | Self-hosted embedding-similarity model | Only compares your own students' submissions to each other, no internet index needed |
| AIS-01, AIS-04 (browsing summary, autograding) | Self-hosted open-weight LLM | Lower-stakes/advisory (AIS-04 is teacher-confirmed before publishing either way) |
| AIS-07 (suspicious behaviour) | Self-hosted lightweight anomaly classifier on telemetry | Not an LLM task at all — keystroke/mouse-timing pattern detection |

---

## 4. Daily Workflow (1hr/day, Claude Code)

Unchanged from the starting guide — one feature ID per session, read every diff before accepting, commit on a feature branch, PR, merge. See `campus-platform-starting-guide.md` Section 4 for the full pattern.

---

## 5. Daily & Weekly Rhythm

- **Daily (async):** one line each in the shared status log — feature ID worked on, done/blocked, anything touching a shared contract.
- **Weekly (~30 min, real-time):** merge open PRs, resolve any flagged contract changes, pick next week's feature IDs.

---

## 6. Contract-Change Protocol

Five things now count as a "shared contract" that needs a log post + thumbs-up before merging, not just two:

1. DB schema (`campus-platform-db-api-schema.md` Part 1)
2. API contract (`campus-platform-db-api-schema.md` Part 2) — regenerate the OpenAPI client after every change, no exceptions. The web apps talk to the Backend API through a TypeScript client generated from its OpenAPI spec (Kiota/NSwag) — this is what makes the C#/TypeScript language boundary type-safe. If the backend changes and the client isn't regenerated, the mismatch won't show up as a compile error the way it would in an all-C# stack — it'll show up as a confusing runtime bug days later. Treat "regenerate the client" as part of finishing an API change, not an optional cleanup step, and never hand-edit a generated client file directly.
3. OpenFGA authorization model / permission catalog (architecture doc Section 9)
4. `Shared Editor Kit` public interface
5. Docker Compose config (architecture doc Section 11)

Post before building against a new version, wait for even an async thumbs-up, then merge. This is the one rule that prevents the two of you from drifting out of sync — everything else in this plan is designed to route around needing real-time coordination, except this.

---

## 7. Rough Pacing

Unchanged: 74 features, 39/35 split, at 1hr/day with Claude Code doing implementation legwork. Expect the first couple of weeks to run slower than this while both of you are still learning git, Docker, and Claude Code's rhythm at the same time — re-estimate after your first 5-10 shipped features, not before.
