# Campus Platform — Database Schema & API Map

Companion to `campus-platform-architecture.md` — every table and endpoint below traces back to a feature ID from that doc. This is a first-pass schema meant to give both of you a concrete starting point for Week 0, not a final, fully-normalized design — expect to adjust it once you're actually building against it. Treat it as **the frozen contract** referenced in the starting guide's Week 0 checklist: once you both commit to this shape, changes go through the contract-change protocol (post in the log, wait for a thumbs-up, then regenerate the OpenAPI client).

Database: PostgreSQL. Backend: ASP.NET Core + EF Core (so these tables map directly to EF entity classes).

---

## Part 1 — Database Schema

### 1.1 Tenancy & Identity

**`colleges`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| name | text | |
| created_at | timestamptz | |

**`departments`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| college_id | uuid FK → colleges | |
| name | text | |
| hod_role_binding_id | uuid FK → role_bindings, nullable | AWA-14 |

**`users`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| college_id | uuid FK → colleges | |
| account_type | enum(student, teacher, admin_tier, parent) | |
| identifier | text, unique per college | roll number (student) or username (teacher/admin) — SDA-02, TWA-03 |
| password_hash | text | |
| totp_secret | text, encrypted | SDA-02, SDA-23 |
| full_name | text | |
| department_id | uuid FK → departments, nullable | |
| date_of_birth | date, nullable | students only — PRT-01 parent-login credential |
| is_active | boolean | |
| created_at | timestamptz | AWA-09 |

**`parent_wards`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| parent_user_id | uuid FK → users | account_type = parent |
| student_id | uuid FK → users | account_type = student; unique per (parent_user_id, student_id) |
| created_at | timestamptz | PRT-01 — authorization gate: a parent may only log in as / view a student registered here, even if they know the roll number + DOB |

**`permissions`** — the catalog itself, now that the architecture doc's Section 9 enumerates it in full
| Column | Type | Notes |
|---|---|---|
| code | text PK | e.g. `create_timetable`, `add_external_marks` — full list in architecture doc Section 9 |
| description | text | |

**`roles`**
| Column | Type | Notes |
|---|---|---|
| code | text PK | `lecturer`, `hod`, `finance`, `it`, `admin` |
| default_scope_kind | enum(global, department) | |

**`role_default_permissions`** — junction table for a role's default permission bundle
| Column | Type | Notes |
|---|---|---|
| role_code | text FK → roles | |
| permission_code | text FK → permissions | |

**`role_bindings`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| user_id | uuid FK → users | |
| role_code | text FK → roles | |
| scope_type | enum(global, department) | |
| department_id | uuid FK → departments, nullable | required if scope_type = department |
| granted_at | timestamptz | AWA-13 |

**`permission_grants`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| user_id | uuid FK → users | |
| permission_code | text FK → permissions | e.g. `add_external_marks`, `create_timetable`, `view_browsing_history` |
| granted | boolean | true = additive grant, false = explicit revoke |
| expires_at | timestamptz, nullable | TWA-17's time-bound mechanism — general to any grant, not special-cased |
| granted_by | uuid FK → users | AWA-13 |
| created_at | timestamptz | |

*Note: these tables mirror what's mechanically enforced by OpenFGA (Section 9 of the architecture doc) — the tables are your source of truth for writes, OpenFGA answers the "can this user do X" query at read time. Keep them in sync via the same write path, never write to one without the other. Seed `permissions` and `role_default_permissions` from the architecture doc's Section 9 catalog table directly — don't let the two documents drift.*

**`user_sessions`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| user_id | uuid FK → users | |
| device_info | text | |
| is_active | boolean | API-01 — one active row per user; new login flips the old row to false |
| created_at | timestamptz | |

### 1.2 Academic Structure

**`subjects`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| department_id | uuid FK → departments | |
| code | text | |
| name | text | |
| teacher_id | uuid FK → users, nullable | SDA-18 |

