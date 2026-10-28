// Inspects generated pixels and writes atlas metadata. Never edits raster pixels.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require(process.env.SHARP_MODULE || 'C:/Users/whwog/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'..'),version=process.argv[2]||'v054';
if(!/^v\d{3}$/.test(version))throw Error('Expected version such as v055');
const doc='docs/design-v0.'+version.slice(2),art=path.join(root,'unity/Assets/Resources/Art/Units',version);
const configFile=path.join(root,doc,'review-config.json');
const config=fs.existsSync(configFile)?JSON.parse(fs.readFileSync(configFile,'utf8')):{};
const frameSelection=config.frameSelection||{'E-PR-H':{4:18,14:13},'H-05':{4:18,14:13}};
async function inspect(file){
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const w=info.width,h=info.height,m=new Uint8Array(w*h),q=new Int32Array(w*h),labels=new Int32Array(w*h),components=[];let zero=0;
 for(let i=0;i<m.length;i++){m[i]=data[i*4+3]>32?1:0;if(!data[i*4+3])zero++;}
 for(let i=0;i<m.length;i++){if(!m[i])continue;let head=0,tail=1;q[0]=i;m[i]=0;let x0=w,y0=h,x1=0,y1=0,sx=0,sy=0;
  while(head<tail){const p=q[head++],x=p%w,y=Math.floor(p/w);x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);sx+=x;sy+=y;
   for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const xx=x+dx,yy=y+dy,n=yy*w+xx;if(xx>=0&&xx<w&&yy>=0&&yy<h&&m[n]){m[n]=0;q[tail++]=n;}}}
  if(tail>=8){let label=components.length+1;for(let k=0;k<tail;k++)labels[q[k]]=label;components.push({label,owner:-1,count:tail,left:x0,top:y0,right:x1,bottom:y1,cx:sx/tail,cy:sy/tail});}
 }
 const id=path.basename(file,'.png'),issues=[],bodyThreshold=config.bodyThreshold||5000,bodies=components.filter(c=>c.count>bodyThreshold),cells=Array(22).fill(null);
 for(const b of bodies){const col=Math.max(0,Math.min(5,Math.floor((b.left+b.right)/2/(w/6)))),row=Math.max(0,Math.min(3,Math.round((b.bottom-h*.24)/(h/4)))),index=row*6+col;
  if(index>21||cells[index]){issues.push('extra/merged body '+index);continue;}b.owner=index;cells[index]={index,count:b.count,bounds:{...b},body:b};}
 for(const c of components.filter(c=>c.count<=bodyThreshold)){
  // Effects travel RIGHT. A released arrow between two cells belongs to the
  // shooter on its left, not the next frame's character just to its right.
  let best=null,d=Infinity;for(const b of cells.filter(Boolean)){let r=b.body;if(r.cx>c.cx+24||r.bottom<c.cy-2)continue;let dx=Math.max(r.left-c.cx,0,c.cx-r.right),dy=Math.max(r.top-c.cy,0,c.cy-r.bottom);let score=dx*dx+dy*dy+.05*((r.cx-c.cx)**2+(r.cy-c.cy)**2);if(score<d){d=score;best=b;}}
  const explicit=(config.effectOwners?.[id]||[]).find(o=>c.left>=o.box[0]&&c.top>=o.box[1]&&c.right<=o.box[2]&&c.bottom<=o.box[3]);
  if(explicit){best=cells[explicit.owner];d=-1;}
  if(!best||d>12000){issues.push('unassigned opaque component '+c.count+' at '+c.left+','+c.top+'..'+c.right+','+c.bottom);continue;}
  c.owner=best.index;let r=best.bounds;r.left=Math.min(r.left,c.left);r.top=Math.min(r.top,c.top);r.right=Math.max(r.right,c.right);r.bottom=Math.max(r.bottom,c.bottom);
 }
 const frames=[],trims=[],isolation=[];
 for(let index=0;index<22;index++){
  const c=cells[index];if(!c){issues.push('missing frame '+index);frames.push(null);continue;}const b=c.bounds;let l=b.left,t=b.top,r=b.right+1,bt=b.bottom+1;
  // UV subregions exclude neighboring poses without editing the source or deleting FX.
  const foreign=components.filter(o=>o.owner!==index&&o.right>=l&&o.left<r&&o.bottom>=t&&o.top<bt);
  const regions=[],active=new Map();let foreignPixels=0,ownPixels=0;
  for(let y=t;y<bt;y++){
   const banned=[];
   for(const o of foreign){if(y<o.top||y>o.bottom)continue;let a=Math.max(l,o.left),z=Math.min(r,o.right+1),own=false;
    for(let x=a;x<z;x++){const label=labels[y*w+x];if(label&&components[label-1].owner===index){own=true;break;}}
    if(!own)banned.push([a,z]);else{let start=-1;for(let x=a;x<=z;x++){const label=x<z?labels[y*w+x]:0,blocked=label&&components[label-1].owner!==index;if(blocked&&start<0)start=x;if(!blocked&&start>=0){banned.push([start,x]);start=-1;}}}
   }
   banned.sort((a,b)=>a[0]-b[0]);let x=l;const spans=[];
   for(const [a,z]of banned){if(a>x)spans.push([x,a]);x=Math.max(x,z);}if(x<r)spans.push([x,r]);
   const next=new Map();for(const [a,z]of spans){const key=a+':'+z;let rect=active.get(key);if(rect)rect.height++;else{rect={x:a-l,y:y-t,width:z-a,height:1};regions.push(rect);}next.set(key,rect);}
   active.clear();for(const [key,value]of next)active.set(key,value);
   for(let x=l;x<r;x++){const label=labels[y*w+x];if(!label)continue;const own=components[label-1].owner===index,drawn=spans.some(([a,z])=>x>=a&&x<z);if(own){ownPixels++;if(!drawn)issues.push('own pixel excluded '+index);}else{foreignPixels++;if(drawn)issues.push('foreign pixel retained '+index);}}
  }
  const foot=config.feet?.[id]?.[index]??c.body.bottom;
  if(!Number.isFinite(foot)||foot<t||foot>=bt)throw Error('Invalid sole position '+id+' frame '+index);
  const frame={x:l,y:h-bt,width:r-l,height:bt-t,pivotX:+((w*(index%6+.5)/6-l)/(r-l)).toFixed(6),pivotY:+((bt-foot)/(bt-t)).toFixed(6)};
  if(foreignPixels)frame.regions=regions;
  frames.push(frame);isolation.push({frame:index,ownPixels,foreignPixelsExcluded:foreignPixels,regions:foreignPixels?regions.length:1});
 }
 const chosen=frameSelection[id]||{};for(const [dest,source]of Object.entries(chosen))frames[dest]={...frames[source]};
 if(zero/(w*h)<.2)issues.push('insufficient transparency');
 if(bodies.length!==22)issues.push('expected22 complete body components, found '+bodies.length);
 const meta=file+'.meta';if(!fs.existsSync(meta))fs.writeFileSync(meta,fs.readFileSync(path.join(art,'../v010',id+'.png.meta'),'utf8').replace(/^guid: .+$/m,'guid: '+crypto.randomBytes(16).toString('hex')));
 return {entry:{id,resourcePath:'Art/Units/'+version+'/'+id,width:w,height:h,frames,status:version+'-reviewed-pose-selection'},report:{id,size:[w,h],transparentFraction:zero/(w*h),bodyCount:bodies.length,cells:cells.map(c=>c?{index:c.index,count:c.count,bounds:c.bounds}:null),trims,isolation,frameSelection:chosen,issues}};
}
(async()=>{const entries=[],reports=[];
for(const f of fs.readdirSync(art).filter(f=>f.endsWith('.png')).sort()){const r=await inspect(path.join(art,f));entries.push(r.entry);reports.push(r.report);console.log(r.report.id,JSON.stringify({bodies:r.report.bodyCount,idle:r.report.cells[0]?.bounds,issues:r.report.issues,trims:r.report.trims}));}
fs.mkdirSync(path.join(root,doc),{recursive:true});fs.writeFileSync(path.join(root,doc,'art-inspection.json'),JSON.stringify(reports,null,2));fs.writeFileSync(path.join(root,doc,'art-refresh-manifest.json'),JSON.stringify({entries},null,2));
if(reports.some(r=>r.issues.length))process.exitCode=1;})();
