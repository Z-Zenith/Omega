# Multirepo Split Plan — Omega Campus Platform

**Status:** Finalized plan, ready to execute.
**Locked decisions:** `git subtree split` for history · 10 repos per C4 container · OpenFGA + Judge0 stay untouched in orchestrator · per-repo `CLAUDE.md`, no orchestrator one.

---

## 1. Goal & non-goals

**Goal.** Decompose the single repo into one repo per architectural container (per C4 Level-2 in `docs/Campus platform architecture.md`), preserving per-component git history so `git log`, `git blame`, contributor counts, and feature-ID-anchored commit messages remain intact inside each new repo.

**Non-goals (this round).**
- No application refactor, no DTO consolidation, no contract cleanup beyond what is strictly required to make each repo buildable in isolation.
- No migration off PostgreSQL, no Judge0/OpenFGA integration — these stay as Compose-level infra in the orchestrator repo, exactly as they are. Confirmed: root `docker-compose.yml` already defines `judge0-db`/`judge0-redis`/`judge0-server`/`judge0-workers` and `authz` as plain Compose services, and neither is called from application code (`grep` for `judge0`/`openfga` across `services/backend-api/**/*.cs` returns nothing) — both really are inert infra today, so "moves as-is" is accurate, not aspirational.
- No continuous publishing pipeline yet — versioned artifacts only where the split requires them now (api-client, SEK host bundles, DMS host bundles). Consistent with §4 step 1's finding that api-client/SEK/DMS already have the relevant `build`/`build:host` scripts — this non-goal is about not adding CI-triggered auto-publish, not about missing build tooling.

---

## 2. Target layout (10 repos)

### Tier 1 — Deployable apps (6)

| New repo | Source path | Stack |
|---|---|---|
| `campus-admin-web` | `apps/admin-web/` | React 19 + Vite + Tailwind + shadcn/ui |
| `campus-teacher-web` | `apps/teacher-web/` | React 19 + Vite + Tailwind + shadcn/ui |
| `campus-parent-portal` | `apps/parent-portal/` | React 19 + Vite + Tailwind + shadcn/ui |
| `campus-student-desktop` | `apps/student-desktop/` (includes `StudentDesktop.Tests/`) | Avalonia/.NET 10, xUnit |
| `campus-ai-services` | `services/ai-services/` | Python 3.11, FastAPI, scikit-learn |
| `campus-backend` | `services/backend-api/` **+ `db/`** | ASP.NET Core/.NET 10, EF Core (database-first), raw PostgreSQL SQL |

### Tier 2 — Shared libraries (3)

| New repo | Source path | Why its own repo |
|---|---|---|
| `campus-api-client` | `packages/api-client/` | Consumed by all three web apps; must become publishable. |
| `campus-shared-editor-kit` | `packages/shared-editor-kit/` | Consumed by Teacher Web (npm) + Student Desktop (WebView host bundle). |
| `campus-direct-messaging` | `packages/direct-messaging/` | Same two-host release model as SEK. |

### Tier 3 — Orchestrator (1)

| New repo | Contents |
|---|---|
| `campus-platform` | `docker-compose.yml`, `.env.example`, `.gitignore`, `.gitattributes`, shared GitHub workflows (`codeql.yml`, `semgrep.yml`, `stale.yml`, `dependabot.yml`, `auto-merge-on-approval.yml`, `dependabot-auto-merge.yml`), `docs/`, `LICENSE`, `services/authz/`, `services/code-execution/`. Does not run any application — orchestrates pinned images from the per-app repos. |

### Explicitly removed

- `services/backend-api-tests/` — 0 tracked files (only ignored `bin/`/`obj/`). Real tests live in `services/backend-api/BackendApi.Tests/` and move with the backend.

---

## 3. History retention — `git subtree split` (Option B)

For each repo in §2, run from a fresh clone of the current `Omega`:

```bash
git clone <omega-remote-url> omega
cd omega
git remote add target <new-repo-remote-url>
git subtree split -P <path-prefix> -b split/<component>
git push target split/<component>:refs/heads/main
git push target split/<component>:refs/tags/CONTAINS_UP_TO_<omega-cutover-sha>
```

