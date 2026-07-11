/**
 * Teacher reports on a section or student (TWA files them, AWA-07 reads
 * them back as part of a student record). Same DTO and REST resource
 * (`/reports`) from both sides; each app only imports the verb it needs.
 */

import { request } from './http.js';

export interface TeacherReportDto {
  id: string;
  teacherId: string;
  teacherName: string;
  sectionId: string | null;
  sectionName: string | null;
  studentId: string | null;
  studentName: string | null;
  content: string;
  submittedAt: string;
}

export function createReport(report: { sectionId?: string | null; studentId?: string | null; content: string }) {
  return request<TeacherReportDto>('/reports', {
    method: 'POST',
    body: JSON.stringify({
      sectionId: report.sectionId ?? null,
      studentId: report.studentId ?? null,
      content: report.content,
    }),
  });
}

export function getReports() {
  return request<TeacherReportDto[]>('/reports');
}
