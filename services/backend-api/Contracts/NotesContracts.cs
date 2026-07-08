namespace BackendApi.Contracts;

// SDA-08, SEK-03 (Note shape matches the pre-existing SEK-03 Note contract in
// packages/shared-editor-kit/src/notes/types.ts, ahead of a full SEK embedding).
public record CreateNoteRequest(string Title, string ContentMarkdown);

public record UpdateNoteRequest(string Title, string ContentMarkdown);

public record NoteDto(Guid Id, string Title, string ContentMarkdown, DateTime CreatedAt, DateTime UpdatedAt);

// For the "append to an existing note" picker — full content isn't needed there.
public record NoteSummaryDto(Guid Id, string Title, DateTime UpdatedAt);