Repeat per component. Each new repo's `main` contains the full history of the original repo; its working tree is scoped to that component's paths (commits that didn't touch the component appear as no-op entries in the log — known cost of subtree split).

After all 10 are pushed, archive `Omega` (lock the default branch on GitHub; do not delete). The full history stays as the integration source of truth.

---

## 4. Preconditions — 8 PRs against current `main`

Land these sequentially before any subtree split. Each is small enough to be its own PR.

1. **Make `api-client` publishable.** It already has a working `build` script (`tsc -p tsconfig.base.json`) and `tsconfig.base.json` already emits to `./dist` — no build tooling to add. Remaining work: remove `"private": true`, point `main`/`types`/`exports` at `dist/*.js`+`dist/*.d.ts` (currently point at `src/*.ts`), change `"files"` from `["src", "README.md"]` to `["dist", "README.md"]`. `dist/` is already covered by the root `.gitignore` (`dist/` at line 7) — no per-package `.gitignore` needed. Add a `"prepare": "npm run build"` script: since step 3 pins consumers via a `github:<org>/repo#tag` git dependency, npm only auto-runs `prepare` (not `build`) on install for git-hosted deps — without it, `dist/` won't exist unless committed. api-client currently has no React peer dependency to bump (that's SEK/DMS, already `^18.0.0 || ^19.0.0` and `^18.0.0` respectively — see step 2).
2. **Split SEK + DMS release model.** Both packages already emit two artifacts today: an npm-shaped build (`build` script, same `tsc -p tsconfig.base.json` pattern) and a WebView host bundle (`build:host` → `scripts/build-host.mjs`, output at `dist/host/**`). No new build tooling needed here either — this step is purely about establishing tag conventions and cutting one `0.1.0` of each. Correction: `npm:0.x.y`/`host:0.x.y` (colon-separated) are **not valid git ref names** — git rejects `:` in tag names. Actual convention adopted: `<package>@0.x.y` for the npm artifact and `<package>-host@0.x.y` for the WebView host bundle (e.g. `shared-editor-kit@0.1.0`, `shared-editor-kit-host@0.1.0`). DMS's peer dep was `^18.0.0` only (not `^18 || ^19` like SEK); widened to match before tagging, since all three consuming web apps already run React 19.2.7. `api-client@0.1.0`, `shared-editor-kit@0.1.0`/`shared-editor-kit-host@0.1.0`, and `direct-messaging@0.1.0`/`direct-messaging-host@0.1.0` are cut as local annotated tags on `Omega` for now (not pushed) — see the note on precondition #3 below.
3. **Replace `file:` deps with version pins.** In each web app's `package.json` (`teacher-web`, `admin-web`, `parent-portal` all currently depend on one or more of `@campus/api-client`, `@campus/shared-editor-kit`, `@campus/direct-messaging` via `file:../../packages/...`) and in `apps/student-desktop/StudentDesktop.csproj` (the `<Content Include="..\..\packages\{shared-editor-kit,direct-messaging}\dist\host\**">` items — not `<None Include=...>`), pin to the tags from steps 1–2. This is what makes subtree-split repos buildable in isolation.

   **Sequencing gap found while executing this step:** the target pin format (`"github:<org>/campus-api-client#0.1.0"`) points at a *separate* `campus-api-client` repo, which doesn't exist until the cutover (§5) creates it via `git subtree split`. npm git-dependencies also can't install a subdirectory of a monorepo by tag — the package has to be the repo root, which is only true post-split. So this step doesn't actually become executable until the three shared-lib repos exist, i.e. it structurally depends on the "shared libs first" part of §5's cutover, even though it's listed as a precondition. **Resolution for now:** steps 1–2's local, unpushed annotated tags (`api-client@0.1.0`, `shared-editor-kit@0.1.0`/`shared-editor-kit-host@0.1.0`, `direct-messaging@0.1.0`/`direct-messaging-host@0.1.0`) are cut on `Omega` as placeholders so the naming convention and version are already decided. The actual `file:` → `github:<org>/campus-<pkg>#0.1.0` swap in web-app `package.json`/`StudentDesktop.csproj` is deferred to immediately after the shared-lib repos are split out in §5 — do it there, not here.
