(function(){
 const image=new Image();image.src='../../unity/Assets/Resources/Art/Enemies/v039/frost-boss-full-motion.png';
 const frames=[[25,30,656,566,350],[729,58,574,544,970],[16,647,688,512,500],[713,608,595,560,983]];
 function index(f){return f===12||f===13||f===22||f===23?1:f===14||f===15?2:f===16||f===17?3:0;}
 function render(ctx,x,y,height,frame,time){
  if(!image.complete||!image.naturalWidth)return;
  const f=frames[index(frame)],s=height/566,bob=Math.sin(time*Math.PI*1.2)*(frame>=6&&frame<=11?2:1.2),fall=frame>=18&&frame<=21?(frame-18)*2:0;
  ctx.save();ctx.imageSmoothingEnabled=false;if(frame>=18&&frame<=21)ctx.globalAlpha*=(21-frame)/3;
  ctx.drawImage(image,f[0],f[1],f[2],f[3],x+(f[0]-f[4])*s,y-f[3]*s-10+bob+fall,f[2]*s,f[3]*s);ctx.restore();
 }
 window.FrostFullMotion={image,render};
})();