**`sections`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| department_id | uuid FK → departments | |
| year | int | used for event/whitelist scoping — SDA-20, TWA-15 |
| name | text | e.g. "3rd Year CSE - A" |

**`section_enrollments`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| section_id | uuid FK → sections | |
| student_id | uuid FK → users | API-02 auto-enrolls at semester start |

**`teacher_section_assignments`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| teacher_id | uuid FK → users | |
| section_id | uuid FK → sections | |
| subject_id | uuid FK → subjects | TWA-01, TWA-02 |

**`timetable_slots`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| section_id | uuid FK → sections | |
| subject_id | uuid FK → subjects | |
| teacher_id | uuid FK → users | AWA-01, AWA-02, AWA-03 |
| day_of_week | int | |
| start_time | time | |
| end_time | time | |
| room | text, nullable | |
| manually_edited | boolean | AWA-03 — protects manual edits from being overwritten by the next auto-generation run |

**`class_sessions`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| timetable_slot_id | uuid FK → timetable_slots | |
| session_date | date | |
| actual_teacher_id | uuid FK → users, nullable | substitution support |

### 1.3 Attendance

**`attendance_records`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| class_session_id | uuid FK → class_sessions | |
| student_id | uuid FK → users | |
| status | enum(present, absent, late) | TWA-08 |
| marked_at | timestamptz | |
| marked_by | uuid FK → users | |

### 1.4 Assignments & Submissions

**`assignments`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| subject_id | uuid FK → subjects | |
| teacher_id | uuid FK → users | |
| type | enum(code, quiz, essay, file_upload) | TWA-07 |
| title | text | |
| description | text | |
| due_date | timestamptz | |
| submission_window_start | timestamptz | |
| submission_window_end | timestamptz | SDA-11 checks against this |
| type_specific_settings | jsonb | quiz question bank / code starter files, varies by type |

**`submissions`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| assignment_id | uuid FK → assignments | |
| student_id | uuid FK → users | |
| content_url | text | file, code, or essay text |
| submitted_at | timestamptz | |
| is_late | boolean | SDA-10 |
| is_autosubmitted | boolean | SDA-11 |

**`plagiarism_reports`** (AIS-02, Copyleaks) — `id`, `submission_id` FK, `similarity_score`, `copyleaks_scan_id` text, `matched_sources` jsonb, `checked_at`
**`copy_check_flags`** (AIS-03) — `id`, `submission_a_id` FK, `submission_b_id` FK, `similarity_score` (flagged at ≥ 90%), `flagged_at`
**`ai_detection_reports`** (AIS-05, Pangram) — `id`, `submission_id` FK, `ai_likelihood_score`, `pangram_report_id` text, `checked_at`
**`autograde_suggestions`** (AIS-04) — `id`, `submission_id` FK, `suggested_grade`, `confirmed_by_teacher` boolean, `confirmed_at` nullable

### 1.5 Marks

**`internal_marks`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| student_id | uuid FK → users | |
| subject_id | uuid FK → subjects | |
| assignment_id | uuid FK → assignments, nullable | |
| marks | numeric | TWA-16 |
| published | boolean | direct-publish, no approval gate |
| published_at | timestamptz, nullable | |
| published_by | uuid FK → users | |

**`external_marks`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| student_id | uuid FK → users | |
| subject_id | uuid FK → subjects | |
| grade | text | grade-based, not numeric — TWA-17 |
| submitted_by | uuid FK → users | must hold active `add_external_marks` grant at submit time |
| submitted_at | timestamptz | |
| approved | boolean | TWA-20 |
| approved_by | uuid FK → users, nullable | must hold `approve_external_marks` |
| approved_at | timestamptz, nullable | |
| published | boolean | only true once approved |

### 1.6 Community

