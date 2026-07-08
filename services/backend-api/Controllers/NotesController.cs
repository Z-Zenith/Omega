using System.Security.Claims;
using BackendApi.Contracts;
using BackendApi.Data;
using BackendApi.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Controllers;

// SDA-08: backs the browser clipper's "save as a new note or append to an existing one"
// flow. General-purpose note CRUD (not clip-specific) so it doubles as the storage layer
// a future full SEK-03 embedding (SDA-19) would also read/write against — the Note shape
// here matches packages/shared-editor-kit/src/notes/types.ts exactly.
[ApiController]
[Route("api/v1/notes")]
[Authorize]
public class NotesController(AppDbContext db) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<List<NoteSummaryDto>>> Mine()
    {
        var userId = CurrentUserId();

        var notes = await db.Notes
            .Where(n => n.OwnerId == userId)
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NoteSummaryDto(n.Id, n.Title, n.UpdatedAt))
            .ToListAsync();

        return Ok(notes);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NoteDto>> GetById(Guid id)
    {
        var userId = CurrentUserId();

        var note = await db.Notes.FindAsync(id);
        if (note is null)
        {
            return NotFound();
        }
        if (note.OwnerId != userId)
        {
            return Forbid();
        }

        return Ok(ToDto(note));
    }

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(CreateNoteRequest request)
    {
        var userId = CurrentUserId();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { error = "title_required", message = "Note title must not be empty." });
        }

        var now = DateTime.UtcNow;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = request.Title.Trim(),
            ContentMarkdown = request.ContentMarkdown ?? "",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync();

        return Ok(ToDto(note));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<NoteDto>> Update(Guid id, UpdateNoteRequest request)
    {
        var userId = CurrentUserId();

        var note = await db.Notes.FindAsync(id);
        if (note is null)
        {
            return NotFound();
        }
        if (note.OwnerId != userId)
        {
            return Forbid();
        }
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { error = "title_required", message = "Note title must not be empty." });
        }

        note.Title = request.Title.Trim();
        note.ContentMarkdown = request.ContentMarkdown ?? "";
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToDto(note));
    }

    private static NoteDto ToDto(Note n) => new(n.Id, n.Title, n.ContentMarkdown, n.CreatedAt, n.UpdatedAt);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
