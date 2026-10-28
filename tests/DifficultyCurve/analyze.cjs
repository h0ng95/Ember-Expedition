const fs=require('fs'),path=require('path');
const [beforeDir,afterDir,fixedFile,outDir]=process.argv.slice(2);
if(!outDir)throw Error('Usage: node analyze.cjs beforeDir afterDir fixedFile outputDir');
function load(dir){return fs.readdirSync(dir).filter(f=>f.endsWith('.json')).sort().map(file=>({file,...JSON.parse(fs.readFileSync(path.join(dir,file)))}));}
const before=load(beforeDir),after=load(afterDir),fixed=JSON.parse(fs.readFileSync(fixedFile));
if(before.length!==20||after.length!==20)throw Error('Incomplete 20-job matrix');
const pairs=[];
for(let i=0;i<before.length;i++){
 const a=before[i],b=after[i];
 if(a.file!==b.file||a.firstSeed!==b.firstSeed||a.count!==b.count)throw Error('Unmatched cohorts');
 for(const n of b.seedResults){const o=a.seedResults.find(s=>s.seed===n.seed&&s.policy===n.policy&&s.spending===n.spending);if(!o)throw Error('Missing pair');pairs.push({starter:a.starter,branch:a.researchBranch,seed:n.seed,policy:n.policy,spending:n.spending,before:o.wins,after:n.wins});}
}
function stats(reports){const a=reports.flatMap(r=>r.seedResults);return {runs:a.length,complete:a.filter(s=>s.complete).length,battles:a.reduce((n,s)=>n+s.attempts,0),meanWins:a.reduce((n,s)=>n+s.wins,0)/a.length};}
function waves(reports){return Array.from({length:30},(_,i)=>{const a=reports.flatMap(r=>r.waves).filter(r=>r.wave===i+1),sum=k=>a.reduce((n,s)=>n+s[k],0),n=sum('reached'),t=sum('attempts');return {wave:i+1,reached:n,firstWins:sum('firstWins'),firstWinRate:100*sum('firstWins')/n,attempts:t,timeouts:sum('timeouts'),meanSeconds:a.reduce((n,s)=>n+s.meanSeconds*s.attempts,0)/t,meanHealthFraction:a.reduce((n,s)=>n+s.meanHealthFraction*s.attempts,0)/t};});}
const summary={firstSeed:before[0].firstSeed,count:before[0].count,note:'Held-out legal expeditions; stage rates conditional on reaching the stage. Not human win rates. Fixed-party report has separate isolated comparisons.',before:stats(before),after:stats(after),changed:{improved:pairs.filter(p=>p.after>p.before).length,worsened:pairs.filter(p=>p.after<p.before).length,same:pairs.filter(p=>p.after===p.before).length,gainedComplete:pairs.filter(p=>p.after===30&&p.before<30).length,lostComplete:pairs.filter(p=>p.before===30&&p.after<30).length},waves:{before:waves(before),after:waves(after)},fixed:{runs:fixed.runs,complete:fixed.complete,actualBattles:fixed.actualBattles,counterfactualBattles:fixed.counterfactualBattles,rows:fixed.rows},pairs};
fs.mkdirSync(outDir,{recursive:true});
fs.writeFileSync(path.join(outDir,'summary.json'),JSON.stringify(summary,null,2));
fs.writeFileSync(path.join(outDir,'balance-results.json'),JSON.stringify({before,after}));
fs.copyFileSync(fixedFile,path.join(outDir,'fixed-party.json'));
console.log(JSON.stringify({before:summary.before,after:summary.after,changed:summary.changed,fixed:summary.fixed.rows.filter(r=>[13,14,17,18,20].includes(r.wave)).map(r=>({wave:r.wave,n:r.n,before:r.beforeWins,after:r.currentWins,hpBefore:r.beforeHp,hpAfter:r.currentHp,next:r.nextWins,boss:r.bossWins,bossBefore:r.bossBeforeWins}))},null,2));