**`groups`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| college_id | uuid FK → colleges | |
| type | enum(class, subject_section, club, teacher_only) | TWA-05 |
| name | text | |
| created_by | uuid FK → users, nullable | null if auto-created |
| section_id | uuid FK → sections, nullable | for class-type groups |

**`group_members`** — `id`, `group_id` FK, `user_id` FK, `joined_at`
**`group_posts`** — `id`, `group_id` FK, `author_id` FK, `content`, `created_at` (SDA-16)

**`materials`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| subject_id | uuid FK → subjects, nullable | |
| group_id | uuid FK → groups, nullable | TWA-06 — can be attached to either or both |
| uploaded_by | uuid FK → users | |
| file_url | text | GCS path, India region |
| title | text | |
| uploaded_at | timestamptz | |

### 1.7 Calendar & Events

**`events`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| college_id | uuid FK → colleges | |
| title | text | |
| start_time | timestamptz | |
| end_time | timestamptz | |
| created_by | uuid FK → users | teacher (TWA-15) or admin (AWA-11) |
| restricted_years | int[], nullable | SDA-20 |
| restricted_departments | uuid[], nullable | |

**`event_registrations`** — `id`, `event_id` FK, `student_id` FK, `registered_at`
**`todos`** — `id`, `student_id` FK, `title`, `due_date`, `completed` (SDA-14)
**`custom_calendar_entries`** — `id`, `student_id` FK, `title`, `entry_date` (SDA-14)

### 1.8 Browser & Whitelist

**`whitelist_sites`** — `id`, `college_id` FK, `url`, `approved_at` (SDA-03; institution-wide once approved per SDA-04)
**`whitelist_requests`** — `id`, `url`, `requested_by` FK → users, `status` enum(pending, approved, rejected), `reviewed_by` FK nullable (SDA-04)
**`browsing_history_summaries`** — `id`, `student_id` FK, `summary_text`, `generated_at` (AIS-01 — visibility gated by `view_browsing_history` permission at the API layer, not by a column here)

### 1.9 Shared Editor Kit (metadata only — file bytes live in GCS)

**`notes`** — `id`, `owner_id` FK → users, `title`, `content_markdown`, `created_at`, `updated_at` (SEK-03)
**`note_links`** — `id`, `from_note_id` FK, `to_note_id` FK, `anchor` text (not null), `created_at` timestamptz (not null, default now()) (SEK-03)
**`documents`** — `id`, `owner_id` FK, `file_url`, `doc_type` enum(pdf, pptx, docx), `annotations` jsonb, `page_count` int (nullable), `ocr_status` enum/text(pending, processing, completed, failed, not_applicable) (not null, default pending) (SEK-02)

### 1.10 Direct Messaging

**`message_threads`** — `id`, `student_id` FK, `teacher_id` FK, `created_at` (DMS-01)
**`messages`** — `id`, `thread_id` FK, `sender_id` FK, `content`, `sent_at`, `read_at` nullable

### 1.11 Notifications

**`notifications`**
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| recipient_id | uuid FK → users | |
| type | enum(exit_ping, absence_ping, report, timetable_request, fee_reminder, whitelist_request, suspicious_flag) | Notification Router, Section 8d |
| payload | jsonb | |
| created_at | timestamptz | |
| read_at | timestamptz, nullable | |

### 1.12 Reports & Feedback

