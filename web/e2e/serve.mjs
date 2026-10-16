// @ts-check
// Serves the production build the way the real host does (deploy/Caddyfile, ADR 0033): the app and the Api on one
// origin — /api and /health forwarded to API_URL — the market's language at /, the source language under /en/, every
// other unknown path answered with its language's index.html for the router, under the host's Content-Security-Policy,
// read from deploy/Caddyfile so the tests run under the one that ships.
import { createReadStream, existsSync, readFileSync, statSync } from 'node:fs';
import { createServer, request } from 'node:http';
import { extname, join, normalize, sep } from 'node:path';

const root = normalize(join(import.meta.dirname, '..', 'dist', 'apptemplate-web', 'browser'));
const port = Number(process.env['E2E_PORT'] ?? 4400);
const api = new URL(process.env['API_URL'] ?? 'http://localhost:5062');
const caddyfile = readFileSync(join(import.meta.dirname, '..', '..', 'deploy', 'Caddyfile'), 'utf8');
const policy = /Content-Security-Policy "([^"]+)"/.exec(caddyfile)?.[1];
if (!policy) {
  throw new Error('No Content-Security-Policy in deploy/Caddyfile.');
}
/** @type {Record<string, string>} */
const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.svg': 'image/svg+xml',
  '.woff2': 'font/woff2',
  '.woff': 'font/woff',
};

/** @param {string} path */
const toApi = (path) => path.startsWith('/api/') || path.startsWith('/health/');
/** @param {string} path */
const english = (path) => path === '/en' || path.startsWith('/en/');

// The request's path, decoded; a malformed escape is no path at all.
/** @param {string} url */
function pathOf(url) {
  try {
    return decodeURIComponent(new URL(url, 'http://localhost').pathname);
  } catch {
    return '/';
  }
}

const server = createServer((req, res) => {
  const url = req.url ?? '/';

  if (toApi(url)) {
    const upstream = request(
      { host: api.hostname, port: api.port, path: url, method: req.method, headers: req.headers },
      (answer) => {
        res.writeHead(answer.statusCode ?? 502, answer.headers);
        answer.pipe(res);
      },
    );
    upstream.on('error', () => req.socket.destroy());
    req.pipe(upstream);
    return;
  }

  const path = pathOf(url);
  let file = normalize(join(root, path));
  if (!(file + sep).startsWith(root + sep) || !existsSync(file) || statSync(file).isDirectory()) {
    file = join(root, english(path) ? 'en' : '', 'index.html');
  }
  res.writeHead(200, {
    'Content-Type': types[extname(file)] ?? 'application/octet-stream',
    'Cache-Control': 'no-cache',
    'Content-Security-Policy': policy,
  });
  createReadStream(file).pipe(res);
});

server.listen(port, () => console.log(`The app on http://localhost:${port}, the Api on ${api.origin}`));
