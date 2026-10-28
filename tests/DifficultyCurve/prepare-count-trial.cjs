const fs=require('fs'),path=require('path');
const root='.local-tools/v047';fs.mkdirSync(root,{recursive:true});
for(const variant of ['baseline','reinforced']) {
 const dir=root+'/'+variant;fs.mkdirSync(dir,{recursive:true});
 for(const f of fs.readdirSync('unity/Assets/Scripts/Core').filter(f=>f.endsWith('.cs')))fs.copyFileSync('unity/Assets/Scripts/Core/'+f,dir+'/'+f);
 if(variant==='baseline')continue;
 const file=dir+'/ExpeditionRun.cs';let s=fs.readFileSync(file,'utf8');
 const edits={
  6:[['"stone-infantry", 2','"stone-infantry", 3']],7:[['"stone-infantry", 2','"stone-infantry", 3']],
  11:[['AddMineWorkers(result, 2','AddMineWorkers(result, 3'],['AddMineSlingers(result, 1','AddMineSlingers(result, 2']],
  12:[['AddMineWorkers(result, 2','AddMineWorkers(result, 3']],13:[['AddMineSlingers(result, 2','AddMineSlingers(result, 3']],14:[['AddMineSlingers(result, 2','AddMineSlingers(result, 3']],
  16:[['AddFrostGuards(result, 2','AddFrostGuards(result, 3']],17:[['"frost-acolyte", 2','"frost-acolyte", 3']],19:[['"frost-acolyte", 2','"frost-acolyte", 3']],20:[['"frost-acolyte", 2','"frost-acolyte", 3']],
  21:[['AddGardenGuards(result, 2','AddGardenGuards(result, 3']],22:[['AddGardenSpitters(result, 2','AddGardenSpitters(result, 3']],24:[['AddGardenSpitters(result, 2','AddGardenSpitters(result, 3']],
  26:[['AddPalaceGuards(result, 2','AddPalaceGuards(result, 3']],27:[['"palace-arbalist", 2','"palace-arbalist", 3']],29:[['"palace-arbalist", 2','"palace-arbalist", 3']]
 };
 for(const [wave,replacements]of Object.entries(edits)){
  const start=s.indexOf('                case '+wave+':',s.indexOf('CatalogBattle CreateBattle()'));
  const next=s.indexOf('                case '+(Number(wave)+1)+':',start);
  if(start<0||next<0)throw Error('Missing stage '+wave);
  let block=s.slice(start,next);for(const[from,to]of replacements){if(!block.includes(from))throw Error('Missing '+from);block=block.replace(from,to);}s=s.slice(0,start)+block+s.slice(next);
 }
 s=s.replace('count == 1 ? 1.2f : (i == 0 ? -1.35f : 1.35f),','count == 1 ? 1.2f : count == 3 ? (i - 1) * 1.35f : (i == 0 ? -1.35f : 1.35f),');
 fs.writeFileSync(file,s);
}
console.log('Prepared isolated baseline and enemy-count trial; game sources unchanged.');
