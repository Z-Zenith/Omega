/**
 * Staff login (Teacher Web / Admin Web) — POST /auth/login.
 *
 * Not shared with Parent Portal: PRT authenticates a parent against a
 * ward's roll number + date of birth via a separate endpoint
 * (`/parent/login`, `ParentLoginResponse`) that returns ward identity
 * fields instead of a staff accountType — see
 * apps/parent-portal/src/lib/api.ts, which stays app-local.
 */

import { request } from './http.js';

export interface LoginResponse {
  token: string;
  userId: string;
  sessionId: string;
  accountType: string;
  fullName: string;
}

export function login(identifier: string, password: string, totpCode: string) {
  return request<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ identifier, password, totpCode, deviceInfo: navigator.userAgent }),
  });
}
