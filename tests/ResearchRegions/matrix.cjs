const fs = require('fs'), path = require('path'), {spawn} = require('child_process');
const [binary, output, firstText, countText, rules] = process.argv.slice(2);
const first = Number(firstText), count = Number(countText);
if (!binary || !output || !Number.isInteger(first) || first < 1 || !Number.isInteger(count) || count < 1 || count > 1000 || !['current','v045'].includes(rules)) throw Error('Invalid matrix arguments');
fs.mkdirSync(output, {recursive:true});
const jobs = [];
for (const starter of ['standard','tactical','ember','frost'])
 for (const branch of ['defense','precision','arcana'])
  for (const spend of branch === 'defense' ? ['all'] : ['early','delayed']) jobs.push({starter,branch,spend});
let next = 0, finished = 0;
async function worker() {
 while (next < jobs.length) {
  const j = jobs[next++], name = [j.starter,j.branch,j.spend].join('-');
  const args = [path.resolve('unity/Assets/Resources/Content/unit-catalog-v010.json'),path.join(output,name+'.json'),String(first),String(count),'Balanced',j.spend,'all','fresh',j.starter,'False',j.branch,rules];
  await new Promise((resolve,reject)=>{
   const child=spawn(binary,args,{windowsHide:true});let log='';
   child.stdout.on('data',d=>log+=d);child.stderr.on('data',d=>log+=d);child.on('error',reject);
   child.on('close',code=>{fs.writeFileSync(path.join(output,name+'.log'),log);code===0?resolve():reject(Error(name+' exit '+code+' '+log.slice(-1000)));});
  });
  console.log(++finished+'/'+jobs.length+' '+name);
 }
}
Promise.all([worker(),worker(),worker(),worker()]).then(()=>console.log('PASS '+(4*7*8*count)+' expedition scenarios, rules='+rules)).catch(e=>{console.error(e);process.exitCode=1;});
