/**
 * SEK-01 — pure code-editor logic.
 *
 * Framework-agnostic on purpose (no React) so it can be unit-tested directly,
 * mirroring notes/linkExtraction.ts.
 */

import type { CodeSource, Language } from './types.js';
import type { SekError } from '../types/common.js';

// Deliberately not imported from types.ts's LANGUAGE_LABELS: this module is
// loaded directly by `node --test` against raw .ts sources (no bundler), and
// Node's native type-stripping only erases `import type` — a runtime value
// import across sibling .ts files fails to resolve outside a bundler. Kept
// in sync with `Language` (not just LANGUAGE_LABELS) via the exhaustive
// Record below, so an added/removed language literal fails to compile here
// too, not just in the label map.
const LANGUAGE_MEMBERS: Readonly<Record<Language, true>> = {
  c: true,
  cpp: true,
  python: true,
  java: true,
  dotnet: true,
  html: true,
  css: true,
  javascript: true,
  typescript: true,
  nodejs: true,
  sql: true,
  json: true,
  yaml: true,
};

const SUPPORTED_LANGUAGES = new Set<string>(Object.keys(LANGUAGE_MEMBERS));

/**
 * Runtime guard for the closed `Language` union. `Language` is a compile-time
 * string-literal union, but data can still arrive at runtime with a stale or
 * foreign value (e.g. content persisted before a language was retired). This
 * is the runtime half of the "a language outside the launch list shows a
 * clear 'unsupported language' error, not a silent failure" acceptance
 * criterion — the compile-time half is the closed union itself.
 */
export function isSupportedLanguage(language: string): language is Language {
  return SUPPORTED_LANGUAGES.has(language);
}

/**
 * Canonical error for a language outside the launch list. Centralized here
 * (rather than built ad hoc at each call site) so the message/code pairing
 * can't drift between the component's Run path and its loadSource path.
 */
export function unsupportedLanguageError(language: string): SekError {
  return {
    code: 'unsupported_language',
    message: `"${language}" is not a supported language.`,
  };
}

/**
 * Builds a `CodeSource` without ever assigning `undefined` to an optional
 * field — this package compiles with `exactOptionalPropertyTypes`, under
 * which `{ stdin: undefined }` is a type error for an `stdin?: string` field.
 * Omitting the key entirely (rather than setting it to `undefined`) is the
 * only valid way to represent "no stdin provided".
 */
export function buildCodeSource(
  language: Language,
  content: string,
  stdin: string,
  filename?: string
): CodeSource {
  return {
    language,
    content,
    ...(stdin ? { stdin } : {}),
    ...(filename ? { filename } : {}),
  };
}
