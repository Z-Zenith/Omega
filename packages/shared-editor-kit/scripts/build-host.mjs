// SDA-19: bundles the SEK notes-editor host entry into a single standalone
// script an Avalonia NativeWebView can load with no bundler/node_modules of
// its own. A plain Node script (not npm-script shell chaining) so it runs
// identically on Windows and POSIX shells.
import { build } from 'esbuild';
import { cpSync, mkdirSync } from 'node:fs';

mkdirSync('dist/host', { recursive: true });

await build({
  entryPoints: ['src/host/notes-host-entry.tsx'],
  bundle: true,
  format: 'iife',
  target: 'es2022',
  outfile: 'dist/host/bundle.js',
});

cpSync('host/index.html', 'dist/host/index.html');
