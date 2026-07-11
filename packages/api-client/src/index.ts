/**
 * @campus/api-client — public entry point.
 *
 * Extracted per issue #87 from apps/teacher-web, apps/admin-web, and
 * apps/parent-portal's src/lib/api.ts, which had drifted into duplicated
 * (in the core HTTP client, byte-identical) copies of the same fetch
 * wrapper, token storage, and several request/response DTOs.
 *
 * What lives here vs what stays app-local:
 *   - `./http`      — genuinely identical across all three apps (TWA, AWA, PRT).
 *   - `./auth`      — staff login, shared by TWA + AWA only. PRT has its own
 *                     ward-based `/parent/login` flow that stays local.
 *   - `./timetable` — shared by TWA + AWA.
 *   - `./events`    — shared by TWA + AWA.
 *   - `./reports`   — shared by TWA (files reports) + AWA (reads them, AWA-07).
 *
 * Everything else each app calls (attendance/marks/roster for TWA; users/
 * roles/permissions/departments/fees for AWA; ward records/fees for PRT) is
 * genuinely app-specific and stays in that app's own src/lib/api.ts, which
 * calls `request()` from here directly instead of re-implementing it.
 */

export { getToken, setToken, ApiError, request } from './http.js';
export type { LoginResponse } from './auth.js';
export { login } from './auth.js';
export type { TimetableSlotDto, ChangeRequestDto } from './timetable.js';
export { getMyTimetable, generateTimetable, patchTimetableSlot, createChangeRequest } from './timetable.js';
export type { EventDto } from './events.js';
export { createEvent } from './events.js';
export type { TeacherReportDto } from './reports.js';
export { createReport, getReports } from './reports.js';
