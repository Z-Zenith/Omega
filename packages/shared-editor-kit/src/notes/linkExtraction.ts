/**
 * SEK-03 — pure Markdown link-extraction logic.
 *
 * Framework-agnostic on purpose (no React) so it can be unit-tested directly
 * and reused outside NotesEditor if another embedder ever needs it.
 *
 * Supported syntaxes, per the NoteLinkRef doc comment in ./types.ts:
 *   - Obsidian-style wikilink: [[toNoteId]] or [[toNoteId|Custom anchor]]
 *   - Markdown id-link:        [Anchor text](id:toNoteId)
 */

import type { OutgoingLinks } from './types.js';

const LINK_PATTERN =
  /\[\[([^\]|]+)(?:\|([^\]]+))?\]\]|\[([^\]]+)\]\(id:([^)]+)\)/g;

/**
 * Extracts every outgoing link from a note's raw Markdown body, in source
 * order. Malformed or empty link targets are skipped rather than throwing —
 * this feeds NotesEditor's link resolution, which must never crash on bad
 * input (see the "links resolve to not-found, not a crash" criterion).
 */
export function extractOutgoingLinks(markdown: string): OutgoingLinks {
  const links: Array<{ toNoteId: string; anchor: string }> = [];

  for (const match of markdown.matchAll(LINK_PATTERN)) {
    const [, wikiTarget, wikiAlias, mdAnchor, mdTarget] = match;

    const toNoteId = (wikiTarget ?? mdTarget ?? '').trim();
    if (!toNoteId) continue;

    const anchor = (wikiTarget !== undefined ? (wikiAlias ?? wikiTarget) : mdAnchor ?? '').trim();
    if (!anchor) continue;

    links.push({ toNoteId, anchor });
  }

  return links;
}
