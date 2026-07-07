/**
 * SEK-01 — Code editor component.
 *
 * Implements the CodeEditorProps/CodeEditorApi contract from ./types.ts.
 * Unstyled on purpose (semantic HTML + stable class hooks only) — SEK owns
 * no styling opinions, the embedder (TWA, SDA) skins it. SEK does not call
 * the Code Execution Service itself; every run goes through the embedder's
 * `onRun` callback (see CodeEditorProps.onRun doc comment).
 */

import { forwardRef, useImperativeHandle, useRef, useState } from 'react';
import type { Result, SekError } from '../types/common.js';
import type {
  CodeEditorApi,
  CodeEditorProps,
  CodeRunResult,
  CodeSource,
  Language,
} from './types.js';
import { LANGUAGE_LABELS } from './types.js';
import { buildCodeSource, isSupportedLanguage, unsupportedLanguageError } from './logic.js';

// Pure function of a module-level constant — computed once, not per instance/mount.
const LANGUAGE_OPTIONS = Object.keys(LANGUAGE_LABELS) as Language[];

export const CodeEditor = forwardRef<CodeEditorApi, CodeEditorProps>(
  function CodeEditor(
    { initialSource, defaultLanguage, canRun, canEdit, onRun, onSourceChange, theme },
    ref
  ) {
    const fallbackLanguage: Language =
      defaultLanguage && isSupportedLanguage(defaultLanguage) ? defaultLanguage : 'python';

    // If the persisted source's language isn't in the launch list, don't load
    // its content under the fallback language either — content authored in an
    // unsupported language isn't valid source for whatever we'd fall back to,
    // and silently pairing them would mislabel the source on the next
    // getSource()/autosave. Only the error banner survives; the editor starts
    // blank in the fallback language instead.
    const validInitialSource =
      initialSource && isSupportedLanguage(initialSource.language) ? initialSource : undefined;

    const [language, setLanguage] = useState<Language>(
      validInitialSource?.language ?? fallbackLanguage
    );
    const [content, setContent] = useState(validInitialSource?.content ?? '');
    const [stdin, setStdin] = useState(validInitialSource?.stdin ?? '');
    const [filename, setFilename] = useState(validInitialSource?.filename);
    const [result, setResult] = useState<CodeRunResult | null>(null);
    const [running, setRunning] = useState(false);
    const [error, setError] = useState<SekError | null>(
      initialSource && !validInitialSource
        ? unsupportedLanguageError(initialSource.language)
        : null
    );

    // Imperative-handle methods close over stale state without this — keep a
    // ref mirroring the latest draft so getSource()/run() are always current
    // even though the handle object identity is stable (same pattern as
    // NotesEditor's contentRef).
    const sourceRef = useRef<CodeSource>(buildCodeSource(language, content, stdin, filename));
    sourceRef.current = buildCodeSource(language, content, stdin, filename);

    // Shared by runSource and the imperative loadSource() so the "reject an
    // unsupported language" behavior can't drift between the two call sites.
    // Returns the error (and records it in state) when rejected, else null.
    const rejectUnsupportedLanguage = (candidate: string): SekError | null => {
      if (isSupportedLanguage(candidate)) return null;
      const err = unsupportedLanguageError(candidate);
      setError(err);
      return err;
    };

    const runSource = async (
      source: CodeSource
    ): Promise<Result<CodeRunResult, SekError>> => {
      const languageErr = rejectUnsupportedLanguage(source.language);
      if (languageErr) return { ok: false, error: languageErr };

      if (!canRun) {
        const err: SekError = {
          code: 'unauthorized',
          message: 'You are not allowed to run code.',
        };
        setError(err);
        return { ok: false, error: err };
      }

      setRunning(true);
      setError(null);
      const outcome = await onRun(source);
      setRunning(false);

      if (outcome.ok) {
        setResult(outcome.value);
      } else {
        setResult(null);
        setError(outcome.error);
      }
      return outcome;
    };

    useImperativeHandle(
      ref,
      (): CodeEditorApi => ({
        loadSource: (source) => {
          if (rejectUnsupportedLanguage(source.language)) return;
          setError(null);
          setResult(null);
          setLanguage(source.language);
          setContent(source.content);
          setStdin(source.stdin ?? '');
          setFilename(source.filename);
        },
        getSource: () => sourceRef.current,
        run: () => runSource(sourceRef.current),
      }),
      // runSource closes over canRun/onRun — without these deps, run() would
      // permanently use the values from whichever render first mounted the
      // ref, ignoring later prop changes (e.g. canRun flipping with role).
      [canRun, onRun]
    );

    const handleLanguageChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
      const next = e.target.value as Language;
      setLanguage(next);
      setResult(null);
      setError(null);
      onSourceChange?.(buildCodeSource(next, content, stdin, filename));
    };

    const handleContentChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
      const next = e.target.value;
      setContent(next);
      setResult(null);
      setError(null);
      onSourceChange?.(buildCodeSource(language, next, stdin, filename));
    };

    const handleStdinChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
      const next = e.target.value;
      setStdin(next);
      setResult(null);
      setError(null);
      onSourceChange?.(buildCodeSource(language, content, next, filename));
    };

    return (
      <div className="sek-code-editor" data-theme={theme ?? 'system'}>
        {error && (
          <div className="sek-code-editor__error" role="alert">
            {error.message}
          </div>
        )}
        <select
          className="sek-code-editor__language"
          value={language}
          onChange={handleLanguageChange}
          disabled={!canEdit}
        >
          {LANGUAGE_OPTIONS.map((lang) => (
            <option key={lang} value={lang}>
              {LANGUAGE_LABELS[lang]}
            </option>
          ))}
        </select>
        <textarea
          className="sek-code-editor__source"
          value={content}
          onChange={handleContentChange}
          disabled={!canEdit}
          spellCheck={false}
        />
        <textarea
          className="sek-code-editor__stdin"
          value={stdin}
          onChange={handleStdinChange}
          disabled={!canEdit}
          placeholder="stdin (optional)"
        />
        {canRun && (
          <div className="sek-code-editor__actions">
            <button
              type="button"
              onClick={() => void runSource(sourceRef.current)}
              disabled={running}
            >
              {running ? 'Running…' : 'Run'}
            </button>
          </div>
        )}
        {result && (
          <div
            className="sek-code-editor__result"
            data-timed-out={result.timedOut}
          >
            <pre className="sek-code-editor__stdout">{result.stdout}</pre>
            {result.stderr && (
              <pre className="sek-code-editor__stderr">{result.stderr}</pre>
            )}
            <div className="sek-code-editor__meta">
              exit {result.exitCode} · {result.durationMs}ms
              {result.timedOut ? ' · timed out' : ''}
            </div>
          </div>
        )}
      </div>
    );
  }
);
