/**
 * Core HTTP client — session-token storage, the fetch wrapper, and the
 * common error type. This is the part that was byte-for-byte identical
 * across apps/teacher-web, apps/admin-web, and apps/parent-portal's
 * src/lib/api.ts before issue #87: every app hit the same `/api/v1` prefix,
 * stored the same bearer token under the same localStorage key, and threw
 * the same ApiError shape on a non-2xx response.
 *
 * Each app's local src/lib/api.ts re-exports these and layers its own
 * app-specific DTOs/endpoints on top by calling `request()` directly —
 * see e.g. apps/teacher-web/src/lib/api.ts's attendance/marks section.
 */

const TOKEN_KEY = 'campus.token';

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

// #158 — every backend controller returns {"error": "...", "message": "human text"} on
// failure; surface that human message instead of the raw JSON blob, falling back to the
// raw text/status if the body isn't the shape we expect (or isn't JSON at all).
async function readErrorMessage(res: Response): Promise<string> {
  const body = await res.text().catch(() => '');
  if (!body) return res.statusText;
  try {
    const parsed = JSON.parse(body);
    if (typeof parsed?.message === 'string' && parsed.message) return parsed.message;
  } catch {
    // not JSON - fall through to the raw text below
  }
  return body;
}

export async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const res = await fetch(`/api/v1${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  });
  if (!res.ok) {
    throw new ApiError(res.status, await readErrorMessage(res));
  }
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}
