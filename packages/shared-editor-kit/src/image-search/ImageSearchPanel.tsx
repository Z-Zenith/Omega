/**
 * SEK-04 — built-in image search panel.
 *
 * Implements the ImageSearchProps contract from ./types.ts. Rendered only as a
 * child of NotesEditor (SEK-03) — there is no standalone "image search" screen,
 * per the acceptance criterion ("no separate 'image search' screen exists
 * outside the notes editor").
 */
import { useState } from 'react';
import type { SekError } from '../types/common.js';
import type { ImageInsert, ImageSearchProps, ImageSearchResult } from './types.js';

export interface ImageSearchPanelProps extends ImageSearchProps {
  /** Called once a result has been uploaded and is ready to embed in the note. */
  readonly onInsert: (insert: ImageInsert) => void;
}

export function ImageSearchPanel({ enabled, onSearch, onUploadImage, onInsert }: ImageSearchPanelProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<ReadonlyArray<ImageSearchResult>>([]);
  const [degraded, setDegraded] = useState(false);
  const [error, setError] = useState<SekError | null>(null);
  const [searching, setSearching] = useState(false);
  // Tracks which result is mid-upload so a second click can't fire a duplicate
  // onUploadImage call for the same (or another) result while one is in flight.
  const [insertingId, setInsertingId] = useState<string | null>(null);

  if (!enabled) {
    return null;
  }

  const handleSearch = async () => {
    const trimmed = query.trim();
    if (!trimmed || searching) {
      return;
    }
    setSearching(true);
    setError(null);
    const result = await onSearch(trimmed);
    if (result.ok) {
      setResults(result.value.results);
      setDegraded(result.value.degraded);
    } else {
      setError(result.error);
      setResults([]);
    }
    setSearching(false);
  };

  const handleInsert = async (result: ImageSearchResult) => {
    if (insertingId !== null) {
      return;
    }
    setInsertingId(result.id);
    // onUploadImage is what makes the eventual embed "embedded, not linked" —
    // this component never writes result.sourceUrl anywhere itself.
    const uploaded = await onUploadImage(result);
    if (uploaded.ok) {
      onInsert(uploaded.value);
      setError(null);
    } else {
      setError(uploaded.error);
    }
    setInsertingId(null);
  };

  return (
    <div className="sek-image-search">
      {error && (
        <div className="sek-image-search__error" role="alert">
          {error.message}
        </div>
      )}
      {degraded && (
        <div className="sek-image-search__degraded" role="status">
          Image search is currently degraded — results may be limited.
        </div>
      )}
      <div className="sek-image-search__query">
        <input
          className="sek-image-search__input"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              handleSearch();
            }
          }}
          placeholder="Search images…"
        />
        <button type="button" onClick={handleSearch} disabled={searching || !query.trim()}>
          Search
        </button>
      </div>
      <ul className="sek-image-search__results">
        {results.map((result) => (
          <li key={result.id} className="sek-image-search__result">
            <button
              type="button"
              onClick={() => handleInsert(result)}
              disabled={insertingId !== null}
              aria-label={`Insert image: ${result.title}`}
            >
              <img src={result.thumbnailUrl} alt={result.title} width={result.width} height={result.height} />
            </button>
            <span className="sek-image-search__attribution">{result.attribution}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
