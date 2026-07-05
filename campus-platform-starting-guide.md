# Starting Guide — Campus Platform (Claude Code, Git, Setup)

Covers repo setup, git workflow, and how to run Claude Code day-to-day. The track split and work division live in `campus-platform-work-division.md` — that file gets updated as the architecture evolves; this one shouldn't need to change alongside it. Refer to both by feature ID; nothing here duplicates their content.

Written for two people new to both git and Claude Code. Claude Code is already installed on both machines — this starts from an empty folder.

---

## 1. Repo Setup

One person does this once; the other clones it.

```bash
# Person A: create and push the repo (do this on GitHub.com first: New Repository → "campus-platform" → do NOT initialize with a README)
git clone git@github.com:<your-org-or-username>/campus-platform.git
cd campus-platform
```

Create the folder structure — this mirrors the containers in Section 0/7 of the architecture doc, so "where does this code go" is never a guess:

```bash
mkdir -p apps/student-desktop apps/teacher-web apps/admin-web apps/parent-portal
mkdir -p services/backend-api services/ai-services services/authz
mkdir -p packages/shared-editor-kit packages/direct-messaging
mkdir -p docs
```

```
campus-platform/
├── CLAUDE.md                      ← Claude Code reads this automatically, every session
├── README.md
├── .gitignore
├── docs/
│   ├── architecture.md            ← copy of campus-platform-architecture.md
│   └── work-division.md           ← copy of this file
├── apps/
│   ├── student-desktop/           ← SDA (Avalonia/.NET)
│   ├── teacher-web/                ← TWA
│   ├── admin-web/                  ← AWA
│   └── parent-portal/              ← PRT
├── services/
│   ├── backend-api/                ← API
│   ├── ai-services/                 ← AIS
│   └── authz/                       ← OpenFGA schema/config
└── packages/
    ├── shared-editor-kit/           ← SEK (consumed by student-desktop and teacher-web)
    └── direct-messaging/            ← DMS (consumed by student-desktop and teacher-web)
```