**`teacher_reports`** — `id`, `teacher_id` FK, `section_id` FK nullable, `student_id` FK nullable, `content`, `submitted_at` (TWA-11)
**`section_feedback`** — `id`, `teacher_id` FK, `section_id` FK, `rating`, `comments`, `submitted_at` (TWA-12; negative feedback feeds AWA-02's exclusion rule)
**`teacher_feedback`** — `id`, `student_id` FK, `teacher_id` FK, `rating`, `comments`, `submitted_at` (SDA-17)
**`timetable_change_requests`** — `id`, `teacher_id` FK, `description`, `status`, `requested_at`, `reviewed_by` FK nullable (TWA-13)

### 1.13 Fees

**`fee_records`** — `id`, `student_id` FK, `amount`, `due_date`, `status` enum(pending, paid), `payment_link`, `paid_at` nullable (AWA-04, AWA-05)
**`payment_transactions`** — `id`, `fee_record_id` FK, `gateway_txn_id`, `status`, `processed_at` (PRT-03)

### 1.14 Suspicious Behaviour

**`usage_telemetry`** — `id`, `student_id` FK, `class_session_id` FK nullable, `assignment_id` FK nullable, `event_type`, `metadata` jsonb, `recorded_at` (SDA-25 — write-only from the client, scoped to active windows only)
**`suspicious_flags`** — `id`, `student_id` FK, `class_session_id` FK nullable, `assignment_id` FK nullable, `confidence_score`, `flagged_at` (AIS-07)

---

## Part 2 — API Map

All routes prefixed `/api/v1`. Every write endpoint checks the caller's effective permission via the Authorization Service (Section 9) before touching the database — that check isn't repeated per-row below, assume it's there.

### Auth & Session
| Method | Path | Feature | Notes |
|---|---|---|---|
| POST | `/auth/login` | SDA-02, TWA-03 | roll number/username + password + TOTP |
| POST | `/auth/logout` | — | |
| POST | `/auth/change-password` | SDA-23 | requires fresh TOTP challenge |
| GET | `/auth/session` | API-01 | current session info |

### Users, Roles & Permissions
| Method | Path | Feature |
|---|---|---|
| POST | `/users` | AWA-09 |
| POST | `/users/{id}/reset-password` | AWA-10 |
| GET | `/users/{id}/profile` | AWA-07, AWA-08 |
| POST | `/role-bindings` | AWA-13 |
| POST | `/permission-grants` | AWA-13 |
| DELETE | `/permission-grants/{id}` | AWA-13 |
| POST | `/departments` | AWA-14 |

### Timetable & Attendance
| Method | Path | Feature |
|---|---|---|
| POST | `/timetable/generate` | AWA-01, AWA-02 |
| PATCH | `/timetable/slots/{id}` | AWA-03 |
| GET | `/timetable/mine` | TWA-10 |
| POST | `/timetable/change-requests` | TWA-13 |
| POST | `/attendance` | TWA-08 |
| GET | `/attendance/alerts` | TWA-09 |

### Assignments & Submissions
| Method | Path | Feature |
|---|---|---|
| POST | `/assignments` | TWA-07 |
| POST | `/assignments/{id}/submissions` | SDA-10, SDA-11 |
| GET | `/submissions/{id}/plagiarism-report` | AIS-02 |
| GET | `/assignments/{id}/copy-check` | AIS-03 |
| GET | `/submissions/{id}/ai-detection` | AIS-05 |
| GET | `/submissions/{id}/autograde-suggestion` | AIS-04 |
| POST | `/submissions/{id}/grade` | teacher confirms/edits suggested grade |

### Marks
| Method | Path | Feature |
|---|---|---|
| POST | `/marks/internal` | TWA-16 |
| POST | `/marks/external` | TWA-17 |
| POST | `/marks/external/{id}/approve` | TWA-20 |
| GET | `/marks/mine` | SDA-15 |
| GET | `/marks/ward/{studentId}` | PRT-02 |

### Notes
| Method | Path | Feature |
|---|---|---|
| GET | `/notes/mine` | SDA-08 |
| GET | `/notes/{id}` | SDA-08 |
| POST | `/notes` | SDA-08 |
| PATCH | `/notes/{id}` | SDA-08 |
| DELETE | `/notes/{id}` | SDA-19 |
| GET | `/notes/{id}/backlinks` | SDA-19 |

### Community
| Method | Path | Feature |
|---|---|---|
| POST | `/groups` | TWA-05, AWA-12 |
| GET | `/groups/mine` | SDA-16 |
| POST | `/groups/{id}/posts` | SDA-16 |
| POST | `/materials` | TWA-06 |
| GET | `/materials/{id}/download` | API-03 |

### Calendar & Events
| Method | Path | Feature |
|---|---|---|
| POST | `/events` | TWA-15, AWA-11 |
| GET | `/events` | SDA-20 (filtered by year/department eligibility) |
| POST | `/events/{id}/register` | SDA-20 |
| GET | `/calendar/mine` | SDA-14 |

### Browser & Behaviour
| Method | Path | Feature |
|---|---|---|
| GET | `/whitelist` | SDA-03 |
| POST | `/whitelist/requests` | SDA-04 |
| POST | `/whitelist/requests/{id}/approve` | SDA-04 |
| GET | `/students/{id}/browsing-summary` | AIS-01 (permission-gated) |
| POST | `/telemetry` | SDA-25 (write-only, scoped windows) |
| GET | `/suspicious-flags` | AIS-07 |

### Messaging & Notifications
| Method | Path | Feature |
|---|---|---|
| POST | `/messages/threads/{id}/messages` | DMS-01 |
| GET | `/messages/threads` | DMS-01 |
| GET | `/notifications` | Notification Router |

### Fees
| Method | Path | Feature |
|---|---|---|
| POST | `/fees/links` | AWA-04 |
| POST | `/fees/{id}/pay` | PRT-03 |
| GET | `/fees/ward/{studentId}` | PRT-02 |

### Parent Portal
| Method | Path | Feature |
|---|---|---|
| POST | `/parent/login` | PRT-01 — roll number + DOB only |

---

## Open Items (mirror Section 16 of the architecture doc)

- Data-subject-rights workflow (access/correct/erase) has no table yet — add one once that feature is scoped.
- **Documents Title field**: `DocumentDescriptor.title` in the Shared Editor Kit is now optional (not backed by a `documents` column) until this is resolved. Still open: should title be (a) added as a stored column, or (b) derived from `file_url`/filename by the embedder? Whichever wins, `title` in the SEK interface should be revisited (made required again, or removed in favor of an embedder-side helper).

~~Full permission-code catalog~~ — resolved; `permissions` and `role_default_permissions` tables added, seeded from architecture doc Section 9.
~~AI Services provider~~ — resolved; Copyleaks (AIS-02) and Pangram (AIS-05) as external services for the two stakes-sensitive detectors, self-hosted models for AIS-01/03/04/07.
~~Contract-change sign-off for `note_links`/`documents` columns~~ — resolved; Track 2 approved the `anchor`/`created_at` (`note_links`) and `page_count`/`ocr_status` (`documents`) additions per the contract-change rule.

---

## Changelog

| Date | Section(s) touched | Change | ID(s) affected |
|---|---|---|---|
| 2026-07-05 | 1.9, Open Items | Fixed two schema mismatches against TS interfaces: added `anchor` and `created_at` to `note_links` (SEK-03); added `page_count` and `ocr_status` to `documents` (SEK-02); added open question regarding document title derivation. | SEK-02, SEK-03 |
| 2026-07-05 | Open Items | Code review follow-up: flagged this schema change as pending shared-log sign-off per the contract-change rule; noted `DocumentDescriptor.title` was made optional in the SEK interface to match the still-unresolved title-derivation question. | SEK-02 |
| 2026-07-05 | Open Items | Contract-change sign-off received from Track 2 for the `note_links`/`documents` column additions (`anchor`, `created_at`, `page_count`, `ocr_status`) — moved from pending to resolved. | SEK-02, SEK-03 |
| 2026-07-05 | 1.9, db/init/01_schema.sql | Applied the approved `note_links`/`documents` column additions to the actual SQL schema — the sign-off above covered the interface/docs change but the migration itself hadn't landed yet. Added `ocr_status` enum type (`pending`, `processing`, `completed`, `failed`, `not_applicable`). | SEK-02, SEK-03 |
