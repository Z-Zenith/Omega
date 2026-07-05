/**
 * SEK-02 — Document viewer & annotator public interface.
 *
 * Spec: docs/Campus platform architecture.md, Section 3 (SEK-02).
 *   "View and annotate PDF, PPTX, DOCX with highlight, text box, ink, and basic OCR
 *    (moved from SDA-06)."
 *
 * Acceptance criteria this interface MUST enforce:
 *   - "Highlight/text-box/ink annotations persist on reopening the same PDF" —
 *     Annotation is a first-class persisted entity; the embedder supplies the
 *     onAnnotationChange callback so the Backend API stores it, and the
 *     initialAnnotations prop re-hydrates on reopen.
 *   - PDF / PPTX / DOCX are the only supported types at launch. Annotation is
 *     PDF-only per spec ("annotate PDFs with highlights, text boxes, ink, and
 *     basic OCR") — other doc types are view-only.
 */

import type { Result, SekError, UserContext } from '../types/common.js';

/** Document types supported at launch. Adding one is a contract change. */
export type DocumentType = 'pdf' | 'pptx' | 'docx';

/** Per-document-type metadata. */
export interface DocumentDescriptor {
  readonly id: string;
  readonly type: DocumentType;
  readonly title: string;
  /** GCS / object-store URL the embedder resolves through the Backend API (API-03). */
  readonly fileUrl: string;
  /** Page count, if known. PDF only. */
  readonly pageCount?: number;
  /** Whether OCR has been run on this document. PDF only. */
  readonly ocrApplied?: boolean;
}

/** Annotation is PDF-only per the spec. Other doc types are view-only. */
export type Annotation =
  | HighlightAnnotation
  | TextBoxAnnotation
  | InkAnnotation;

/** Coordinates use the PDF's intrinsic coordinate space (0..1 normalized). */
interface NormalizedRect {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

export interface HighlightAnnotation {
  readonly kind: 'highlight';
  readonly id: string;
  readonly page: number;
  readonly rect: NormalizedRect;
  /** Hex color, e.g. "#FFEB3B". */
  readonly color: string;
  /** Optional note attached to the highlight. */
  readonly note?: string;
  readonly createdAt: string; // ISO 8601
  readonly createdBy: string; // user id
}

export interface TextBoxAnnotation {
  readonly kind: 'textBox';
  readonly id: string;
  readonly page: number;
  readonly position: NormalizedRect;
  readonly text: string;
  /** Hex color of the text. */
  readonly color: string;
  readonly createdAt: string;
  readonly createdBy: string;
}

export interface InkAnnotation {
  readonly kind: 'ink';
  readonly id: string;
  readonly page: number;
  /** Vector stroke list — diagrams (SEK-05) reuse the same vector primitive. */
  readonly strokes: ReadonlyArray<InkStroke>;
  readonly createdAt: string;
  readonly createdBy: string;
}

/**
 * One stroke is a sequence of points in normalized 0..1 page space.
 * SEK-05 will reuse the same shape for vector shape data.
 */
export interface InkStroke {
  readonly color: string;
  readonly width: number; // in CSS pixels at 1x zoom
  readonly points: ReadonlyArray<{ readonly x: number; readonly y: number }>;
}

/** Result of a single OCR pass on a page. */
export interface OcrPageResult {
  readonly page: number;
  readonly text: string;
  /** Per-word bounding boxes, normalized 0..1 page space. */
  readonly words: ReadonlyArray<{
    readonly text: string;
    readonly rect: NormalizedRect;
    readonly confidence: number;
  }>;
}

export interface DocumentViewerProps {
  readonly user: UserContext;
  readonly document: DocumentDescriptor;
  /** Annotations to re-hydrate on open — drives the "persist on reopen" criterion. */
  readonly initialAnnotations: ReadonlyArray<Annotation>;
  /** Whether the user can add/edit annotations. Read-only review otherwise. */
  readonly canAnnotate: boolean;
  /** Whether the user can run OCR. */
  readonly canOcr: boolean;
  /**
   * Persist an annotation (create, update, delete). The embedder forwards to
   * the Backend API which writes the JSONB `annotations` column on `documents`
   * (see docs/campus-platform-db-api-schema.md Section 1.9).
   */
  readonly onAnnotationChange: (
    change: AnnotationChange,
  ) => Promise<Result<Annotation, SekError>>;
  /**
   * Run OCR on a single page. The embedder forwards to the AI Services
   * container. The "basic OCR" promised in the spec is intentionally narrow
   * — the embedder should display a "best-effort" hint next to the result.
   */
  readonly onOcrPage: (page: number) => Promise<Result<OcrPageResult, SekError>>;
}

export type AnnotationChange =
  | { readonly op: 'create'; readonly annotation: Annotation }
  | { readonly op: 'update'; readonly annotation: Annotation }
  | { readonly op: 'delete'; readonly id: string };

export interface DocumentViewerApi {
  /** Navigate to a specific page. 1-indexed. */
  goToPage(page: number): void;
  /** Read out all current annotations. */
  getAnnotations(): ReadonlyArray<Annotation>;
}
