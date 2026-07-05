using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

[ApiController]
[Route("api/v1")]
public class MessagingController(AppDbContext db) : ControllerBase
{
    // DMS-01 (Track 2) — get-or-create the single thread for a student-teacher pair.
    // Not in the original documented API map (only send/list were), but a thread has
    // to exist before a message can be sent into it, and the DB's unique index on
    // (student_id, teacher_id) is exactly DMS-01's "scoped to exactly one pair"
    // acceptance criterion — this endpoint is what upholds it.
    [HttpPost("messages/threads")]
    public async Task<ActionResult<MessageThreadResponse>> CreateThread(CreateThreadRequest request)
    {
        if (request.StudentId == request.TeacherId)
        {
            return BadRequest("StudentId and TeacherId must be different users.");
        }

        var student = await db.Users.FindAsync(request.StudentId);
        if (student is null || student.AccountType != AccountType.Student)
        {
            return BadRequest("StudentId must reference an existing user with account type Student.");
        }

        var teacher = await db.Users.FindAsync(request.TeacherId);
        if (teacher is null || teacher.AccountType != AccountType.Teacher)
        {
            return BadRequest("TeacherId must reference an existing user with account type Teacher.");
        }

        var existing = await db.MessageThreads
            .FirstOrDefaultAsync(t => t.StudentId == request.StudentId && t.TeacherId == request.TeacherId);
        if (existing is not null)
        {
            return Ok(ToResponse(existing));
        }

        var thread = new MessageThread
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            TeacherId = request.TeacherId,
            CreatedAt = DateTime.UtcNow,
        };
        db.MessageThreads.Add(thread);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(ListMessages), new { id = thread.Id }, ToResponse(thread));
    }

    // DMS-01 (Track 2)
    [HttpPost("messages/threads/{id}/messages")]
    public async Task<ActionResult<MessageResponse>> SendMessage(Guid id, SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Content must not be empty.");
        }

        var thread = await db.MessageThreads.FindAsync(id);
        if (thread is null)
        {
            return NotFound();
        }

        if (request.SenderId != thread.StudentId && request.SenderId != thread.TeacherId)
        {
            return BadRequest("SenderId must be a participant of this thread.");
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ThreadId = id,
            SenderId = request.SenderId,
            Content = request.Content,
            SentAt = DateTime.UtcNow,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(ListMessages), new { id }, ToResponse(message));
    }

    // DMS-01 (Track 2) — not in the original documented API map, but required to
    // actually render a thread's conversation (sending was the only documented route).
    [HttpGet("messages/threads/{id}/messages")]
    public async Task<ActionResult<List<MessageResponse>>> ListMessages(Guid id)
    {
        var threadExists = await db.MessageThreads.AnyAsync(t => t.Id == id);
        if (!threadExists)
        {
            return NotFound();
        }

        var messages = await db.Messages
            .Where(m => m.ThreadId == id)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        return Ok(messages.Select(ToResponse).ToList());
    }

    // DMS-01 (Track 2) — the other party's inbox, per the requirement ("deliver it to
    // the other party's inbox in their respective app"). userId identifies which
    // side's inbox to list (student or teacher) since there is no session-derived
    // identity in this API yet.
    [HttpGet("messages/threads")]
    public async Task<ActionResult<List<ThreadSummaryResponse>>> ListThreads([FromQuery] Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest("userId query parameter is required.");
        }

        var threads = await db.MessageThreads
            .Where(t => t.StudentId == userId || t.TeacherId == userId)
            .Include(t => t.Messages)
            .ToListAsync();

        var summaries = threads
            .Select(t =>
            {
                var lastMessage = t.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault();
                return new ThreadSummaryResponse(
                    t.Id,
                    t.StudentId,
                    t.TeacherId,
                    t.CreatedAt,
                    lastMessage is null ? null : ToResponse(lastMessage));
            })
            .OrderByDescending(s => s.LastMessage?.SentAt ?? s.CreatedAt)
            .ToList();

        return Ok(summaries);
    }

    // Notification Router (shared)
    [HttpGet("notifications")]
    public IActionResult ListNotifications() => StatusCode(501, new { feature = "Notification Router", status = "not_implemented" });

    private static MessageThreadResponse ToResponse(MessageThread thread) =>
        new(thread.Id, thread.StudentId, thread.TeacherId, thread.CreatedAt);

    private static MessageResponse ToResponse(Message message) =>
        new(message.Id, message.ThreadId, message.SenderId, message.Content, message.SentAt, message.ReadAt);
}
