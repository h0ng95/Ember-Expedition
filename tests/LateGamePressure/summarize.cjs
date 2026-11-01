const fs = require('fs');
const legal = JSON.parse(fs.readFileSync(process.argv[2]));
const controlled = JSON.parse(fs.readFileSync(process.argv[3]));
const pct = (n, d) => Math.round(n / d * 1000) / 10;
function stats(rows, key = 'result') {
  const outcomes = rows.map(r => r[key]);
  return { n: rows.length, wins: outcomes.filter(r => r.win).length,
    winPercent: pct(outcomes.filter(r => r.win).length, rows.length),
    timeouts: outcomes.filter(r => r.timeout).length,
    meanHpPercent: Math.round(outcomes.reduce((n,r) => n + r.health, 0) / rows.length * 1000) / 10 };
}
function group(rows, key, summarize) {
  return Object.fromEntries([...new Set(rows.map(r => r[key]))].map(v => [v, summarize(rows.filter(r => r[key] === v))]));
}
const summary = {
  rules: controlled.rules,
  legal: { routes: legal.routes.length, completed: legal.routes.filter(r => r.complete).length,
    actualBattles: legal.actual, isolatedReplays: legal.isolated, anchors: legal.anchors.length,
    waves: group(legal.anchors, 'wave', a => ({ current: stats(a,'current'), previous: stats(a,'previous'),
      next: a[0].next ? stats(a,'next') : null, compact: stats(a,'compact'), spread: stats(a,'spread') })),
    policies: group(legal.routes, 'policy', a => ({ n:a.length, completed:a.filter(r=>r.complete).length, acquired:a.filter(r=>r.acquired>0).length })) },
  controlled: { battles: controlled.matrix,
    waves: group(controlled.rows,'wave',a=>stats(a)),
    pairs: Object.fromEntries([[23,24],[28,29],[25,30]].map(([from,to]) => {
      const key=r=>[r.supply,r.build,r.backline,r.research,r.layout].join('|');
      const target=new Map(controlled.rows.filter(r=>r.wave===to).map(r=>[key(r),r]));
      const pairs=controlled.rows.filter(r=>r.wave===from).map(a=>[a,target.get(key(a))]);
      return [from+'-'+to,{ n:pairs.length,
        lossToWin:pairs.filter(([a,b])=>!a.result.win&&b.result.win).length,
        winToLoss:pairs.filter(([a,b])=>a.result.win&&!b.result.win).length,
        meanHpDifference:pairs.reduce((n,[a,b])=>n+b.result.health-a.result.health,0)/pairs.length }];
    })),
    budgets: group(controlled.rows,'supply',a=>group(a,'wave',b=>stats(b))),
    finalBossBuilds: group(controlled.rows.filter(r=>r.wave===30),'supply',a=>group(a,'build',b=>stats(b))),
    finalBossResearch: group(controlled.rows.filter(r=>r.wave===30),'supply',a=>group(a,'research',b=>stats(b))),
    wave29Builds: group(controlled.rows.filter(r=>r.wave===29 && r.supply===35),'build',a=>stats(a)),
    wave30WinningRows: controlled.rows.filter(r=>r.wave===30&&r.result.win) },
  limitations: [ 'Deterministic scenario coverage, not player win probabilities.',
    'Legal routes share two random seeds and fixed policies; reaching late waves selects stronger parties.',
    'Controlled teams equal recruited material and research cost, not recruitment luck or acquisition order.',
    'Two fixed layouts, six fixed research sets and selected evolution branches are not exhaustive.',
    'Controlled hybrid teams spend their one-copy saving on an inherited banner trait for the frontline.',
    'No production balance values were changed by this audit.' ]
};
const output=JSON.stringify(summary,null,2);
if(process.argv[4])fs.writeFileSync(process.argv[4],output);else console.log(output);
