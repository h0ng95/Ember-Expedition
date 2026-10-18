import http from 'node:http';
import {readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const root = path.dirname(fileURLToPath(import.meta.url));
const routes = new Map([
  ['/', ['index.html','text/html; charset=utf-8']],
  ['/index.html', ['index.html','text/html; charset=utf-8']],
  ['/style.css', ['style.css','text/css; charset=utf-8']],
  ['/app.js', ['app.js','text/javascript; charset=utf-8']],
  ['/assets/forest-battlefield.png', ['assets/forest-battlefield.png','image/png']],
  ['/assets/expedition-sprites-v2.png', ['assets/expedition-sprites-v2.png','image/png']]
]);
const server = http.createServer(async (req,res) => {
  const entry=routes.get(new URL(req.url,'http://127.0.0.1').pathname);
  if(!entry || !['GET','HEAD'].includes(req.method)){res.writeHead(404);res.end();return;}
  try{
    const bytes=await readFile(path.join(root,entry[0]));
    res.writeHead(200,{'Content-Type':entry[1],'Cache-Control':'no-store','X-Content-Type-Options':'nosniff'});
    res.end(req.method==='HEAD'?undefined:bytes);
  }catch{res.writeHead(404);res.end();}
});
server.listen(4173,'127.0.0.1',()=>console.log('UI preview: http://127.0.0.1:4173 — only the six prototype files are served.'));
