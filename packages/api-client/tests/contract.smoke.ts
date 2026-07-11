/**
 * Contract smoke test for the api-client public interface.
 *
 * Compile-time only (see packages/shared-editor-kit/tests/contract.smoke.ts
 * for the pattern this mirrors) — forces every public export to resolve
 * through the barrel and through each subpath, so a broken re-export fails
 * `npm test` instead of silently shipping.
 */

import type { LoginResponse, TimetableSlotDto, ChangeRequestDto, EventDto, TeacherReportDto } from '../src/index.js';
import { getToken, setToken, ApiError, request, login, getMyTimetable, generateTimetable, patchTimetableSlot, createChangeRequest, createEvent, createReport, getReports } from '../src/index.js';

// ---- subpath imports also resolve (tree-shaking contract) ----
import { getToken as getTokenSub, ApiError as ApiErrorSub, request as requestSub } from '../src/http.js';
import type { LoginResponse as LoginResponseSub } from '../src/auth.js';
import { login as loginSub } from '../src/auth.js';
import type { TimetableSlotDto as TimetableSlotDtoSub } from '../src/timetable.js';
import type { EventDto as EventDtoSub } from '../src/events.js';
import type { TeacherReportDto as TeacherReportDtoSub } from '../src/reports.js';

// ---- every barrel export and subpath export referenced in one type
// position is enough to force resolution ----
declare const _barrelExports: {
  getToken: typeof getToken;
  setToken: typeof setToken;
  ApiError: typeof ApiError;
  request: typeof request;
  login: typeof login;
  loginResponse: LoginResponse;
  getMyTimetable: typeof getMyTimetable;
  generateTimetable: typeof generateTimetable;
  patchTimetableSlot: typeof patchTimetableSlot;
  createChangeRequest: typeof createChangeRequest;
  timetableSlot: TimetableSlotDto;
  changeRequest: ChangeRequestDto;
  createEvent: typeof createEvent;
  event: EventDto;
  createReport: typeof createReport;
  getReports: typeof getReports;
  report: TeacherReportDto;
};
void _barrelExports;

declare const _subpathExports: {
  getToken: typeof getTokenSub;
  ApiError: typeof ApiErrorSub;
  request: typeof requestSub;
  login: typeof loginSub;
  loginResponse: LoginResponseSub;
  timetableSlot: TimetableSlotDtoSub;
  event: EventDtoSub;
  report: TeacherReportDtoSub;
};
void _subpathExports;

// ---- ApiError carries a status code (the contract every app's
// error-handling `err instanceof ApiError ? ... : ...` branch relies on) ----
const _err = new ApiError(403, 'forbidden');
if (_err.status !== 403) throw new Error('ApiError.status did not round-trip');
if (!(_err instanceof Error)) throw new Error('ApiError must extend Error');

console.log('api-client contract smoke test passed');