Copy the two docs in, write a one-paragraph README, add a `.gitignore` (use [gitignore.io](https://gitignore.io) for whatever languages you land on in Week 0 — see Section 3), then:

```bash
git add .
git commit -m "chore: initial repo structure"
git push origin main
```

**On GitHub:** Settings → Branches → add a rule for `main` requiring at least one pull-request review before merging. This is the one guardrail worth setting up on day one — it makes "accidentally pushed broken code straight to main" impossible for both of you.

```bash
# Person B: clone it
git clone git@github.com:<your-org-or-username>/campus-platform.git
cd campus-platform
```

---

## 2. Git Branching — Feature Branches, Not User Branches

**Don't create a branch per person** (e.g. `ruthvik-dev`). Those live too long, drift from `main`, and turn merges into a mess of unrelated changes tangled together. Use **one branch per feature ID** instead — it maps directly onto "one Claude Code session = one feature = one branch = one PR," which is exactly the size that fits a 1hr/day cadence.

**Naming:** `feature/<feature-id>-<short-name>`, `fix/<short-name>`, `chore/<short-name>`

```bash
# Start a feature — always branch from an up-to-date main
git checkout main
git pull
git checkout -b feature/sda-11-autosubmit

# ... do the work with Claude Code (Section 4) ...

git add .
git commit -m "feat: SDA-11 auto-submit assignment on exit during window"
git push origin feature/sda-11-autosubmit
# Open a Pull Request on GitHub, targeting main
```

Once the PR is approved (even a quick self-check if the other person is busy — don't let review become a bottleneck at this scale) and merged:

```bash
git checkout main
git pull
git branch -d feature/sda-11-autosubmit   # delete the local branch, it's done its job
```

**Commit message format:** `<type>: <what changed>` — types are `feat`, `fix`, `refactor`, `test`, `docs`, `chore`. One feature ID per commit where possible; if a feature needs several commits, that's fine, just keep each commit doing one logical thing.

**If something breaks mid-session:** `git diff` to see what changed, `git checkout -- <file>` to discard a single file's changes, or `git reset --hard HEAD` to throw away everything since the last commit. This is why committing often matters — you never lose more than one small increment.

---

## 3. CLAUDE.md — Fill This In Together, Week 0

This file is the single highest-leverage thing you'll write — Claude Code reads it automatically at the start of every session in this repo. Draft:

```markdown
# Campus Digitalization Platform

## What this is
See docs/architecture.md for the full picture. Short version: a locked-down Student
desktop app, a Teacher web app, an Admin web app, and a Parent portal, sharing one
Backend API and one Database. RBAC via self-hosted OpenFGA.

## Tech Stack
- Student Desktop App: Avalonia (.NET/C#), .NET 10 — decided over MAUI, which has no official Linux support
- Teacher Web App: React + TypeScript + Vite, Tailwind + shadcn/ui, Framer Motion, TanStack Query, Recharts
- Admin Web App: same as Teacher Web App
- Parent Portal: same as Teacher Web App
- Backend API: ASP.NET Core, .NET 10 (LTS, supported through Nov 2028)
- Database: PostgreSQL
- Authorization Service: OpenFGA, self-hosted
- AI Services: Copyleaks (plagiarism, AIS-02), Pangram (AI-content detection, AIS-05), self-hosted embedding model (copy-check, AIS-03), self-hosted open-weight LLM (browsing summary + autograding, AIS-01/04), self-hosted anomaly classifier (suspicious behaviour, AIS-07)
- Code Execution Service: Judge0 or Piston, self-hosted, sandboxed (runs untrusted student code — configure real resource limits)

## Commands
[FILL IN once each app's project is scaffolded — build/test/lint/dev commands per app]

## Code Conventions
[FILL IN — e.g. naming conventions, folder layout inside each app, test file location]

## Boundaries
- Never commit secrets, API keys, or .env files
- Never push directly to main — always a feature branch + PR
- Reference the feature ID (e.g. SDA-11) from docs/architecture.md in every commit
  and PR title — it's the shared vocabulary between the two of you and Claude Code
- Regenerate the OpenAPI client (Kiota/NSwag) after every Backend API change —
  the C#/TypeScript boundary won't catch a mismatch at compile time the way an
  all-C# stack would
- Ask before changing the DB schema or the OpenFGA authorization model — both of you
  depend on these staying stable
```

The `[FILL IN]` lines are genuinely open — the architecture doc deliberately left tech stack for Teacher Web App, Admin Web App, Parent Portal, Backend API, and Database as TBD (Section 7, Open Questions). Pick these together in Week 0 before writing any real feature code — don't let each track independently guess.

---

## 4. Using Claude Code — Daily Pattern for Two Novices

1. **Open a terminal in the repo folder**, on your feature branch (Section 2).
2. **Start Claude Code** (`claude` in that folder — it auto-loads `CLAUDE.md`).
3. **Give it the feature, not the whole spec.** Don't paste the entire architecture doc. Copy just the one feature row from `docs/architecture.md` — ID, description, EARS requirement, acceptance criteria — that's already written in testable form, so there's nothing to guess:

   > "Implement SDA-11 from docs/architecture.md: [paste the row]. This goes in apps/student-desktop."

4. **Read the diff before accepting anything.** Claude Code shows you what it's about to change — actually look at it, even if you don't understand every line yet. If something looks like it touches files outside what you asked for, ask why before approving.
5. **Test it actually works**, then commit (Section 2's pattern: implement → test → commit → next).
6. **One feature per session.** Splitting a session across two feature IDs means neither lands in a reviewable, committable state — this is the single easiest mistake to make when you're new to this.

**If Claude Code goes off the rails** (confused, editing the wrong thing, producing something that doesn't match the feature spec): don't try to argue it back on track. `git reset --hard HEAD` if you haven't committed, or just close the session and start a fresh one with a clearer, narrower prompt. Fresh context beats a confused long conversation.

---

## 5. Week 0 — Build Together Before Splitting

Also moved to `campus-platform-work-division.md` (Section 2) — it now includes Docker Compose, self-hosted OpenFGA, and self-hosted Judge0/Piston, none of which existed as decisions when this section was first written here. Once that checklist is done, split into the two tracks it describes.

---

## 6. Equal Work Division

Moved to its own file: `campus-platform-work-division.md`. It covers the track split, the Week 0 foundation checklist (now including Docker, self-hosted OpenFGA, and self-hosted Judge0/Piston), and the contract-change protocol — kept separate so it can be updated as the architecture evolves without this guide needing to change too.

---

## 7. Daily & Weekly Rhythm, and Pacing

Also moved to `campus-platform-work-division.md` (Sections 5-7) — the sync cadence, the contract-change protocol, and the rough pacing estimate all live there now, alongside the track split they're describing. Keeping them together means one doc changes as the plan evolves instead of two.

---

## 8. Preventing, Identifying, and Rolling Back Problems

### Preventing accidental damage

- **Branch protection on `main`** (Section 1) is your biggest safety net — neither of you can push straight to it, so `main` always reflects reviewed, working code. Never edit on `main` directly.
- **Commit often** (Section 2). Never let uncommitted changes pile up — each commit is a point you can always get back to. The longer you go without committing, the more you stand to lose if something goes wrong.
- **Read the diff before accepting anything Claude Code produces** (Section 4). This is the single highest-leverage habit here — almost every "how did this break" moment traces back to a diff nobody actually looked at.
- **Add CI once your stack is picked** (optional, but worth it once you're past Week 0): a GitHub Actions workflow that runs your tests automatically on every PR, so a broken change gets flagged before either of you even reviews it by hand.
- **Never commit secrets.** Add `.env` and credential files to `.gitignore`, and run `git diff --staged` before every commit to eyeball for anything that looks like a password, API key, or token.
- **Coordinate before touching shared code at the same time.** The main way accidental damage happens *between* the two of you isn't a bad line of code — it's both people editing the same shared piece (e.g. the Notification Router) in the same window without saying so. Same rule as any contract change: post it in the log first.

### Identifying problems

- **Read the actual error message**, not just "it's broken" — feed Claude Code the specific error (e.g. `TypeError: Cannot read property 'id' of undefined at UserService.ts:42`), not the whole console log.
- `git log --oneline` — see what changed, in order, and when.
- `git diff HEAD~3` (or `git diff <commit>..<commit>`) — see exactly what changed between two points in history.
- `git bisect start` / `git bisect good <commit>` / `git bisect bad` — when something broke and you don't know which of the last N commits caused it, this walks you through a binary search across history to find the exact commit that introduced the bug. One of git's most useful and most underused tools for exactly this situation.
- `git blame <file>` — see which commit last touched each line, useful when one specific function is misbehaving and you want to know why it looks the way it does.
- **Run tests locally before pushing.** Catches most breakage before it ever reaches a PR, let alone `main`.

### Rolling back — three situations, three different commands

1. **Uncommitted changes you want to throw away** (mid-session, things went sideways):
   ```bash
   git checkout -- <file>        # discard changes to one file
   git reset --hard HEAD         # discard everything since your last commit
   ```

2. **A commit that's already pushed or merged, and you want to undo it without rewriting shared history:**
   ```bash
   git revert <commit-hash>      # creates a NEW commit that undoes the old one
   ```
   This is the right tool once something is on `main` — it doesn't rewrite history the other person may have already pulled. On GitHub, a merged PR has a **Revert** button that does this automatically and opens a new PR for it.

3. **You committed something locally, haven't pushed yet, and want to undo or fix it:**
   ```bash
   git reset --soft HEAD~1       # undo the last commit, keep the changes staged
   git commit --amend            # fix the last commit's message or contents
   ```

**If you think you've lost work entirely:** `git reflog` — git keeps a record of everywhere `HEAD` has pointed, including commits that look "deleted" from a branch. Almost nothing in git is actually gone forever; reflog is usually how you get it back.

**One hard rule:** never run `git push --force` on `main`, and be very careful with it on a shared feature branch — it rewrites history and can silently erase the other person's work if they've already pulled that branch. `git revert` is almost always the safer choice once anything is shared with the other person.
