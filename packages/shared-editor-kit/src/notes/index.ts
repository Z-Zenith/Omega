/**
 * SEK-03 — Markdown notes public surface.
 */
export type {
  Note,
  NoteLink,
  NoteLinkRef,
  OutgoingLinks,
  Backlinks,
  NotesEditorProps,
  NotesEditorApi,
} from './types.js';

export { NotesEditor } from './NotesEditor.js';
export { extractOutgoingLinks } from './linkExtraction.js';
