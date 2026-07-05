/**
 * Contract smoke test for the SEK public interface.
 *
 * This is a compile-time test — it doesn't run anything, but it forces every
 * public type and value to actually resolve from the package's subpath
 * exports. If a barrel re-export is broken or a type was accidentally
 * removed, this file fails to typecheck.
 *
 * Reference feature IDs are inlined in the comments so it's obvious which
 * spec row each symbol came from.
 */

import type {
  // Common
  UserContext,
  Result,
  SekError,
  SekErrorCode,
  // SEK-01
  Language,
  CodeSource,
  CodeRunResult,
  CodeEditorProps,
  CodeEditorApi,
  // SEK-02
  DocumentType,
  DocumentDescriptor,
  Annotation,
  HighlightAnnotation,
  TextBoxAnnotation,
  InkAnnotation,
  InkStroke,
  OcrPageResult,
  DocumentViewerProps,
  DocumentViewerApi,
  AnnotationChange,
  // SEK-03
  Note,
  NoteLink,
  NoteLinkRef,
  OutgoingLinks,
  Backlinks,
  NotesEditorProps,
  NotesEditorApi,
  // SEK-04
  ImageSearchResult,
  ImageSearchResponse,
  ImageInsert,
  ImageSearchProps,
} from '../src/index.js';

import { LANGUAGE_LABELS } from '../src/index.js';

// ---- subpath imports also resolve (tree-shaking contract) ----
import type { CodeEditorProps as CEP } from '../src/code-editor/index.js';
import type { DocumentViewerProps as DVP } from '../src/document-viewer/index.js';
import type { NotesEditorProps as NEP } from '../src/notes/index.js';
import type { ImageSearchProps as ISP } from '../src/image-search/index.js';
import type {
  UserContext as UC,
  Result as Res,
  SekError as SE,
} from '../src/types/common.js';

// ---- Exhaustiveness checks: every Language has a label, no extras ----
const _exhaustiveLanguage: Record<Language, string> = LANGUAGE_LABELS;
void _exhaustiveLanguage;

const _allLanguageLiterals: Language[] = [
  'c',
  'cpp',
  'python',
  'java',
  'dotnet',
  'html',
  'css',
  'javascript',
  'typescript',
  'nodejs',
  'sql',
  'json',
  'yaml',
];
void _allLanguageLiterals;

// ---- Result<T,E> discriminated union narrows correctly ----
function unwrap<T, E>(r: Result<T, E>): T {
  if (r.ok) return r.value;
  throw new Error(String(r.error));
}

// ---- SekErrorCode is a closed union (literal exhaustiveness) ----
const _errorCodeSample: SekErrorCode = 'unsupported_language';
void _errorCodeSample;

// ---- Annotation is a discriminated union of three kinds ----
function classifyAnnotation(a: Annotation): string {
  switch (a.kind) {
    case 'highlight':
      return a.rect.x.toString();
    case 'textBox':
      return a.text;
    case 'ink':
      return a.strokes[0]?.color ?? 'no-stroke';
  }
}

// ---- DocumentType is a closed union ----
const _docType: DocumentType = 'pdf';
void _docType;

// ---- Mock UserContext so we can construct props (compile-time only) ----
const user: UserContext = {
  userId: 'u1',
  sessionToken: 'tok',
  role: 'teacher',
  collegeId: 'c1',
};

// SEK-01 — props shape compiles
const _codeEditor: CodeEditorProps = {
  user,
  canRun: true,
  canEdit: true,
  onRun: async (_source: CodeSource): Promise<Result<CodeRunResult, SekError>> => ({
    ok: true,
    value: { stdout: '', stderr: '', exitCode: 0, durationMs: 1, timedOut: false },
  }),
};
void _codeEditor;

// SEK-02 — props shape compiles, AnnotationChange discriminated union works
const _annotationChange: AnnotationChange = { op: 'delete', id: 'a1' };
void _annotationChange;

// SEK-03 — NoteLinkRef round-trips
const _link: NoteLinkRef = { toNoteId: 'n2', anchor: 'see also' };
void _link;

// SEK-04 — ImageInsert is the stable URL, not the source URL (per spec)
const _imageInsert: ImageInsert = {
  embeddedUrl: 'content://img/abc',
  altText: 'alt',
  width: 800,
  height: 600,
  attribution: 'CC-BY / Author',
};
void _imageInsert;

// API surface references compile (not invoked)
declare const _codeApi: CodeEditorApi;
declare const _viewerApi: DocumentViewerApi;
declare const _notesApi: NotesEditorApi;
void _codeApi;
void _viewerApi;
void _notesApi;

// Type-only re-exports compile (these would fail if any barrel broke)
type _UC2 = UC;
type _Res2 = Res<string, SE>;
type _NoteLinkAlias = NoteLink;
type _HighlightAlias = HighlightAnnotation;
type _TextBoxAlias = TextBoxAnnotation;
type _InkAlias = InkAnnotation;
type _StrokeAlias = InkStroke;
type _OcrAlias = OcrPageResult;
type _DocDescAlias = DocumentDescriptor;
type _CodeSrcAlias = CodeSource;
type _CodeRunAlias = CodeRunResult;
type _BacklinksAlias = Backlinks;
type _OutgoingAlias = OutgoingLinks;
type _SearchResultAlias = ImageSearchResult;
type _SearchResponseAlias = ImageSearchResponse;
type _DVP2 = DVP;
type _NEP2 = NEP;
type _ISP2 = ISP;
type _CEP2 = CEP;
void (null as unknown as _UC2);
void (null as unknown as _Res2);
void (null as unknown as _NoteLinkAlias);
void (null as unknown as _HighlightAlias);
void (null as unknown as _TextBoxAlias);
void (null as unknown as _InkAlias);
void (null as unknown as _StrokeAlias);
void (null as unknown as _OcrAlias);
void (null as unknown as _DocDescAlias);
void (null as unknown as _CodeSrcAlias);
void (null as unknown as _CodeRunAlias);
void (null as unknown as _BacklinksAlias);
void (null as unknown as _OutgoingAlias);
void (null as unknown as _SearchResultAlias);
void (null as unknown as _SearchResponseAlias);
void (null as unknown as _DVP2);
void (null as unknown as _NEP2);
void (null as unknown as _ISP2);
void (null as unknown as _CEP2);
