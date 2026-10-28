// Publishes inspected metadata and a read-only animation review page.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..');process.chdir(root);
const read=f=>JSON.parse(fs.readFileSync(f,'utf8').replace(/^\uFEFF/,''));
const dir='docs/design-v0.54',refresh=read(dir+'/art-refresh-manifest.json'),inspection=read(dir+'/art-inspection.json');
if(inspection.some(r=>r.issues.length))throw Error('Unresolved art inspection issues');
const manifest=read('unity/Assets/Resources/Art/Units/v010/art-manifest.json');
const body=read('unity/Assets/Resources/Art/Units/v022/body-scale.json');
// Manual crown-to-sole landmarks; excludes plume, pole, banner, staff and FX.
const tops={'B-AR':56,'B-BA':98,'B-FI':62,'B-IC':70,'B-PR':80,'E-PR-G':54,'E-PR-H':76,'F-KN':73,'F-PR':72,'H-02':79,'H-05':112};
for(const e of refresh.entries){let i=manifest.entries.findIndex(x=>x.id===e.id);if(i<0)throw Error(e.id);manifest.entries[i]=e;
const b=body.entries.find(x=>x.id===e.id),report=inspection.find(x=>x.id===e.id);b.bodyTop=tops[e.id];b.footY=report.cells[0].bounds.bottom;b.bodyHeight=b.footY-b.bodyTop;b.sourceWidth=e.width;b.sourceHeight=e.height;
if(b.bodyHeight<100||b.bodyHeight>240)throw Error('Bad manual body measurement '+e.id);}
fs.writeFileSync('unity/Assets/Resources/Art/Units/v010/art-manifest.json',JSON.stringify(manifest,null,2)+'\n');
fs.writeFileSync('unity/Assets/Resources/Art/Units/v022/body-scale.json',JSON.stringify(body,null,2)+'\n');
fs.writeFileSync(dir+'/body-calibration.json',JSON.stringify({referenceBodyHeight:179.2,measurement:body.measurement,entries:body.entries.filter(x=>tops[x.id]!==undefined||x.id==='B-KN')},null,2));
const old=read(dir+'/before-manifest.json'),oldbody=read(dir+'/before-body-scale.json');
const names={'B-KN':'방패 기사 · 기준','B-AR':'수습 궁수','B-FI':'불꽃술사','B-IC':'서리술사','B-PR':'빛의 사제','B-BA':'서약 기수','F-KN':'서약 수호장','H-02':'광휘 성기사','E-PR-G':'수호 사제','E-PR-H':'치유 사제','F-PR':'여명 대사제','H-05':'성가 기수'};
const catalog=read('unity/Assets/Resources/Content/unit-catalog-v010.json');
const ids=Object.keys(names),rows=ids.map(id=>({id,name:catalog.units.find(x=>x.id===id).name+(id==='B-KN'?' · 기준':''),before:old.entries.find(x=>x.id===id),after:manifest.entries.find(x=>x.id===id),oldHeight:oldbody.entries.find(x=>x.id===id).bodyHeight,height:body.entries.find(x=>x.id===id).bodyHeight}));
fs.writeFileSync(dir+'/preview-data.js','window.ART_REVIEW='+JSON.stringify(rows)+';\n');
const hashes=refresh.entries.map(e=>({id:e.id,file:e.resourcePath+'.png',sha256:crypto.createHash('sha256').update(fs.readFileSync('unity/Assets/Resources/'+e.resourcePath+'.png')).digest('hex')}));
fs.writeFileSync(dir+'/asset-hashes.json',JSON.stringify(hashes,null,2));
console.log('Published '+refresh.entries.length+' sheets; knight reference retained; human heights recalibrated.');