4. **Export OpenAPI from Backend API** as part of its build. Commit a snapshot at `services/backend-api/Contracts/openapi.snapshot.json`. Same for AI Services. This is the upstream of the contract — the published API client packages will pin against a commit SHA of these snapshots.
5. **Add a co-migration note** to `services/backend-api/README.md` and `db/README.md` declaring the two move together. Add `services/backend-api/MIGRATIONS.md` establishing policy (when EF migrations are eventually added, they live here, with SQL in `db/init/` and matching `Up`/`Down`).
6. **Add `.mailmap`** to the original repo mapping both author-name variants of the same email to one identity. Confirmed live in this repo's history: `Anne Ruthvik <anneruthvik9@gmail.com>` and `Ruthvik Anne <anneruthvik9@gmail.com>` are the same person under two name spellings. Without a mailmap, contributor stats in the new repos will be split across two names.
7. **Fix doc drift.** `services/authz/README.md` already correctly documents OpenFGA as reference-only/unwired — no change needed there. What's still stale: `packages/shared-editor-kit/README.md` and `packages/direct-messaging/README.md` both still say the Student Desktop (SDA) C# binding is "deferred to a later PR" and describe `apps/student-desktop/` as "currently empty" — but `StudentDesktop.csproj` already integrates both packages' `dist/host/**` bundles (SDA-19/SDA-24, see step 3). Update both READMEs to reflect that the SDA integration exists. Also fix repository-relative links in `docs/*.md` where they've drifted.
8. **Cut `cutover-pre-split` annotated tag** on `main` after merging 1–7. Freeze `main` until step 5 (§5) is complete (no further merges — protect the branch).

The current `fix/80-notification-router-completion` branch should be merged before step 8; if it isn't ready, defer the whole split rather than carving an in-flight feature through. **As of this writing, `fix/80-notification-router-completion` is the checked-out branch and is still open** — do not start precondition work (steps 1–8) on it. Preconditions 1–7 are ordinary repo changes with no dependency on #80's content; they can land on their own branch(es) once #80 merges, without waiting on anything else in this plan.

---

## 5. Cutover — 10 subtree splits in sequence

For each repo in §2:

- Run the `git subtree split` command from §3.
- Push `split/<component>` to the new repo as `main` (and to `Omega` as a `history/<component>` ref so the original repo retains a pointer to each extracted history).
- Push `CONTAINS_UP_TO_<cutover-sha>` tag.
- Add a per-repo `CLAUDE.md` written from scratch (not copied from the old one), scoped to that component: tech stack, build/test/lint commands, code conventions, feature IDs that live here, references back to `campus-platform/docs/Campus platform architecture.md`.
- Add the per-component CI workflow extracted from current `.github/workflows/*` (`web-ci.yml` matrix row, `backend-api-ci.yml`, `ai-services-ci.yml`, `student-desktop-ci.yml`).
- Add a `RELEASE.md` describing the version-tag convention adopted in step 2 of §4 (so consumers know which tag to pin).

**Process order** (minimises rework):

1. Shared libs first: `campus-api-client`, `campus-shared-editor-kit`, `campus-direct-messaging`.
2. Apps that depend on them: `campus-teacher-web`, `campus-student-desktop`.
3. Remaining apps: `campus-admin-web`, `campus-parent-portal`.
4. Services: `campus-ai-services`, `campus-backend`.
5. Orchestrator last: `campus-platform`.

---

## 6. Orchestrator repo responsibilities (after split)

`campus-platform` does not run any app itself. It:

