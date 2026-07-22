using BackendApi.Contracts;
using BackendApi.Data;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Services;

// Shared by MarksController's Mine() (SDA-15) and Ward() (PRT-02), and by
// UsersController's GetProfile() (AWA-08) — every "view a student's marks" surface
// applies the same "published only" rule from one place, so the three views can't
// silently drift on which marks are visible.
public static class PublishedMarksQueries
{
    public static Task<List<InternalMarkDto>> GetPublishedInternalMarksAsync(AppDbContext db, Guid studentId) =>
        db.InternalMarks
            .Where(m => m.StudentId == studentId && m.Published)
            .Select(m => new InternalMarkDto(m.SubjectId, m.Subject.Name, m.Marks, m.PublishedAt))
            .ToListAsync();

    public static Task<List<ExternalMarkDto>> GetPublishedExternalMarksAsync(AppDbContext db, Guid studentId) =>
        db.ExternalMarks
            .Where(m => m.StudentId == studentId && m.Published)
            .Select(m => new ExternalMarkDto(m.SubjectId, m.Subject.Name, m.Grade, m.ApprovedAt))
            .ToListAsync();
}
