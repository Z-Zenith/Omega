/**
 * SEK-01 — Code editor public surface.
 */
export type {
  Language,
  CodeSource,
  CodeRunResult,
  CodeEditorProps,
  CodeEditorApi,
} from './types.js';

export { LANGUAGE_LABELS } from './types.js';
export { CodeEditor } from './CodeEditor.js';
export { isSupportedLanguage, unsupportedLanguageError } from './logic.js';
