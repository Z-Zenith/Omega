/**
 * SEK-01 — Code editor public interface.
 *
 * Spec: docs/Campus platform architecture.md, Section 3 (SEK-01).
 *   "VS Code-style editor running code via the Code Execution Service; supports
 *    C, C++, Python, Java, .NET (C#), HTML, CSS, JavaScript/TypeScript, Node.js
 *    and its major runtime variants, SQL, JSON, and YAML at launch."
 *
 * Acceptance criteria this interface MUST enforce:
 *   - "Output/error appears in the editor pane" — CodeEditorProps.run returns a
 *     CodeRunResult that the host pane renders.
 *   - "A language outside the launch list shows a clear 'unsupported language'
 *      error, not a silent failure" — Language is a closed string-literal union
 *      so the type system catches unsupported languages at the call site, and
 *      the runtime surface returns Result<CodeRunResult, SekErrorCode> with
 *      'unsupported_language' for any unrecognised value (e.g. from stale
 *      persisted content).
 */

import type { Result, SekError, UserContext } from '../types/common.js';

/** Closed set of languages supported at launch. Adding one is a contract change. */
export type Language =
  | 'c'
  | 'cpp'
  | 'python'
  | 'java'
  | 'dotnet'        // .NET (C#)
  | 'html'
  | 'css'
  | 'javascript'
  | 'typescript'
  | 'nodejs'        // Node.js and its major runtime variants (per SEK-01 spec)
  | 'sql'
  | 'json'
  | 'yaml';

/** Human-readable label for each language — used in the language picker UI. */
export const LANGUAGE_LABELS: Readonly<Record<Language, string>> = {
  c: 'C',
  cpp: 'C++',
  python: 'Python',
  java: 'Java',
  dotnet: '.NET (C#)',
  html: 'HTML',
  css: 'CSS',
  javascript: 'JavaScript',
  typescript: 'TypeScript',
  nodejs: 'Node.js',
  sql: 'SQL',
  json: 'JSON',
  yaml: 'YAML',
};

/** Source code that the host pane hands to SEK. */
export interface CodeSource {
  readonly language: Language;
  readonly content: string;
  /** Optional stdin the Code Execution Service should feed to the program. */
  readonly stdin?: string;
  /** Optional filename hint, useful for multi-file languages. */
  readonly filename?: string;
}

/** Output of a single code run. Mirrors the Code Execution Service response. */
export interface CodeRunResult {
  /** Standard output captured by the runner. */
  readonly stdout: string;
  /** Standard error captured by the runner. */
  readonly stderr: string;
  /** Process exit code; non-zero is a runtime error, not a SEK error. */
  readonly exitCode: number;
  /** Wall-clock duration in milliseconds. */
  readonly durationMs: number;
  /** Whether the runner applied a timeout — surfaced so the UI can explain. */
  readonly timedOut: boolean;
}

/**
 * Props the embedder (TWA, SDA) passes to the SEK code-editor component.
 *
 * Callbacks are supplied by the embedder — SEK is purely presentational +
 * routing. It does not own persistence or auth; the embedder does.
 */
export interface CodeEditorProps {
  readonly user: UserContext;
  /** Initial source. If absent, the editor starts blank in `defaultLanguage`. */
  readonly initialSource?: CodeSource;
  /** Language of a new blank document. Defaults to 'python'. */
  readonly defaultLanguage?: Language;
  /** Whether the user is allowed to click "Run". Disable for read-only review. */
  readonly canRun: boolean;
  /** Whether the user is allowed to edit source. Disable for read-only review. */
  readonly canEdit: boolean;
  /**
   * Called when the user clicks Run. The embedder is responsible for forwarding
   * the request to the Backend API, which talks to the self-hosted Code
   * Execution Service (Judge0 or Piston — see architecture doc Section 7).
   * SEK does NOT call the runner directly.
   */
  readonly onRun: (source: CodeSource) => Promise<Result<CodeRunResult, SekError>>;
  /** Called whenever the user edits source. Used for autosave hooks. */
  readonly onSourceChange?: (source: CodeSource) => void;
  /** Optional theme override. Defaults to the embedder's design system. */
  readonly theme?: 'light' | 'dark' | 'system';
}

/**
 * Runtime surface the embedder can call imperatively (e.g. from a toolbar
 * button outside the editor pane). Component-level concerns stay in the
 * React/Avalonia component; this is the public, framework-agnostic API.
 */
export interface CodeEditorApi {
  /** Re-render the editor with a new source (e.g. when a file is opened). */
  loadSource(source: CodeSource): void;
  /** Read the current source out of the editor. */
  getSource(): CodeSource;
  /** Programmatically trigger a run. Returns the same shape as onRun. */
  run(): Promise<Result<CodeRunResult, SekError>>;
}
