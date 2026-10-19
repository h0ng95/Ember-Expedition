import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const directory = dirname(fileURLToPath(import.meta.url));
const manifestPath = join(directory, 'art-manifest.json');
const galleryPath = join(directory, 'animation-gallery.html');
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
if (manifest.entries?.length !== 30 || manifest.entries.some(entry => entry.frames?.length !== 22))
  throw new Error('Expected 30 art entries with 22 frames each before syncing the gallery.');
const html = readFileSync(galleryPath, 'utf8');
const start = '/*MANIFEST_START*/';
const end = '/*MANIFEST_END*/';
const first = html.indexOf(start), last = html.indexOf(end, first + start.length);
if (first < 0 || last < 0) throw new Error('Manifest marker missing from gallery.');
const synced = html.slice(0, first + start.length) + JSON.stringify(manifest) + html.slice(last);
writeFileSync(galleryPath, synced);
console.log(`Synced ${manifest.entries.length} sheets into ${galleryPath}`);
