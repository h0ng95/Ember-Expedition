// Diagnostic output, not player win rates or independent random samples.
const fs=require('fs'),path=require('path'),zlib=require('zlib');
const output=process.argv[2]; if(!output)throw Error('Pass an output directory and report paths.');
fs.mkdirSync(output,{recursive:true});
function stats(rows){
 const result={cases:rows.length};
 for(const arm of ['separate','combined','refilled']){
  const values=rows.map(r=>r[arm]);const avg=k=>+(values.reduce((n,r)=>n+r[k],0)/values.length).toFixed(4);
  result[arm]={wins:values.filter(r=>r.win).length,timeouts:values.filter(r=>r.timeout).length};
  for(const key of ['openingDps','openingCommonHealth','healing','protection','firstDeath','hpFraction'])result[arm][key]=avg(key);
 }
 result.gainedWins=rows.filter(r=>!r.separate.win&&r.refilled.win).length;
 result.lostWins=rows.filter(r=>r.separate.win&&!r.refilled.win).length;
 return result;
}
for(const file of process.argv.slice(3)){
 const raw=fs.readFileSync(file),data=JSON.parse(raw),byHybrid={};
 for(const id of [...new Set(data.rows.map(r=>r.hybrid))]){
  const rows=data.rows.filter(r=>r.hybrid===id),group={total:stats(rows)};
  for(const key of ['wave','team','branchA','branchB','layout','effects']){
   group[key]={};for(const value of [...new Set(rows.map(r=>r[key]))])group[key][value]=stats(rows.filter(r=>r[key]===value));
  }
  group.boss=stats(rows.filter(r=>r.wave%5===0));group.regular=stats(rows.filter(r=>r.wave%5!==0));byHybrid[id]=group;
 }
 const name=data.matrix;
 fs.writeFileSync(path.join(output,name+'-summary.json'),JSON.stringify({rules:data.rules,note:data.note,cases:data.cases,battles:data.battles,repeatBattles:Math.ceil(data.cases/211),assertions:data.assertions,byHybrid},null,2));
 fs.writeFileSync(path.join(output,name+'-raw.json.gz'),zlib.gzipSync(raw,{level:9}));
 console.log(name+': '+data.cases+' cases, '+data.battles+' experimental fights.');
}
