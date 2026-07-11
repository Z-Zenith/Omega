/**
 * SEK-02 — runtime tests for the annotation-overlay geometry helpers.
 *
 * Uses Node's built-in test runner directly against TypeScript sources,
 * same pattern as tests/notes.linkExtraction.test.ts.
 */

import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  clamp01,
  isRectSizable,
  isStrokeSizable,
  rectFromPoints,
} from '../src/document-viewer/geometry.ts';

test('clamp01 leaves in-range values untouched', () => {
  assert.equal(clamp01(0.5), 0.5);
});

test('clamp01 clamps below 0 up to 0', () => {
  assert.equal(clamp01(-0.2), 0);
});

test('clamp01 clamps above 1 down to 1', () => {
  assert.equal(clamp01(1.4), 1);
});

test('clamp01 treats NaN as 0 rather than propagating it', () => {
  assert.equal(clamp01(Number.NaN), 0);
});

test('rectFromPoints builds a rect regardless of drag direction', () => {
  const rect = rectFromPoints({ x: 0.7, y: 0.6 }, { x: 0.2, y: 0.3 });
  assert.equal(rect.x, 0.2);
  assert.equal(rect.y, 0.3);
  // Float subtraction (0.7 - 0.2) isn't exact — assert within a tight tolerance
  // rather than bit-for-bit, since the geometry itself (not floating point) is
  // what this test is verifying.
  assert.ok(Math.abs(rect.width - 0.5) < 1e-9);
  assert.ok(Math.abs(rect.height - 0.3) < 1e-9);
});

test('rectFromPoints on identical points yields a zero-size rect', () => {
  const rect = rectFromPoints({ x: 0.5, y: 0.5 }, { x: 0.5, y: 0.5 });
  assert.deepEqual(rect, { x: 0.5, y: 0.5, width: 0, height: 0 });
});

test('isRectSizable rejects a stray-click-sized rect', () => {
  assert.equal(isRectSizable({ x: 0, y: 0, width: 0.001, height: 0.001 }), false);
});

test('isRectSizable accepts a deliberately dragged rect', () => {
  assert.equal(isRectSizable({ x: 0, y: 0, width: 0.1, height: 0.1 }), true);
});

test('isRectSizable rejects when only one dimension is too small', () => {
  assert.equal(isRectSizable({ x: 0, y: 0, width: 0.1, height: 0.001 }), false);
});

test('isStrokeSizable rejects a single-point tap', () => {
  assert.equal(isStrokeSizable([{ x: 0.1, y: 0.1 }]), false);
});

test('isStrokeSizable rejects an empty stroke', () => {
  assert.equal(isStrokeSizable([]), false);
});

test('isStrokeSizable accepts a two-point drag', () => {
  assert.equal(isStrokeSizable([{ x: 0.1, y: 0.1 }, { x: 0.2, y: 0.2 }]), true);
});

test('isStrokeSizable rejects duplicate-point jitter (pointer taps that still fire 2+ move events)', () => {
  assert.equal(
    isStrokeSizable([{ x: 0.5, y: 0.5 }, { x: 0.5, y: 0.5 }, { x: 0.5, y: 0.5 }]),
    false
  );
});

test('isStrokeSizable accepts many points that never individually move far but drift past the threshold', () => {
  const points = Array.from({ length: 5 }, (_, i) => ({ x: 0.5 + i * 0.005, y: 0.5 }));
  assert.equal(isStrokeSizable(points), true);
});
