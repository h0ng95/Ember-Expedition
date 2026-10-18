// Usage: node tools/render-planning-review.mjs [source.md] [path/to/marked.esm.js]
// Requires marked. The optional module path supports a locally bundled runtime.
import { readFile, writeFile } from 'node:fs/promises';
import { resolve, basename } from 'node:path';
import { pathToFileURL } from 'node:url';

const sourcePath = resolve(process.argv[2] || 'docs/slotbound-comparison-v0.4.md');
const { marked } = process.argv[3]
  ? await import(pathToFileURL(resolve(process.argv[3])).href)
  : await import('marked');
const source = await readFile(sourcePath, 'utf8');
const escape = value => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;')
  .replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const title = source.match(/^# (.+)$/m)?.[1] || '기획 검토';
const toc = [];
let index = 0;
let content = marked.parse(source, { gfm: true });
content = content.replace(/<h1>.*?<\/h1>\s*/s, '');
content = content.replace(/<h2>(.*?)<\/h2>/g, (_, heading) => {
  const id = `section-${++index}`;
  toc.push(`<a href="#${id}">${heading}</a>`);
  return `<h2 id="${id}">${heading}</h2>`;
});
content = content.replace(/<table>/g, '<div class="table-wrap" tabindex="0" role="region" aria-label="비교 표"><table>')
  .replace(/<\/table>/g, '</table></div>');
const outputPath = sourcePath.replace(/\.md$/i, '.html');
if (outputPath === sourcePath) throw new Error('The source file must end in .md.');
const html = `<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>${escape(title)}</title>
<style>
:root{--ink:#233a38;--muted:#60706a;--paper:#fffefb;--line:#dedfd5;--green:#245c50;--amber:#a95924;--wash:#f2f1e9}
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:var(--wash);color:var(--ink);font:16px/1.85 'Malgun Gothic','Apple SD Gothic Neo',sans-serif;word-break:keep-all;overflow-wrap:anywhere}
a{color:var(--green);text-underline-offset:4px}a:hover{color:var(--amber)}a:focus-visible,button:focus-visible,.table-wrap:focus-visible{outline:3px solid #ca8d4a;outline-offset:4px}
.hero{background:#173c35;color:#fff8e9;border-bottom:5px solid #bf874b;padding:48px max(28px,calc((100vw - 1200px)/2)) 38px}
.eyebrow{color:#bcd4c2;letter-spacing:.16em;font-size:12px;font-weight:700}.hero h1{font-size:clamp(27px,3.2vw,40px);line-height:1.4;margin:12px 0 16px;max-width:1000px}.hero p{max-width:900px;margin:0;color:#d7e0d6;font-size:17px}
.chips{display:flex;flex-wrap:wrap;gap:8px;margin-top:24px}.chips span{font-size:12px;padding:5px 13px;border:1px solid #557064;border-radius:30px;color:#e5ebdf}
.layout{display:grid;grid-template-columns:226px minmax(0,1fr);gap:28px;max-width:1280px;margin:30px auto;padding:0 24px}
nav{position:sticky;top:24px;max-height:calc(100vh - 48px);overflow:auto;align-self:start;padding:12px 4px 24px;font-size:12px;line-height:1.6}nav strong{display:block;color:var(--muted);font-size:11px;letter-spacing:.12em;margin-bottom:12px}nav a{display:block;text-decoration:none;padding:9px 11px;border-left:2px solid transparent}nav a:hover{background:#e5e7da;border-left-color:var(--green)}nav .source-link{border-top:1px solid var(--line);margin-top:14px;padding-top:15px;font-weight:bold}
main{min-width:0;background:var(--paper);border:1px solid var(--line);border-radius:12px;padding:36px 42px;box-shadow:0 8px 30px #263d3010}main>p:first-child{color:var(--muted);font-size:12px;margin-top:0}main>p:nth-child(2){background:#edf3e9;border-left:4px solid #39765e;padding:18px 22px;font-size:18px;line-height:1.8}
h2{font-size:24px;line-height:1.5;margin:48px 0 22px;padding-top:28px;border-top:1px solid var(--line);scroll-margin-top:24px;color:#194f41}h3{font-size:19px;line-height:1.55;margin:30px 0 12px;color:#684421}p{margin:15px 0}li{margin:9px 0}ul,ol{padding-left:24px}strong{font-weight:700}code{font:0.88em/1.6 Consolas,'Malgun Gothic',monospace;background:#eef0e9;padding:2px 5px;border-radius:3px}
pre{background:#eaf0e9;border:1px solid #d1decf;border-radius:8px;padding:24px;margin:24px 0;white-space:pre-wrap;line-height:1.8}pre code{background:none;padding:0;font-family:'Malgun Gothic',sans-serif}
.table-wrap{overflow-x:auto;margin:24px 0;border:1px solid var(--line);border-radius:7px}table{border-collapse:collapse;width:100%;font-size:13px;line-height:1.8}th,td{text-align:left;vertical-align:top;padding:13px 15px;border-bottom:1px solid var(--line)}th{background:#e7ede4;color:#24483b;font-weight:700}td:first-child{font-weight:700;min-width:105px}tbody tr:nth-child(even){background:#f8f8f1}tbody tr:last-child td{border-bottom:0}table:has(th:nth-child(4)){min-width:660px}th:nth-child(4):last-child{width:29%}
footer{max-width:1280px;margin:25px auto;padding:0 24px 36px;color:var(--muted);font-size:12px;text-align:right}footer button{border:1px solid #9eaea0;background:#fffefb;color:var(--green);border-radius:5px;padding:8px 16px;margin-left:14px;cursor:pointer}
@media(max-width:1000px){.layout{grid-template-columns:180px minmax(0,1fr);gap:18px;padding:0 16px}main{padding:26px}.hero{padding:34px 28px}nav{font-size:11px}}
@media(max-width:740px){.layout{display:block;margin:18px auto;padding:0 12px}nav{position:static;max-height:none;padding:0 4px 16px}nav a{display:inline-block;padding:7px 9px}nav .source-link{display:block}main{padding:22px 18px}.hero{padding:28px 22px}.hero p{font-size:15px}h2{font-size:21px}main>p:nth-child(2){font-size:16px;padding:14px}body{font-size:15px}th,td{padding:11px 12px}}
@media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}}
@media print{body{background:white;font-size:10pt;line-height:1.7}.hero{background:white;color:#173c35;border-bottom:2px solid #173c35;padding:0 0 18px}.hero h1{font-size:24pt}.hero p{color:#38534b;font-size:11pt}.eyebrow,.chips,nav,footer{display:none}.layout{display:block;margin:0;padding:0}main{padding:0;border:0;box-shadow:none}main>p:nth-child(2){font-size:12pt}h2{font-size:16pt;margin-top:24px;padding-top:16px}h3{font-size:12pt}h2,h3{break-after:avoid}.table-wrap{overflow:visible;border:0}table,table:has(th:nth-child(4)){min-width:0;font-size:9pt}th,td{padding:7px;word-break:normal;overflow-wrap:anywhere}td:first-child{min-width:0}tr{break-inside:avoid}a{color:inherit}pre{font-size:10pt}@page{size:A4;margin:18mm}}
</style>
</head>
<body>
<header class="hero">
<div class="eyebrow">EMBER EXPEDITION · DESIGN REVIEW 0.4</div>
<h1>${escape(title)}</h1>
<p>조합이 완성되는 성장의 쾌감과 배치로 이기는 공략을 중심에 두고, 기존 기획과 참고작의 규칙을 비교합니다.</p>
<div class="chips"><span>현재 조건</span><span>Slotbound 공개 구조</span><span>성장과 조합</span><span>배치와 보스</span><span>수정 제안</span></div>
</header>
<div class="layout">
<nav aria-label="문서 목차"><strong>CONTENTS</strong>${toc.join('\n')}<a class="source-link" href="${escape(basename(sourcePath))}">노션용 Markdown 원문</a></nav>
<main>${content}</main>
</div>
<footer>기획 비교 단계 · 세부 규칙과 수치는 검토안입니다.<button type="button" onclick="window.print()">인쇄 / PDF 저장</button></footer>
</body>
</html>`;
await writeFile(outputPath, html, 'utf8');
console.log(`Rendered ${toc.length} sections: ${outputPath}`);
