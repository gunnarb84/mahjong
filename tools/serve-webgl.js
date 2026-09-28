#!/usr/bin/env node
/// Lokaler Test-Server fuer den Unity-WebGL-Build.
/// Setzt `Content-Encoding: gzip` fuer .unityweb (so verhaelt er sich wie
/// itch.io / ein konfigurierter Hoster) — der Browser dekomprimiert nativ.
/// Aufruf:  node tools/serve-webgl.js [Verzeichnis] [Port]
const http = require('http');
const fs = require('fs');
const path = require('path');

const root = path.resolve(process.argv[2] || path.join(__dirname, '..', 'build', 'WebGL'));
const port = Number(process.argv[3]) || 8130;

const mime = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.wasm': 'application/wasm',
  '.png': 'image/png',
};

http.createServer(function (req, res) {
  var url;
  try {
    url = decodeURIComponent(req.url.split('?')[0]);
  } catch (e) {
    res.writeHead(400);
    return res.end('400');
  }
  if (url.endsWith('/')) url += 'index.html';
  if (url.includes('..')) { res.writeHead(400); return res.end('400'); }

  var file = path.join(root, url);
  fs.readFile(file, function (err, data) {
    if (err) { res.writeHead(404); return res.end('404'); }
    if (url.endsWith('.unityweb')) {
      res.setHeader('Content-Encoding', 'gzip');
      res.setHeader('Content-Type', 'application/vnd.unity');
    } else {
      res.setHeader('Content-Type', mime[path.extname(url)] || 'application/octet-stream');
    }
    res.end(data);
  });
}).listen(port, function () {
  console.log('[Mahjong] Test-Server: http://localhost:' + port + '  (root: ' + root + ')');
});