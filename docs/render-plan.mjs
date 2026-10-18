import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';
import { marked } from 'file:///C:/Users/whwog/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/marked/lib/marked.esm.js';

const require = createRequire(import.meta.url);
const { chromium } = require('C:/Users/whwog/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const folder = path.dirname(new URL(import.meta.url).pathname.replace(/^\/(\w:)/, '$1'));
const md = await fs.readFile(path.join(folder, 'mobile-autobattler-plan-v0.2.md'), 'utf8');
let index = 0;
const sections = [];
const renderer = new marked.Renderer();
renderer.heading = function(token) {
  const text = this.parser.parseInline(token.tokens);
  const id = `section-${index++}`;
  if (token.depth === 2) sections.push({ id, text });
  return `<h${token.depth} id="${id}">${text}</h${token.depth}>\n`;
};
const content = marked.parse(md, { renderer, gfm: true });
const unit = (name, role, cls='') => `<div class="unit ${cls}"><span class="piece">${name.slice(0,1)}</span><b>${name}</b><small>${role}</small></div>`;
const mockup = `
<figure id="landscape-figure">
  <figcaption><strong>가로 준비 화면</strong><span>구조 검토용 시안 · 캐릭터와 아트 미확정</span></figcaption>
  <div class="game-shell">
    <div class="game-top"><div><b>먼지 폐허</b><span>전투 7 / 15</span></div><div class="resources"><span>골드 <b>18</b></span><span>재정비 <b>1회</b></span><span class="pause">일시정지</span></div></div>
    <div class="game-body">
      <div class="battlefield">
        <div class="field-label"><b>우리 원정대</b><span>출전 5 / 5 · 칸을 눌러 배치</span></div>
        <div class="battle-boards">
          <div class="allies"><div class="column-labels"><span>후방</span><span>중앙</span><span>전방 →</span></div><div class="formation">${unit('등불사','보호막')}${unit('정찰병','표식')}${unit('방패병','반격','selected')}${unit('포수','집중 사격')}<div class="empty-slot">빈칸</div>${unit('수선공','회복')}</div></div>
          <div class="cross">대치</div>
          <div class="enemies"><div class="column-labels"><span>← 전방</span><span>중앙</span><span>후방</span></div><div class="formation">${unit('추격자','기본 공격','enemy')}<div class="empty-slot"></div>${unit('저격수','후방 표적','enemy')}<div class="empty-slot"></div>${unit('추격자','기본 공격','enemy')}<div class="empty-slot"></div></div></div>
        </div>
        <div class="synergy-strip"><b>현재 전술</b><span>깨진 방벽 · 보호막이 깨질 때 반격</span></div>
      </div>
      <aside class="context-panel"><div class="danger"><span class="eyebrow">다음 적의 특징</span><b>후방을 노리는 저격</b><p>가장 뒤쪽 동료를 먼저 공격합니다.</p></div><div class="selected-info"><span class="eyebrow">선택한 동료</span><h3>방패병</h3><p>반격 분기 · 승급 1 / 2</p><div class="info-rule"></div><p>같은 행의 뒤쪽 동료를 보호합니다. 피격 시 반격을 준비합니다.</p><span class="detail-label">능력 상세 보기</span></div></aside>
    </div>
    <div class="game-bottom"><div class="bench"><span>대기 1 / 2</span><b>길잡이</b></div><div class="prep-actions"><span>장비</span><span>영입 6</span><span>승급 16</span></div><div class="start-action">전투 시작 →</div></div>
  </div>
</figure>`;

const html = `<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>모바일 원정 오토배틀러 기획 검토안</title><style>
:root{--ink:#172532;--muted:#617180;--line:#dbe3e7;--blue:#20556b;--paper:#fff;--wash:#f2f5f6}*{box-sizing:border-box}body{margin:0;background:var(--wash);color:var(--ink);font-family:'Malgun Gothic','Apple SD Gothic Neo',sans-serif;font-size:15px;line-height:1.9}a{color:var(--blue);text-underline-offset:3px}header{background:#172c37;color:#f7fafb;padding:44px max(32px,calc((100vw - 1200px)/2)) 38px}header .eyebrow{color:#a6c3cd;letter-spacing:.14em;font-size:12px}header h1{font-size:32px;line-height:1.4;margin:12px 0}header p{max-width:840px;color:#cedde2;margin:0}.layout{display:grid;grid-template-columns:220px minmax(0,980px);gap:30px;max-width:1270px;margin:30px auto;padding:0 20px}nav{position:sticky;top:24px;height:calc(100vh - 48px);overflow:auto;font-size:12px;padding:8px 12px 16px 0}nav b{font-size:13px;display:block;margin-bottom:12px}nav a{display:block;text-decoration:none;padding:6px 0;color:#4e626f;line-height:1.5}nav a:hover{color:#0c7186}main{padding:42px 44px;background:var(--paper);border:1px solid var(--line);border-radius:12px;min-width:0}article>h1{display:none}h2{font-size:23px;line-height:1.5;margin:46px 0 16px;padding-top:18px;border-top:1px solid var(--line);scroll-margin-top:24px}h3{font-size:17px;margin:24px 0 12px}p{margin:15px 0}strong{font-weight:700}table{width:100%;border-collapse:collapse;font-size:13px;line-height:1.65;margin:22px 0}th,td{border-bottom:1px solid var(--line);padding:11px 12px;text-align:left;vertical-align:top}th{background:#edf3f5;color:#244350;font-weight:700}td:first-child{font-weight:600;min-width:90px}pre{background:#f4f7f8;border:1px solid var(--line);padding:18px;border-radius:8px;font-family:'Malgun Gothic',monospace;font-size:12px;line-height:1.7;overflow:auto}li{margin:8px 0}footer{color:var(--muted);font-size:12px;text-align:center;padding:28px}.figure-wrap{max-width:1240px;margin:28px auto;padding:0 20px}figure{margin:0;padding:24px;background:#eaf0f2;border:1px solid #d5dfe4;border-radius:12px}figcaption{display:flex;justify-content:space-between;align-items:center;margin-bottom:18px;color:#233c48}figcaption strong{font-size:18px}figcaption span{font-size:12px;color:#5a6f7c}.game-shell{background:#101f2a;border-radius:22px;padding:14px;color:#e5eef1;box-shadow:0 12px 24px #152b3720}.game-top{height:58px;display:flex;justify-content:space-between;align-items:center;padding:0 18px;background:#1a303e;border-radius:10px 10px 0 0}.game-top b{font-size:16px}.game-top span{font-size:12px;color:#c2d2d8;margin-left:18px}.resources{display:flex;align-items:center}.resources b{color:#f2d68e}.pause{border:1px solid #55717e;border-radius:7px;padding:5px 10px}.game-body{display:grid;grid-template-columns:1fr 230px;gap:16px;padding:20px 18px 16px;background:#203948;min-height:310px}.field-label{display:flex;align-items:center;justify-content:space-between;font-size:13px;margin-bottom:14px}.field-label span{font-size:11px;color:#b2c8d2}.battle-boards{display:grid;grid-template-columns:1fr 42px 1fr;align-items:center;gap:6px}.column-labels{display:grid;grid-template-columns:repeat(3,1fr);text-align:center;color:#aec4ce;font-size:11px;margin-bottom:8px}.formation{display:grid;grid-template-columns:repeat(3,1fr);grid-template-rows:repeat(2,84px);gap:7px}.unit{border:1px solid #63828e;background:#304e5d;border-radius:9px;display:flex;flex-direction:column;align-items:center;justify-content:center;line-height:1.3}.unit b{font-size:12px;font-weight:700;margin:3px 0}.unit small{font-size:10px;color:#d4e1e6}.piece{display:flex;align-items:center;justify-content:center;width:25px;height:25px;border-radius:7px;background:#77a5ad;color:#0c2933;font-weight:700;font-size:13px}.unit.selected{border:2px solid #e9c875;background:#3a514e}.unit.selected .piece{background:#e9c875;color:#3d3621}.unit.enemy{background:#51454a;border-color:#8b747b}.unit.enemy .piece{background:#c9a1a0;color:#3b2628}.empty-slot{border:1px dashed #59717e;border-radius:9px;display:flex;align-items:center;justify-content:center;color:#97aebb;font-size:12px;opacity:.7}.cross{color:#b4c7ce;text-align:center;font-size:10px}.synergy-strip{display:flex;gap:14px;margin-top:17px;padding:8px 10px;background:#142c38;border-radius:6px;font-size:11px}.synergy-strip b{color:#e7cd8c}.context-panel{border-left:1px solid #4a606d;padding-left:17px;font-size:12px;line-height:1.6}.context-panel .eyebrow{display:block;color:#b0c6cf;font-size:10px;margin-bottom:5px}.danger{padding-bottom:12px;border-bottom:1px solid #4a606d}.danger b{color:#f0c5a5;font-size:13px}.danger p,.selected-info p{margin:6px 0}.selected-info{padding-top:13px}.selected-info h3{font-size:17px;margin:3px 0}.info-rule{height:1px;background:#496371;margin:10px 0}.detail-label{display:inline-block;margin-top:5px;color:#b9d7df;border-bottom:1px solid #b9d7df}.game-bottom{display:flex;align-items:center;gap:16px;background:#192e3c;padding:14px 18px;border-radius:0 0 10px 10px;min-height:71px}.bench{display:flex;gap:12px;align-items:center;font-size:12px}.bench span{color:#b5c9d2}.bench b{background:#304b59;padding:8px 15px;border-radius:6px}.prep-actions{display:flex;gap:8px;margin-left:auto}.prep-actions span{padding:8px 13px;border:1px solid #56707d;border-radius:6px;font-size:12px}.start-action{padding:11px 24px;border-radius:7px;background:#e8c879;color:#293525;font-weight:700;font-size:14px}.document-note{font-size:12px;color:#5d7180;text-align:right;margin-top:10px}@media(max-width:1000px){.layout{display:block;max-width:900px}nav{display:none}.figure-wrap{overflow:auto}.figure-wrap figure{min-width:960px}main{padding:28px}header h1{font-size:27px}}@media print{body{background:#fff;font-size:11pt}.layout{display:block;margin:0;padding:0}nav,header,.figure-wrap,footer{display:none}main{border:0;padding:0}article>h1{display:block}h2{break-after:avoid}table{font-size:9pt}tr{break-inside:avoid}a{color:inherit}pre{white-space:pre-wrap}}
</style></head><body><header><span class="eyebrow">GAME DESIGN REVIEW · VERSION 0.2</span><h1>모바일 원정 오토배틀러 기획 검토안</h1><p>가로 화면에서 팀을 만들고 바꾸는 재미를 중심으로, 랜덤 선택과 원정 경제를 연결한 개발 기준안.</p></header><div class="figure-wrap">${mockup}<div class="document-note">아래 본문과 Markdown 문서의 규칙은 동일합니다.</div></div><div class="layout"><nav><b>문서 목차</b>${sections.map(x=>`<a href="#${x.id}">${x.text}</a>`).join('')}</nav><main><article>${content}</article></main></div><footer>2026.10.18 · 규칙과 수치는 시제품 검증을 위한 초기안</footer></body></html>`;
const htmlPath=path.join(folder,'mobile-autobattler-plan-v0.2.html');
await fs.writeFile(htmlPath,html,'utf8');
const browser = await chromium.launch({ headless:true, channel:'chrome' });
const page=await browser.newPage({viewport:{width:1440,height:1100},deviceScaleFactor:1});
const errors=[];
page.on('pageerror',e=>errors.push(e.message));
await page.goto(pathToFileURL(htmlPath).href);
await page.locator('#landscape-figure').screenshot({path:path.join(folder,'landscape-layout-v0.2.png')});
await page.screenshot({path:path.join(folder,'plan-preview-v0.2.png')});
const report=await page.evaluate(()=>({title:document.title,headings:document.querySelectorAll('article h2').length,tables:document.querySelectorAll('article table').length,bodyScrollWidth:document.body.scrollWidth,viewportWidth:window.innerWidth,links:[...document.querySelectorAll('article a')].map(a=>a.href)}));
await browser.close();
console.log(JSON.stringify({htmlPath,...report,errors},null,2));
