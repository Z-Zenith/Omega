using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Services;

// SDA-12: server-side "is this student in a scheduled class right now" check. Kept separate
// from the controller (and out of any client-side logic) so it is the single, authoritative
// source of truth — the desktop app doesn't need its own copy of timetable/session rules to
// decide when to ping, it just always calls the exit-ping endpoint and this decides whether
// there's actually an active session to notify about.
public static class ClassSessionLookup
{
    public record ActiveSession(Guid ClassSessionId, Guid TeacherId, Guid SectionId, string SectionName, Guid SubjectId);

    public static async Task<ActiveSession?> FindOrStartActiveSessionAsync(AppDbContext db, Guid studentId, DateTime nowUtc)
    {
        // TimetableController's Grid uses the same day-of-week convention as
        // DateTime.DayOfWeek (Sunday=0 .. Saturday=6), so no remapping is needed here.
        var dayOfWeek = (int)nowUtc.DayOfWeek;
        var timeNow = TimeOnly.FromDateTime(nowUtc);
        var today = DateOnly.FromDateTime(nowUtc);

        var slot = await db.TimetableSlots
            .Include(s => s.Section)
            .Where(s => s.DayOfWeek == dayOfWeek && s.StartTime <= timeNow && timeNow < s.EndTime)
            .Where(s => db.SectionEnrollments.Any(e => e.SectionId == s.SectionId && e.StudentId == studentId))
            .FirstOrDefaultAsync();

        if (slot is null)
        {
            return null;
        }

        var session = await db.ClassSessions
            .FirstOrDefaultAsync(cs => cs.TimetableSlotId == slot.Id && cs.SessionDate == today);
        if (session is null)
        {
            session = new ClassSession
            {
                Id = Guid.NewGuid(),
                TimetableSlotId = slot.Id,
                SessionDate = today,
            };
            db.ClassSessions.Add(session);
            await db.SaveChangesAsync();
        }

        // Prefer the teacher who actually ran the session (e.g. a substitute) once recorded;
        // fall back to the timetable's assigned teacher before that's known.
        var teacherId = session.ActualTeacherId ?? slot.TeacherId;

        return new ActiveSession(session.Id, teacherId, slot.SectionId, slot.Section.Name, slot.SubjectId);
    }
}
