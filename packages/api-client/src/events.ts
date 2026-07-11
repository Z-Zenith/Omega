/**
 * Calendar/events (TWA-15 / AWA-11) — creating an institution-/section-wide
 * event. Identical DTO and endpoint in Teacher Web and Admin Web; only the
 * page copy differs per role, which stays local to each app's EventsPage.
 */

import { request } from './http.js';

export interface EventDto {
  id: string;
  title: string;
  startTime: string;
  endTime: string;
  isRegistered: boolean;
}

export function createEvent(event: {
  title: string;
  startTime: string;
  endTime: string;
  restrictedYears: number[] | null;
  restrictedDepartments: string[] | null;
}) {
  return request<EventDto>('/events', {
    method: 'POST',
    body: JSON.stringify(event),
  });
}