- Provides `docker-compose.yml` that pulls **pinned images** (or, for dev, `build:` contexts pointing at per-repo tags) for `campus-backend`, `campus-ai-services`, plus `postgres`, `judge0-*`, `authz` (OpenFGA image).
- Hosts `docs/` — the four Markdown architecture/schema/start-guide/work-division docs. Path/links updated per step 7 of §4.
- Holds shared infra that no single component owns: `services/authz/model.fga` (untouched), `services/code-execution/judge0.conf` (untouched), security/scheduler GitHub workflows.
- Holds the release-compatibility matrix (`INTEGRATIONS.md`) — a small table of `campus-backend@X.Y.Z` ↔ `campus-api-client@X.Y.Z` ↔ `campus-shared-editor-kit@X.Y.Z` ↔ `campus-direct-messaging@X.Y.Z` ↔ etc.
- No `CLAUDE.md` of its own (per locked decision).

---

## 7. What the split does not change (out of scope)

- Hand-mirrored C#/TypeScript DTOs remain hand-mirrored. Generated clients are a separate, larger project. Confirmed: no codegen tooling (`NSwag`, `openapi-generator`, `openapi-typescript`, etc.) is wired into the repo today, so there's no existing pipeline this decision walks back.
- OpenFGA remains unwired. It moves as-is into the orchestrator. Wiring it in is its own project. Confirmed by `services/authz/README.md`'s own status banner ("not currently invoked... `PermissionService.cs` + the DB tables as the single source of truth") and by no OpenFGA client existing anywhere in `services/backend-api`.
- Judge0 remains uncalled. Same disposition as OpenFGA. Confirmed: `judge0-server`/`judge0-workers`/`judge0-db`/`judge0-redis` are defined in root `docker-compose.yml` as infra, but no `.cs` file under `services/backend-api` references Judge0.
- Duplicated shadcn-style UI primitives across the three web apps stay duplicated. Extracting a shared design-system repo is its own decision. Confirmed: `button.tsx`/`card.tsx` (at minimum) are byte-duplicated under `src/components/ui/` in all three of `teacher-web`, `admin-web`, `parent-portal`.
- The two-author-name variant problem is solved by `.mailmap`, not by re-authoring history. Confirmed live in `git log --all`: `Anne Ruthvik <anneruthvik9@gmail.com>` and `Ruthvik Anne <anneruthvik9@gmail.com>` are the same email under two name spellings, so this isn't a hypothetical — precondition #6 (§4) has a real, current target.

### 7a. Scope of the deferred items above

None of these are sized or scheduled by this plan — each is a distinct future project, called out here only so scope boundaries are explicit rather than a vague "someday." Which repo(s) each would touch (post-split) and its current tracking status:

