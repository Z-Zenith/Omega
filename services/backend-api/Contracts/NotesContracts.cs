namespace BackendApi.Contracts;

// SDA-08, SEK-03 (Note shape matches the pre-existing SEK-03 Note contract in
// packages/shared-editor-kit/src/notes/types.ts, ahead of a full SEK embedding).

// SDA-19: outgoing links as parsed client-side by SEK's own extractOutgoingLinks
// (packages/shared-editor-kit/src/notes/linkExtraction.ts) — the backend trusts the
// caller's parse rather than re-implementing wikilink syntax in C#, and just syncs
// the note_links rows for this note to match.
public record NoteLinkInput(Guid ToNoteId, string Anchor);

// SDA-19: SEK-03's NotesEditor always generates a note's ID client-side before calling
// onSave (see notes-host-entry.tsx) and expects a single upsert callback — so Create must
// accept an optional caller-supplied Id for the "first save of a SEK-authored note" case.
// Omitted (null) for the SDA-08 clip flow, which never supplies one; the server generates
// an Id as before.
public record CreateNoteRequest(string Title, string ContentMarkdown, Guid? Id = null, IReadOnlyList<NoteLinkInput>? Links = null);

public record UpdateNoteRequest(string Title, string ContentMarkdown, IReadOnlyList<NoteLinkInput>? Links = null);

public record NoteDto(Guid Id, string Title, string ContentMarkdown, DateTime CreatedAt, DateTime UpdatedAt);

// For the "append to an existing note" picker — full content isn't needed there.
public record NoteSummaryDto(Guid Id, string Title, DateTime UpdatedAt);
