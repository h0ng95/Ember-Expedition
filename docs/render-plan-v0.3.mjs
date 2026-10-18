import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { marked } from 'file:///C:/Users/whwog/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/marked/lib/marked.esm.js';

const folder = path.dirname(fileURLToPath(import.meta.url));
const md = await fs.readFile(path.join(folder, 'mobile-autobattler-plan-v0.3.md'), 'utf8');
const toc = [];
let number = 0;
const renderer = new marked.Renderer();
renderer.heading = function(token) {
  const label = this.parser.parseInline(token.tokens);
  const id = `section-${number++}`;
  if (token.depth === 2) toc.push({ id, label });
  return `<h${token.depth} id="${id}">${label}</h${token.depth}>`;
};
const content = marked.parse(md, {renderer, gfm:true});
const html = `<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>모바일 원정 오토배틀러 기획 검토안 v0.3</title><style>
*{box-sizing:border-box}body{margin:0;background:#f0f3f5;color:#20313e;font:15px/1.85 'Malgun Gothic','Apple SD Gothic Neo',sans-serif}header{background:#203440;color:#fff;padding:36px max(24px,calc((100vw - 1240px)/2))}header small{letter-spacing:.12em;color:#b7ced7}header h1{font-size:28px;margin:10px 0}header p{margin:0;color:#d2e1e5}.layout{max-width:1280px;margin:28px auto;display:grid;grid-template-columns:210px minmax(0,1fr);gap:28px;padding:0 24px}nav{font-size:12px;position:sticky;top:20px;height:calc(100vh - 40px);overflow:auto}nav a{display:block;line-height:1.5;padding:7px 0;text-decoration:none}main{background:white;border:1px solid #dbe3e8;border-radius:10px;padding:36px;min-width:0}main>h1{display:none}h2{font-size:22px;border-top:1px solid #dbe3e8;padding-top:24px;margin-top:40px;scroll-margin-top:20px}h3{font-size:17px;margin-top:28px}p{margin:15px 0}a{color:#205e77;text-underline-offset:3px}img{display:block;max-width:100%;height:auto;margin:18px auto;border:1px solid #dbe3e8}table{border-collapse:collapse;width:100%;font-size:13px;line-height:1.7;margin:24px 0}th,td{padding:10px;border-bottom:1px solid #dbe3e8;text-align:left;vertical-align:top}th{background:#edf3f5}td:first-child{font-weight:600}pre{font:12px/1.8 'Malgun Gothic',monospace;background:#f3f6f7;border:1px solid #dbe3e8;padding:18px;overflow:auto}li{margin:7px 0}footer{text-align:center;color:#667982;font-size:12px;padding:25px}@media(max-width:900px){.layout{display:block;padding:0 14px}nav{display:none}main{padding:22px}header h1{font-size:24px}table{font-size:12px}th,td{padding:7px}}@media print{body{background:white}.layout{display:block;margin:0;padding:0}nav,header,footer{display:none}main{border:0;padding:0}main>h1{display:block}h2,h3{break-after:avoid}tr,img{break-inside:avoid}pre{white-space:pre-wrap}}
</style></head><body><header><small>GAME DESIGN REVIEW · VERSION 0.3</small><h1>모바일 원정 오토배틀러 기획 검토안</h1><p>실제 참고작 화면 확인 · 도트 그래픽 · 가로 전장 · 전투 전 직접 배치</p></header>
<div class="layout"><nav><strong>문서 목차</strong>${toc.map(s=>`<a href="#${s.id}">${s.label}</a>`).join('')}</nav><main>${content}</main></div>
<footer>2026.10.18 · 참고 화면은 원작의 공개 자료이며, 제안한 디자인과 규칙은 미확정 검토안입니다.</footer></body></html>`;
const output = path.join(folder, 'mobile-autobattler-plan-v0.3.html');
await fs.writeFile(output, html, 'utf8');
console.log(JSON.stringify({output, sections:toc.length, images:(content.match(/<img /g)||[]).length}));