| Deferred item | Would touch | Tracking status | Rough scope if picked up |
|---|---|---|---|
| Generated API clients (replace hand-mirrored DTOs) | `campus-backend` (OpenAPI source of truth) + `campus-api-client` (generated output) | Untracked — no issue exists. `#87` only covers the existing hand-written `api-client` extraction, not codegen. | Pick a generator (NSwag for C#→TS is the natural fit given ASP.NET Core), wire it to the `openapi.snapshot.json` from precondition #4 (§4), regenerate on backend release, and decide how generated types coexist with or replace `api-client`'s hand-written ones. |
| Wire OpenFGA into enforcement | `campus-platform` (orchestrator, owns `authz`) + `campus-backend` (would add an OpenFGA client to `PermissionService.cs`) | **Already decided against**, not just deferred — see architecture doc changelog: `#76` was resolved by keeping `PermissionService.cs` + Postgres tables as the sole enforcement engine, marking `model.fga` as a non-enforced reference. Explicitly "revisit only if there's a concrete driver ... e.g. true multi-college ReBAC needs outgrow the flat table model." | Only relevant if that driver materializes; not a live backlog item today. |
| Wire Judge0 into code execution | `campus-platform` (owns the `judge0-*` Compose services) + whichever app/service ends up calling it (likely `campus-backend` or a future SDA feature needing sandboxed execution) | Untracked — no issue found referencing Judge0 usage, only its infra provisioning. | Needs a concrete consumer/feature to be defined first (no acceptance-criteria row in the architecture doc currently calls for live code execution) before this has a real scope. |
| Extract a shared design-system repo (dedup shadcn primitives) | New `campus-design-system` repo (an 11th repo, not in §2's 10) + `campus-teacher-web`/`campus-admin-web`/`campus-parent-portal` (would consume it instead of local `components/ui/`) | Untracked — no issue found. | Would need to land *before or during* the split (adding it after means three more repos to re-point at a new dependency) — worth flagging as a decision point if this is prioritized, since §5's process order doesn't currently account for it. |

---

## 8. Validation after the split

For each new repo, run on the freshly-pushed `main`:

- `pnpm install && pnpm build && pnpm test` (or `dotnet test`, `pytest`) — every component must build green in isolation, with no `file:` cross-repo references.
- Open a test PR against the new repo and confirm its CI workflow runs end-to-end.

For `campus-platform`:

- `docker compose -f docker-compose.yml up` with all components pinned at their current versions, plus the OpenFGA/Judge0/Postgres services. Confirm Backend API starts, AI Services is reachable from Backend, and the four apps' published builds can connect to the backend URL from their own repos.

For the archived `Omega`:

- Verify `git log --all` still resolves to the same SHAs as before the split. Subtree splits are non-destructive on the source.

---

## 9. Risk register

| Risk | Mitigation |
|---|---|
| Subtree split leaves commits in `main` of new repos that didn't touch that component's paths. | Document this in each repo's `README.md` under "About this repo's history." Don't try to rewrite it — Option B's known cost. |
| Pinning packages to versions when CI hasn't yet exercised those versions together. | After the split, the first job in `campus-platform`'s CI is a compatibility matrix smoke test against the latest tags of all per-app repos. |
| `cutover-pre-split` tag lands while `fix/80-notification-router-completion` is still open. | Defer split until that branch merges or is closed. Do not carry an in-flight feature through a split. |
| `.mailmap` not committed before the split. | Check in precondition 6; verify with `git shortlog -sn --all` post-tag that the two name variants consolidate to one count. |
| Three web apps' `package-lock.json` files currently share inconsistent versions through `file:` resolution. | Re-run `pnpm install` (or existing tool) per repo after switching from `file:` to pinned versions, regenerate lockfiles, commit them. |
| Bootstrap teams / new clones need to discover the multirepo structure. | `campus-platform/INTEGRATIONS.md` lists every repo with purpose, current versions, and a setup order. First-time setup script (optional) clones all repos at compatible tags into a sibling directory. |

---

## 10. Execution — first concrete step

Precondition #1: make `api-client` publishable. This unblocks precondition #3 (replace `file:` deps) and sets up everything downstream. Touch files:

- `packages/api-client/package.json` — remove `"private": true`; the `"build": "tsc -p tsconfig.base.json"` script already exists (no need to add one) — just repoint `main`/`types`/`exports` from `./src/index.ts`/etc. to the `dist/` equivalents, and change `"files"` from `["src", "README.md"]` to `["dist", "README.md"]`. Add `"prepare": "npm run build"` so `npm install github:<org>/campus-api-client#0.1.0` builds `dist/` on install (git-hosted npm deps only auto-run `prepare`, not `build`).
- `packages/api-client/tsconfig.base.json` — already emits to `./dist` (`outDir: "./dist"`, `rootDir: "./src"`). No change needed.
- Root `.gitignore` already has `dist/` (line 7), which already covers `packages/api-client/dist/` — no per-package `.gitignore` needed. Since `dist/` stays untracked and the `prepare` script builds it on install, no decision needed about committing built output.
- Cut `api-client@0.1.0` tag locally; do not push to a public registry yet — pin to the tag in web-app `package.json` files as `"@campus/api-client": "github:<org>/campus-api-client#0.1.0"` (or whatever the existing team's Git-resolved dependency form is).

When precondition #1 lands, move on to #2 (SEK + DMS dual-tag — both packages' `build`/`build:host` scripts already exist, this step is tag conventions only), then #3 (replace `file:` deps with these pins across `teacher-web`, `admin-web`, `parent-portal`, and `StudentDesktop.csproj`'s `<Content Include>` items).
