// Run from any directory: node scripts/preview.cjs
// Frontend: http://localhost:3000/frontend/login/index.html
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../frontend');
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.ico': 'image/x-icon' };
const server = http.createServer((req, res) => {
  if (req.url === '/api' || req.url.startsWith('/api/') || req.url === '/health') {
    const proxy = http.request({ hostname: '127.0.0.1', port: 8080, path: req.url, method: req.method, headers: { ...req.headers, host: 'localhost:8080' } }, upstream => {
      res.writeHead(upstream.statusCode, upstream.headers);
      upstream.pipe(res);
    });
    proxy.on('error', () => {
      res.writeHead(502, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: { message: 'API に接続できません。start-local.bat で起動してください。' } }));
    });
    req.pipe(proxy);
    return;
  }
  if (req.url === '/') {
    res.writeHead(302, { Location: '/frontend/login/index.html' });
    res.end();
    return;
  }
  let pathname;
  try { pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname); }
  catch { res.writeHead(400); res.end(); return; }
  const file = path.resolve(root, pathname.slice('/frontend/'.length));
  const relative = path.relative(root, file);
  if (!pathname.startsWith('/frontend/') || relative.startsWith('..') || path.isAbsolute(relative) || pathname.includes('\0')) {
    res.writeHead(404); res.end(); return;
  }
  fs.readFile(file, (error, data) => {
    if (error) { res.writeHead(404); res.end(); return; }
    res.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    res.end(data);
  });
});
server.listen(3000, '127.0.0.1', () => console.log('Open http://localhost:3000/frontend/login/index.html (Ctrl+C to stop)'));
