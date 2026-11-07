// Reproducible, offline export from the pinned PixelFX checkout. No game assets are uploaded.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, join, dirname } from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const upstream = resolve(root, process.argv[2] || '.local-tools/pixelfx');
const revision = 'a84159688af3ca37a4be05a0d422b94451b2e24e';
if (execFileSync('git', ['-C', upstream, 'rev-parse', 'HEAD'], {encoding:'utf8'}).trim() !== revision)
  throw Error('Use the pinned PixelFX revision: ' + revision);
if (execFileSync('git', ['-C', upstream, 'status', '--porcelain', '--untracked-files=no'], {encoding:'utf8'}).trim())
  throw Error('PixelFX source must be unmodified. Keep our adaptations in this exporter.');
const imp = p => import(pathToFileURL(join(upstream, p)).href);
const {createFrame, createFx, clearFrame, composite, fit, sparkle, TAU} = await imp('src/core/index.js');
const {encodePNG} = await imp('tools/lib/png.mjs');
const sword = await imp('src/fx/melee/sword-slash.js');
const heal = await imp('src/fx/holy/heal.js');
const ice = ['#354957','#506b79','#7896a2','#a6bdc4','#d7e0d9'];

// An open, body-independent frost cuff, drawn with PixelFX primitives. No stock actor baked in.
const frost = {
  meta: {duration:1,loop:true,options:{palette:ice}},
  draw(p,t,o) {
    const C=fit(o.palette,5);
    p.ellipse(0,0,22,5,C[1],.55);
    p.ellipse(0,-1,19,4,C[2],.55);
    const crystals=[[-19,-1,8,30],[-11,3,6,16],[13,3,7,21],[21,0,7,35]];
    for(let i=0;i<crystals.length;i++) {
      const [x,y,w,h]=crystals[i], top=[x-2,y-h];
      p.poly([[x-w/2,y],[x-w/2-2,y-h*.66],top,[x+w/2,y-h*.5],[x+w/2,y]],C[0],.85);
      p.poly([[x-w/2+1,y-1],[x-w/2-1,y-h*.64],top,[x,y-2]],C[3],.78);
      p.poly([top,[x+w/2-1,y-h*.5],[x+w/2-1,y-1],[x,y-2]],C[1],.9);
      p.line(top[0],top[1]+1,x-w/2,y-h*.65,C[4],.95);
      const pulse=Math.max(0,Math.sin(t*TAU+i*1.7))**12;
      if(pulse>.08) sparkle(p,x-2,y-h*.7,2+pulse,C[4],C[2],.18*pulse);
    }
    for(let i=0;i<5;i++) {
      const phase=(t+i*.21)%1, x=(i%2?1:-1)*(17+i%3*3);
      p.dot(x,-10-phase*34,C[3],Math.sin(phase*Math.PI)*.5);
    }
  }
};
const emptyActor={x:0,y:-38,mask:{w:0,h:0,data:new Uint8Array(0)},pixels:new Uint32Array(0)};
const specs=[
  {id:'sword-hit',mod:sword,w:144,h:128,pivot:[72,64],origin:[66,90],n:8,cols:4,
    start:.05,step:.05,duration:.24,options:{seed:23,light:.12,
      palette:['#4e3935','#86613c','#b58b50','#dfb975','#e8ce9a','#f3e4bb','#fff3d3'],
      sparks:['#f3e4bb','#d8b47b','#a0794b','#705146']}},
  {id:'healing',mod:heal,w:80,h:104,pivot:[40,92],origin:[40,92],n:16,cols:4,
    start:.08,step:.10,duration:.64,options:{seed:23,light:.15,actor:emptyActor,
      palette:['#243a32','#365a45','#4d7b58','#709e6d','#9abb86','#c0d5a1','#e0e5bf','#f4eed8']}},
  {id:'frozen',mod:frost,w:64,h:64,pivot:[32,56],origin:[32,56],n:16,cols:4,
    start:0,step:1/16,duration:1,loop:true,options:{seed:23,light:.12}},
];
const dest=join(root,'unity/Assets/Resources/Art/Effects/v070');
mkdirSync(dest,{recursive:true});
const manifest={upstream:'https://github.com/patch-gg/pixelfx',revision,effects:[]};
for(const s of specs){
  const inst=createFx(s.mod,{...s.options,x:s.origin[0],y:s.origin[1]});
  const f=createFrame(s.w,s.h), rgba=new Uint8ClampedArray(s.w*s.h*4);
  const rows=Math.ceil(s.n/s.cols),width=s.w*s.cols,height=s.h*rows;
  const sheet=new Uint8ClampedArray(width*height*4);
  inst.update(s.start);
  let occupied=0;
  for(let i=0;i<s.n;i++){
    if(i)inst.update(s.step);
    clearFrame(f);inst.draw(f);composite(f,rgba,null);
    let pixels=0;
    for(let y=0;y<s.h;y++)for(let x=0;x<s.w;x++){
      const at=(y*s.w+x)*4;
      // Remove imperceptible additive haze; leave a fully transparent two-pixel atlas gutter.
      if(rgba[at+3]<12 || x<2 || y<2 || x>=s.w-2 || y>=s.h-2)rgba.fill(0,at,at+4);
      if(rgba[at+3])pixels++;
      const to=((Math.floor(i/s.cols)*s.h+y)*width+(i%s.cols)*s.w+x)*4;
      sheet.set(rgba.subarray(at,at+4),to);
    }
    if(!pixels)throw Error(s.id+' empty frame '+i);
    occupied+=pixels;
  }
  const png=encodePNG(width,height,sheet);
  writeFileSync(join(dest,s.id+'.png'),png);
  const entry={id:s.id,frameSize:[s.w,s.h],pivot:s.pivot,frames:s.n,columns:s.cols,rows,
    duration:s.duration,loop:!!s.loop,bytes:png.length,sha256:createHash('sha256').update(png).digest('hex')};
  manifest.effects.push(entry);
  console.log(`${s.id}: ${s.n} nonempty frames, ${width}x${height}, ${png.length} bytes, ${occupied} occupied pixels`);
}
writeFileSync(join(dest,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');
writeFileSync(join(dest,'PixelFX-LICENSE.txt'),readFileSync(join(upstream,'LICENSE')));
writeFileSync(join(dest,'PixelFX-NOTICE.txt'),readFileSync(join(upstream,'NOTICE.md')));
