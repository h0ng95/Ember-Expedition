// PixelFX renderer + original IGNIS shapes. Uses the same pinned checkout as generate.mjs.
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve,join,dirname} from 'node:path';
import {pathToFileURL,fileURLToPath} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const upstream=resolve(root,process.argv[2]||'.local-tools/pixelfx');
const revision='a84159688af3ca37a4be05a0d422b94451b2e24e';
if(execFileSync('git',['-C',upstream,'rev-parse','HEAD'],{encoding:'utf8'}).trim()!==revision)throw Error('Wrong PixelFX revision');
if(execFileSync('git',['-C',upstream,'status','--porcelain','--untracked-files=no'],{encoding:'utf8'}).trim())throw Error('Modified upstream');
const imp=p=>import(pathToFileURL(join(upstream,p)).href);
const {createFrame,createFx,clearFrame,composite,fit,hex,sparkle,TAU}=await imp('src/core/index.js');
const {encodePNG}=await imp('tools/lib/png.mjs');
const arrow=await imp('src/fx/projectiles/arrow.js'),fire=await imp('src/fx/fire/fireball.js');
const ice=await imp('src/fx/ice/ice-lance.js'),blunt=await imp('src/fx/impacts/blunt-hit.js');
const ramps={fire:['#663c32','#a35433','#d17a3e','#edb05f','#f2d28c'],ice:['#354d63','#5f829a','#87aabb','#b3cbd2','#e0e7dd'],holy:['#6e5336','#a37f48','#c9a863','#e0c78e','#f2e8bd'],nature:['#314c3a','#57764e','#85a169','#b4c586','#dbe2b1'],steel:['#404c54','#6c7c82','#99a7a6','#c2cabe','#ebe6cd']};
function custom(draw,loop=false){return {meta:{duration:1,loop,options:{}},draw};}
const orb=palette=>custom((p,t)=>{
 const C=fit(palette,5),r=5;
 for(let i=0;i<6;i++){const x=-8-i*5,y=Math.sin(t*TAU+i)*2;p.disc(x,y,Math.max(1,3-i*.4),C[Math.max(0,3-i%3)],.9-i*.1);}
 p.poly([[2,0],[-4,-r],[-9,0],[-4,r]],C[1]);p.poly([[2,0],[-4,-r],[-4,3]],C[3]);p.line(-4,-3,-1,0,C[4]);p.glow(-4,0,10,C[2],.15);
},true);
const hit=palette=>custom((p,t)=>{
 const C=fit(palette,5),u=t,fade=1-u;
 if(u<.45){p.disc(0,0,Math.max(1,6*(1-u/.5)),C[3]);sparkle(p,0,0,8*(1-u/.5),C[4],C[2],.15);}
 for(let i=0;i<9;i++){let a=i*TAU/9+.35,dist=5+u*(14+(i%3)*5),x=Math.cos(a)*dist,y=Math.sin(a)*dist+u*u*9,s=(i%3===0?3:2)*(1-u*.6);
   p.poly([[x-s,y],[x,y-s*1.6],[x+s,y],[x,y+s]],C[2+i%3],fade);
 }
});
const flameHit=custom((p,t)=>{
 const C=fit(ramps.fire,5),fade=1-t;
 for(let i=0;i<7;i++){let a=i*TAU/7+.4,dist=3+t*16,x=Math.cos(a)*dist,y=Math.sin(a)*dist-t*9,r=5*fade+1;
 p.disc(x,y,r,C[1]);p.disc(x-1,y-1,r*.7,C[3]);if(t<.45)p.disc(x,y,r*.35,C[4]);}
 for(let i=0;i<7;i++)p.dot((i-3)*(3+t*4),-9-t*(18+i),C[2+i%3],fade);
});
const claw=custom((p,t)=>{
 const C=fit(ramps.steel,5),fade=1-t;
 for(let n=0;n<3;n++)for(let i=0;i<16;i++){
  const u=i/15,x=-16+u*30+(n-1)*8,y=-20+u*35+(n-1)*3;
  p.line(x,y,x+2,y,C[i<8?4:2],fade);if(i<9)p.dot(x-1,y,C[1],fade);
 }
});
const shield=custom((p,t)=>{
 const C=fit(ramps.ice,5),fade=1-t;
 const outline=[[-10,-17],[10,-17],[13,-7],[9,8],[0,16],[-9,8],[-13,-7],[-10,-17]];
 p.polyline(outline,C[3],fade);p.polyline(outline.map(([x,y])=>[x*.77,y*.77]),C[1],fade);
 if(t<.45){p.line(-5,0,5,0,C[4],fade);p.line(0,-6,0,6,C[4],fade);}
 for(let i=0;i<5;i++)p.dot(12+t*(12+i*3),-14+i*7,C[2+i%3],fade);
});
const ground=custom((p,t)=>{
 const C=fit(ramps.steel,5),fade=1-t;
 for(let i=0;i<18;i++){const a=i*TAU/18,r=8+24*t,x=Math.cos(a)*r,y=Math.sin(a)*r*.38;
  p.line(x,y,x+Math.cos(a)*3,y+Math.sin(a),C[2+i%2],fade);
  if(i%3===0)p.rect(x-1,y-1-t*9,3,2,C[1],fade);
 }
});
const charge=custom((p,t)=>{
 const C=fit(ramps.steel,5);
 for(let i=0;i<8;i++){const a=i*TAU/8,r=6+20*(1-t),x=Math.cos(a)*r,y=Math.sin(a)*r*.38;p.rect(x,y,2,2,C[3],.6+.3*t);}
 p.ellipse(0,0,5+3*t,2+t,C[2],.65);
});
const stone=custom((p,t)=>{
 const C=fit(ramps.steel,5),wobble=Math.sin(t*TAU)*1;
 p.poly([[0,-3],[-5,-5+wobble],[-10,-1],[-8,4],[-2,4]],C[1]);p.poly([[0,-3],[-5,-5+wobble],[-8,-1],[-2,0]],C[3]);
 for(let i=0;i<3;i++)p.dot(-13-i*6,Math.sin(t*TAU+i)*2,C[1],.8-i*.2);
},true);
const specs=[];
function add(id,mod,w,h,pivot,n,duration,options={},origin=pivot,start=0,step=1/n){specs.push({id,mod,w,h,pivot,origin,n,duration,options:{seed:23,light:.12,...options},start,step,loop:!!mod.meta.loop});}
add('arrow',arrow,112,32,[92,16],8,.6,{palette:['#375253','#728e8a','#d8cbaa','#e6dfc6'],wind:'#9eabb3'},undefined,0,.6/8);
add('firebolt',fire,136,56,[112,28],8,1.2,{palette:['#4b2e2a','#6f382b','#9b4931','#b96634','#d38542','#e8ac5a','#edc884','#f1dfac','#fff0d0']},undefined,0,1.2/8);
add('icebolt',ice,176,64,[146,32],8,.8,{palette:['#283e50','#3b566c','#557a90','#779dac','#9cbac7','#bfced2','#dbe4df','#f1eee1']},undefined,0,.8/8);
add('holybolt',orb(ramps.holy),64,32,[48,16],8,1);
add('venombolt',orb(ramps.nature),64,32,[48,16],8,1);
add('stone',stone,64,32,[48,16],8,1);
add('blunt-hit',blunt,128,96,[64,48],8,.25,{height:24,warm:'#c6ac81',dust:'#8d918b'},[64,72],.018,.055);
add('pierce-hit',hit(ramps.steel),80,80,[40,40],8,.22,{},undefined,.02,.115);
add('ice-hit',hit(ramps.ice),80,80,[40,40],8,.28,{},undefined,.02,.115);
add('holy-hit',hit(ramps.holy),80,80,[40,40],8,.25,{},undefined,.02,.115);
add('venom-hit',hit(ramps.nature),80,80,[40,40],8,.25,{},undefined,.02,.115);
add('fire-hit',flameHit,80,96,[40,58],8,.28,{},undefined,.02,.115);
add('claw-hit',claw,80,80,[40,40],8,.20,{},undefined,.02,.115);
add('shield-block',shield,80,80,[40,40],8,.25,{},undefined,.02,.115);
add('ground-contact',ground,80,48,[40,28],8,.32,{},undefined,.02,.115);
add('ground-charge',charge,80,48,[40,28],8,1,{},undefined,.02,.115);
const dest=join(root,'unity/Assets/Resources/Art/Effects/v071');mkdirSync(dest,{recursive:true});
const manifest={upstream:'https://github.com/patch-gg/pixelfx',revision,effects:[]};
for(const s of specs){
 const inst=createFx(s.mod,{...s.options,x:s.origin[0],y:s.origin[1]}),f=createFrame(s.w,s.h),rgba=new Uint8ClampedArray(s.w*s.h*4);
 const width=s.w*4,rows=Math.ceil(s.n/4),height=s.h*rows,data=new Uint8ClampedArray(width*height*4);inst.update(s.start);
 for(let i=0;i<s.n;i++){
  if(i)inst.update(s.step);clearFrame(f);inst.draw(f);composite(f,rgba,null);let nonempty=0;
  for(let y=0;y<s.h;y++)for(let x=0;x<s.w;x++){
   const at=(y*s.w+x)*4;
   if(rgba[at+3]<12 || x<2 || y<2 || x>=s.w-2 || y>=s.h-2)rgba.fill(0,at,at+4);
   if(rgba[at+3])nonempty++;data.set(rgba.subarray(at,at+4),((Math.floor(i/4)*s.h+y)*width+(i%4)*s.w+x)*4);
  }if(!nonempty)throw Error('Empty frame '+s.id+' '+i);
 }
 const png=encodePNG(width,height,data);writeFileSync(join(dest,s.id+'.png'),png);
 manifest.effects.push({id:s.id,frameSize:[s.w,s.h],pivot:s.pivot,frames:s.n,columns:4,rows,duration:s.duration,loop:s.loop,bytes:png.length,sha256:createHash('sha256').update(png).digest('hex')});
 console.log(s.id+' '+width+'x'+height+' '+png.length+' bytes');
}
writeFileSync(join(dest,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');
for(const [from,to]of [['LICENSE','PixelFX-LICENSE.txt'],['NOTICE.md','PixelFX-NOTICE.txt']])writeFileSync(join(dest,to),readFileSync(join(upstream,from)));
