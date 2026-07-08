/**
 * SEK-03 — Markdown notes editor component.
 *
 * Implements the NotesEditorProps/NotesEditorApi contract from ./types.ts.
 * Unstyled on purpose (semantic HTML + stable class hooks only) — SEK owns
 * no styling opinions, the embedder (TWA, SDA) skins it.
 */

import {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
} from 'react';
import type { SekError } from '../types/common.js';
import type {
  Backlinks,
  Note,
  NotesEditorApi,
  NotesEditorProps,
  OutgoingLinks,
} from './types.js';
import { extractOutgoingLinks } from './linkExtraction.js';
import { ImageSearchPanel } from '../image-search/ImageSearchPanel.js';
import type { ImageInsert } from '../image-search/types.js';

type LinkStatus = 'pending' | 'resolved' | 'not_found';

function newNoteId(): string {
  // crypto.randomUUID is available in every embedder runtime we target
  // (browsers, Node 19+, and Avalonia's WebView2/CEF host).
  return globalThis.crypto.randomUUID();
}

export const NotesEditor = forwardRef<NotesEditorApi, NotesEditorProps>(
  function NotesEditor(
    { user, currentNote, canEdit, onSave, onDelete, onResolveLink, onListBacklinks, imageSearch },
    ref
  ) {
    const [title, setTitle] = useState(currentNote?.title ?? '');
    const [content, setContent] = useState(currentNote?.contentMarkdown ?? '');
    const [backlinks, setBacklinks] = useState<Backlinks>([]);
    const [linkStatuses, setLinkStatuses] = useState<Record<string, LinkStatus>>({});
    const [error, setError] = useState<SekError | null>(null);
    const [showImageSearch, setShowImageSearch] = useState(false);

    // Imperative-handle methods close over stale state without this — keep a
    // ref mirroring the latest draft so getMarkdown()/getOutgoingLinks() are
    // always current even though the handle object identity is stable.
    const contentRef = useRef(content);
    contentRef.current = content;

    // SEK-04: tracks the textarea DOM node so an inserted image lands at the
    // cursor rather than always appending to the end of the note.
    const textareaRef = useRef<HTMLTextAreaElement | null>(null);

    const outgoingLinks: OutgoingLinks = useMemo(
      () => extractOutgoingLinks(content),
      [content]
    );

    const loadNote = async (noteId: string | null) => {
      setError(null);
      if (noteId === null) {
        setTitle('');
        setContent('');
        setBacklinks([]);
        return;
      }
      const [resolved, backlinkResult] = await Promise.all([
        onResolveLink(noteId),
        onListBacklinks(noteId),
      ]);
      if (resolved.ok) {
        setTitle(resolved.value.title);
        setContent(resolved.value.contentMarkdown);
      } else {
        setError(resolved.error);
      }
      setBacklinks(backlinkResult.ok ? backlinkResult.value : []);
    };

    // Reset the draft whenever the embedder swaps which note is being edited.
    useEffect(() => {
      setTitle(currentNote?.title ?? '');
      setContent(currentNote?.contentMarkdown ?? '');
      setBacklinks([]);
      if (currentNote) {
        onListBacklinks(currentNote.id).then((result) => {
          if (result.ok) setBacklinks(result.value);
        });
      }
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [currentNote?.id]);

    // Resolve every outgoing link's live status so broken links render as
    // "not found" instead of crashing — this is the acceptance criterion
    // SEK-03 exists to satisfy. `cancelled` discards results from a
    // resolution pass that's since been superseded by newer edits.
    useEffect(() => {
      let cancelled = false;
      const uniqueTargets = [...new Set(outgoingLinks.map((link) => link.toNoteId))];

      setLinkStatuses((prev) => {
        const next: Record<string, LinkStatus> = {};
        for (const id of uniqueTargets) next[id] = prev[id] ?? 'pending';
        return next;
      });

      Promise.all(
        uniqueTargets.map(async (toNoteId) => {
          const result = await onResolveLink(toNoteId);
          return [toNoteId, result.ok ? 'resolved' : 'not_found'] as const;
        })
      ).then((results) => {
        if (cancelled) return;
        setLinkStatuses((prev) => ({ ...prev, ...Object.fromEntries(results) }));
      });

      return () => {
        cancelled = true;
      };
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [outgoingLinks]);

    useImperativeHandle(
      ref,
      (): NotesEditorApi => ({
        reload: () => loadNote(currentNote?.id ?? null),
        getMarkdown: () => contentRef.current,
        getOutgoingLinks: () => extractOutgoingLinks(contentRef.current),
      }),
      [currentNote?.id]
    );

    const handleSave = async () => {
      if (!canEdit) return;
      const now = new Date().toISOString();
      const note: Note = {
        id: currentNote?.id ?? newNoteId(),
        ownerId: currentNote?.ownerId ?? user.userId,
        title,
        contentMarkdown: content,
        createdAt: currentNote?.createdAt ?? now,
        updatedAt: now,
      };
      const result = await onSave(note);
      if (!result.ok) setError(result.error);
    };

    const handleDelete = async () => {
      if (!canEdit || !currentNote) return;
      const result = await onDelete(currentNote.id);
      if (!result.ok) setError(result.error);
    };

    // SEK-04: writes ImageInsert.embeddedUrl (the content-addressed URL from
    // onUploadImage), never the search result's original sourceUrl — that's
    // what makes the embed "embedded, not linked" per the acceptance criterion.
    const handleInsertImage = (insert: ImageInsert) => {
      const markdown = `![${insert.altText}](${insert.embeddedUrl})`;
      const textarea = textareaRef.current;
      if (!textarea) {
        setContent((prev) => `${prev}\n${markdown}\n`);
        return;
      }
      const { selectionStart, selectionEnd } = textarea;
      setContent((prev) => `${prev.slice(0, selectionStart)}${markdown}${prev.slice(selectionEnd)}`);
      // Wait for React to commit the new value before moving the cursor —
      // setSelectionRange on the current render would operate on stale text.
      requestAnimationFrame(() => {
        const pos = selectionStart + markdown.length;
        textarea.focus();
        textarea.setSelectionRange(pos, pos);
      });
    };

    return (
      <div className="sek-notes-editor">
        {error && (
          <div className="sek-notes-editor__error" role="alert">
            {error.message}
          </div>
        )}
        <input
          className="sek-notes-editor__title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Untitled note"
          disabled={!canEdit}
        />
        <textarea
          ref={textareaRef}
          className="sek-notes-editor__body"
          value={content}
          onChange={(e) => setContent(e.target.value)}
          disabled={!canEdit}
        />
        {canEdit && imageSearch && showImageSearch && (
          <ImageSearchPanel user={user} {...imageSearch} onInsert={handleInsertImage} />
        )}
        {canEdit && (
          <div className="sek-notes-editor__actions">
            <button type="button" onClick={handleSave}>
              Save
            </button>
            {currentNote && (
              <button type="button" onClick={handleDelete}>
                Delete
              </button>
            )}
            {imageSearch && (
              <button type="button" onClick={() => setShowImageSearch((v) => !v)}>
                {showImageSearch ? 'Close image search' : 'Insert image'}
              </button>
            )}
          </div>
        )}
        <ul className="sek-notes-editor__outgoing-links">
          {outgoingLinks.map((link, i) => (
            <li
              key={`${link.toNoteId}-${i}`}
              data-status={linkStatuses[link.toNoteId] ?? 'pending'}
            >
              {link.anchor}
            </li>
          ))}
        </ul>
        <ul className="sek-notes-editor__backlinks">
          {backlinks.map((note) => (
            <li key={note.id}>{note.title}</li>
          ))}
        </ul>
      </div>
    );
  }
);
