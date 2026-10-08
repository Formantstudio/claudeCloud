const TAU = Math.PI*2, PHI = (1+Math.sqrt(5))/2;

/* ---------- helpers ---------- */
function rot(p,a,b,ang){ if(!ang) return; const c=Math.cos(ang),s=Math.sin(ang);
  const x=p[a],y=p[b]; p[a]=x*c-y*s; p[b]=x*s+y*c; }
function dedupe(list,eps=1e-4){ const out=[]; for(const v of list){ let dup=false;
  for(const w of out){ let d=0; for(let i=0;i<v.length;i++) d+=(v[i]-w[i])**2;
    if(d<eps){dup=true;break;} } if(!dup) out.push(v); } return out; }
function perms4(evenOnly){ const res=[],p=[0,1,2,3];
  (function go(k){ if(k===4){ if(!evenOnly||parity(p)===0) res.push(p.slice()); return; }
    for(let i=k;i<4;i++){ [p[k],p[i]]=[p[i],p[k]]; go(k+1); [p[k],p[i]]=[p[i],p[k]]; } })(0);
  return res; }
function parity(p){ const q=p.slice(); let s=0;
  for(let i=0;i<q.length;i++) while(q[i]!==i){ const j=q[i]; [q[i],q[j]]=[q[j],q[i]]; s++; }
  return s&1; }
/* Every arrangement under the permutations, with every sign change on all four coordinates. */
function orbit(out,perms,m){ for(const pm of perms) for(let s=0;s<16;s++)
  out.push([ m[pm[0]]*((s&1)?1:-1), m[pm[1]]*((s&2)?1:-1),
             m[pm[2]]*((s&4)?1:-1), m[pm[3]]*((s&8)?1:-1) ]); }
/* Shortest pairwise distance is the edge length of any regular polytope. */
function edgesByNearest(verts,tol){ let best=Infinity;
  for(let i=0;i<verts.length;i++) for(let j=i+1;j<verts.length;j++){
    let d=0; for(let k=0;k<verts[i].length;k++) d+=(verts[i][k]-verts[j][k])**2;
    if(d>1e-9&&d<best) best=d; }
  const lim=best*(1+tol)*(1+tol), E=[];
  for(let i=0;i<verts.length;i++) for(let j=i+1;j<verts.length;j++){
    let d=0; for(let k=0;k<verts[i].length;k++) d+=(verts[i][k]-verts[j][k])**2;
    if(d<=lim) E.push([i,j]); }
  return E; }
/* Scale and centre a 3-D vertex list into roughly the unit cube, for shapes built in their own units. */
function autoFit(geo,target=.95){ let lo=[Infinity,Infinity,Infinity],hi=[-Infinity,-Infinity,-Infinity];
  for(const p of geo.verts) for(let k=0;k<3;k++){ lo[k]=Math.min(lo[k],p[k]); hi[k]=Math.max(hi[k],p[k]); }
  const c=[0,1,2].map(k=>(lo[k]+hi[k])/2), r=Math.max(...[0,1,2].map(k=>(hi[k]-lo[k])/2))||1;
  geo.verts=geo.verts.map(p=>[0,1,2].map(k=>(p[k]-c[k])/r*target)); return geo; }
/* Weld polygon faces (arrays of points) into nodes and unique edges, and measure V - E + F and the boundary. */
function meshFromFaces(faces){
  const key=p=>p.map(x=>Math.round(x*1e5)).join(',');
  const idx=new Map(), verts=[], uses=new Map();
  let F=0;
  for(const f of faces){
    const ids=[]; for(const p of f){ const k=key(p); if(!idx.has(k)){ idx.set(k,verts.length); verts.push(p); } ids.push(idx.get(k)); }
    const loop=ids.filter((v,i)=>v!==ids[(i+1)%ids.length]);
    if(loop.length<3) continue;
    F++;
    for(let i=0;i<loop.length;i++){ const a=loop[i],b=loop[(i+1)%loop.length];
      const k=a<b?a+','+b:b+','+a; uses.set(k,(uses.get(k)||0)+1); }
  }
  const edges=[], boundary=new Map(); let open=0;
  for(const [k,n] of uses){ const [a,b]=k.split(',').map(Number); edges.push([a,b]);
    if(n===1){ open++; (boundary.get(a)||boundary.set(a,[]).get(a)).push(b); (boundary.get(b)||boundary.set(b,[]).get(b)).push(a); } }
  let loops=0; const seen=new Set();
  for(const s of boundary.keys()){ if(seen.has(s)) continue; loops++; const st=[s]; seen.add(s);
    while(st.length){ for(const m of boundary.get(st.pop())) if(!seen.has(m)){ seen.add(m); st.push(m); } } }
  return {verts,edges,dim:3,chi:verts.length-edges.length+F,faces:F,boundaryLoops:loops,openEdges:open}; }

/* ---------- n-cube ---------- */
function nCube(n){ const V=1<<n, verts=[], edges=[], c=1/Math.sqrt(n);
  for(let v=0;v<V;v++){ const p=[]; for(let d=0;d<n;d++) p.push((v&(1<<d))?c:-c); verts.push(p); }
  for(let v=0;v<V;v++) for(let d=0;d<n;d++) if(!(v&(1<<d))) edges.push([v,v|(1<<d)]);
  return {verts,edges,dim:n}; }

/* ---------- regular 4-polytopes ---------- */
function cell24(){ const out=[], pr=[[0,1],[0,2],[0,3],[1,2],[1,3],[2,3]];
  for(const [a,b] of pr) for(let s=0;s<4;s++){ const v=[0,0,0,0];
    v[a]=(s&1)?1:-1; v[b]=(s&2)?1:-1; out.push(v); } return out; }
/* 8 axis + 16 half-cube vertices (a 24-cell of radius 1) + 96 even permutations of (φ, 1, 1/φ, 0)/2.
   The (±1, ±1, 0, 0) 24-cell is a different orientation and does not fit the 96: it gave 96 edges, not 720. */
function cell600(){ const out=[];
  orbit(out,perms4(false),[1,0,0,0]); orbit(out,perms4(false),[.5,.5,.5,.5]);
  orbit(out,perms4(true),[PHI/2,.5,.5/PHI,0]);
  return dedupe(out,1e-6); }
function cell120(){ const out=[], r5=Math.sqrt(5), ip=1/PHI, ip2=ip*ip, p2=PHI*PHI,
  all=perms4(false), ev=perms4(true);
  orbit(out,all,[0,0,2,2]); orbit(out,all,[1,1,1,r5]);
  orbit(out,all,[ip2,PHI,PHI,PHI]); orbit(out,all,[ip,ip,ip,p2]);
  orbit(out,ev,[0,ip2,1,p2]); orbit(out,ev,[0,ip,PHI,r5]); orbit(out,ev,[ip,1,PHI,2]);
  return dedupe(out); }
function polytope4(kind,tol=.04){
  let v;
  if(kind==='5')   v=[[1,1,1,-1/Math.sqrt(5)],[1,-1,-1,-1/Math.sqrt(5)],
                      [-1,1,-1,-1/Math.sqrt(5)],[-1,-1,1,-1/Math.sqrt(5)],[0,0,0,4/Math.sqrt(5)]];
  else if(kind==='8')  v=nCube(4).verts;
  else if(kind==='16') v=[[1,0,0,0],[-1,0,0,0],[0,1,0,0],[0,-1,0,0],
                          [0,0,1,0],[0,0,-1,0],[0,0,0,1],[0,0,0,-1]];
  else if(kind==='24') v=cell24();
  else if(kind==='600')v=cell600();
  else                 v=cell120();
  v=v.map(p=>{ const m=Math.hypot(...p)||1; return p.map(x=>x/m); });
  return {verts:v, edges:edgesByNearest(v,tol), dim:4}; }

/* ---------- Metatron: cuboctahedron + centre, all 78 lines ---------- */
const CUBOCT=[[1,1,0],[1,-1,0],[-1,1,0],[-1,-1,0],[1,0,1],[1,0,-1],
              [-1,0,1],[-1,0,-1],[0,1,1],[0,1,-1],[0,-1,1],[0,-1,-1]];
function metatron(flat){
  const ax=[1/Math.sqrt(3),1/Math.sqrt(3),1/Math.sqrt(3)], verts=[];
  for(const q of CUBOCT){ const m=Math.hypot(...q); let p=q.map(x=>x/m);
    const dot=p[0]*ax[0]+p[1]*ax[1]+p[2]*ax[2];
    p=p.map((x,i)=>x*(1-flat)+(x-ax[i]*dot)*flat);
    /* bring the 3-fold axis to +z so the flat form reads as the classic glyph */
    verts.push(alignZ(p,ax)); }
  verts.push([0,0,0]);
  const edges=[]; for(let i=0;i<13;i++) for(let j=i+1;j<13;j++) edges.push([i,j]);
  return {verts,edges,dim:3}; }
function alignZ(p,ax){
  const to=[0,0,1], v=[ax[1]*to[2]-ax[2]*to[1], ax[2]*to[0]-ax[0]*to[2], ax[0]*to[1]-ax[1]*to[0]];
  const c=ax[0]*to[0]+ax[1]*to[1]+ax[2]*to[2], k=1/(1+c);
  return [ p[0]*(v[0]*v[0]*k+c)+p[1]*(v[0]*v[1]*k-v[2])+p[2]*(v[0]*v[2]*k+v[1]),
           p[0]*(v[1]*v[0]*k+v[2])+p[1]*(v[1]*v[1]*k+c)+p[2]*(v[1]*v[2]*k-v[0]),
           p[0]*(v[2]*v[0]*k-v[1])+p[1]*(v[2]*v[1]*k+v[0])+p[2]*(v[2]*v[2]*k+c) ]; }

/* ---------- Platonic solids ---------- */
function platonic(kind){
  let v;
  if(kind==='tetra') v=[[1,1,1],[1,-1,-1],[-1,1,-1],[-1,-1,1]];
  else if(kind==='cube') v=nCube(3).verts;
  else if(kind==='octa') v=[[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]];
  else if(kind==='icosa'){ v=[]; for(const s of [[0,1,PHI],[0,1,-PHI],[0,-1,PHI],[0,-1,-PHI]])
      for(let r=0;r<3;r++) v.push([s[r%3],s[(r+1)%3],s[(r+2)%3]]); v=dedupe(v); }
  else { v=dedupe([...nCube(3).verts.map(p=>p.map(x=>x*Math.sqrt(3))),
      ...[[0,1/PHI,PHI],[0,1/PHI,-PHI],[0,-1/PHI,PHI],[0,-1/PHI,-PHI]].flatMap(s=>
        [0,1,2].map(r=>[s[r%3],s[(r+1)%3],s[(r+2)%3]]))]); }
  v=v.map(p=>{ const m=Math.hypot(...p)||1; return p.map(x=>x/m); });
  return {verts:v, edges:edgesByNearest(v,0.05), dim:3}; }

/* ---------- parametric surfaces ---------- */
function surfaceGrid(fn,nu,nv,wrapU,wrapV){
  const verts=[],edges=[];
  for(let j=0;j<nv;j++) for(let i=0;i<nu;i++){
    const u=wrapU? i/nu : i/(nu-1), v=wrapV? j/nv : j/(nv-1);
    verts.push(fn(u,v)); }
  const at=(i,j)=>((j%nv)+nv)%nv*nu+((i%nu)+nu)%nu;
  for(let j=0;j<nv;j++) for(let i=0;i<nu;i++){
    if(wrapU||i<nu-1) edges.push([at(i,j),at(i+1,j)]);
    if(wrapV||j<nv-1) edges.push([at(i,j),at(i,j+1)]); }
  return {verts,edges,dim:3}; }

const spow=(x,e)=>Math.sign(x)*Math.pow(Math.abs(x),e);

const SURF={
  torus:(u,v)=>{ const a=u*TAU,b=v*TAU,R=.7,r=.3,rr=R+r*Math.cos(b);
    return [Math.cos(a)*rr,Math.sin(a)*rr,r*Math.sin(b)]; },
  sphere:(u,v)=>{ const a=u*TAU,t=v*Math.PI,s=Math.sin(t);
    return [Math.cos(a)*s,Math.sin(a)*s,Math.cos(t)]; },
  mobius:(u,v)=>{ const a=u*TAU,h=(v*2-1)*.3,k=.5,rr=.75+h*Math.cos(k*a);
    return [Math.cos(a)*rr,Math.sin(a)*rr,h*Math.sin(k*a)]; },
  superell:(u,v)=>{ const a=u*TAU,t=v*Math.PI-Math.PI/2,n1=.4,n2=.4;
    const ct=spow(Math.cos(t),n1),st=spow(Math.sin(t),n1);
    return [ct*spow(Math.cos(a),n2),ct*spow(Math.sin(a),n2),st]; },
  knot:(u,v)=>{ const p=2,q=3,t=u*TAU,R=.62,r2=.26,tr=.1;
    const C=t2=>{ const rr=R+r2*Math.cos(q*t2);
      return [Math.cos(p*t2)*rr,Math.sin(p*t2)*rr,r2*Math.sin(q*t2)]; };
    const c=C(t),h=1e-3,f=C(t+h),g=C(t-h);
    let T=[f[0]-g[0],f[1]-g[1],f[2]-g[2]]; const m=Math.hypot(...T); T=T.map(x=>x/m);
    const ref=Math.abs(T[2])<.9?[0,0,1]:[1,0,0];
    let N=[T[1]*ref[2]-T[2]*ref[1],T[2]*ref[0]-T[0]*ref[2],T[0]*ref[1]-T[1]*ref[0]];
    const mn=Math.hypot(...N); N=N.map(x=>x/mn);
    const B=[T[1]*N[2]-T[2]*N[1],T[2]*N[0]-T[0]*N[2],T[0]*N[1]-T[1]*N[0]];
    const ring=v*TAU;
    return [0,1,2].map(i=>c[i]+(N[i]*Math.cos(ring)+B[i]*Math.sin(ring))*tr); },
  helicoid:(u,v)=>{ const arm=(u*2-1)*.8,sw=v*TAU*2;
    return [Math.cos(sw)*arm,Math.sin(sw)*arm,.22*sw-.7]; },
  klein:(u,v)=>{ const a=u*TAU,b=v*TAU,h=a/2;
    const bulge=Math.cos(h)*Math.sin(b)-Math.sin(h)*Math.sin(2*b),rr=.62+.3*bulge;
    return [Math.cos(a)*rr,Math.sin(a)*rr,
            .3*(Math.sin(h)*Math.sin(b)+Math.cos(h)*Math.sin(2*b))]; }
};
/* Clifford torus / duocylinder: 4-D, so projected like the polytopes. */
function clifford(cellFill){ return surfaceGrid((u,v)=>{
    const a=u*TAU,b=v*TAU,inv=.70710678,k=1-cellFill*(1-v);
    return [Math.cos(a)*inv*k,Math.sin(a)*inv*k,Math.cos(b)*inv,Math.sin(b)*inv];
  },40,28,true,true); }

/* ---------- Calabi-Yau: real slice of z1^n + z2^n = 1, n^2 patches ---------- */
function cpow(re,im,e){ const r=Math.hypot(re,im); if(r<1e-7) return [0,0];
  const th=Math.atan2(im,re),rp=Math.pow(r,e);
  return [rp*Math.cos(th*e),rp*Math.sin(th*e)]; }
function calabi(n,ang,res){
  const verts=[],edges=[],patches=n*n;
  for(let k=0;k<patches;k++){
    const k1=Math.floor(k/n),k2=k%n,base=verts.length;
    for(let j=0;j<res;j++) for(let i=0;i<res;i++){
      const x=(i/(res-1))*Math.PI/2, y=(j/(res-1))*2-1;
      const ch=Math.cosh(y),sh=Math.sinh(y);
      let [z1r,z1i]=cpow(Math.cos(x)*ch,-Math.sin(x)*sh,2/n);
      let [z2r,z2i]=cpow(Math.sin(x)*ch, Math.cos(x)*sh,2/n);
      const a1=TAU*k1/n,a2=TAU*k2/n;
      let t=z1r*Math.cos(a1)-z1i*Math.sin(a1); z1i=z1r*Math.sin(a1)+z1i*Math.cos(a1); z1r=t;
      t=z2r*Math.cos(a2)-z2i*Math.sin(a2); z2i=z2r*Math.sin(a2)+z2i*Math.cos(a2); z2r=t;
      verts.push([z1r*.6,z2r*.6,(z1i*Math.cos(ang)+z2i*Math.sin(ang))*.6]); }
    for(let j=0;j<res;j++) for(let i=0;i<res;i++){
      if(i<res-1) edges.push([base+j*res+i,base+j*res+i+1]);
      if(j<res-1) edges.push([base+j*res+i,base+(j+1)*res+i]); } }
  return {verts,edges,dim:3}; }

/* ---------- fractal accordion rings ---------- */
function cantorPos(t,depth,s){ if(!depth) return t;
  const sc=t*(1<<depth); let addr=Math.min(Math.floor(sc),(1<<depth)-1);
  const frac=sc-addr; let x=0,step=1;
  for(let k=depth-1;k>=0;k--){ if(addr&(1<<k)) x+=(1-s)*step; step*=s; }
  x+=frac*step;
  let span=0,st=1; for(let k=0;k<depth;k++){ span+=(1-s)*st; st*=s; } span+=st;
  return span>0? x/span : t; }
function tri(x){ const p=((x/TAU)%1+1)%1; return 4*Math.abs(p-.5)-1; }
function kochR(th,depth,f0,lac,gain){ if(!depth) return 0;
  let sum=0,norm=0,amp=1,f=f0;
  for(let k=0;k<depth;k++){ sum+=amp*tri(f*th); norm+=amp; amp*=gain; f*=lac; }
  return norm? sum/norm : 0; }
function accordion(opt){
  const rings=38,sides=26,verts=[],edges=[],z=[];
  let total=0;
  for(let r=0;r<rings;r++){ const t=r/(rings-1);
    z.push(total); total+=Math.max(1+opt.accordion*Math.cos(TAU*opt.bellows*t),.05); }
  for(let r=0;r<rings;r++){ let t=z[r]/total;
    t=t*(1-opt.cantor)+cantorPos(t,opt.cantorDepth,.34)*opt.cantor;
    z[r]=t*1.9-0.95; }
  for(let r=0;r<rings;r++){ const base=verts.length;
    for(let s=0;s<sides;s++){ const th=s/sides*TAU;
      const rr=.52*(1+opt.fractal*.3*kochR(th+t2(r,rings)*2,opt.depth,5,2.3,.62));
      verts.push([Math.cos(th)*rr,Math.sin(th)*rr,z[r]]); }
    for(let s=0;s<sides;s++) edges.push([base+s,base+(s+1)%sides]);
    if(r<rings-1&&opt.connectors>0){ const step=Math.floor(sides/opt.connectors);
      for(let c=0;c<opt.connectors;c++) edges.push([base+c*step,base+sides+c*step]); } }
  return {verts,edges,dim:3};
  function t2(r,n){ return r/(n-1); } }

/* ---------- Hopf fibration (HopfFibration.cs) ---------- */
/* Fiber over base point b on S² at angle t: z0 = cos(θ/2)e^{i(t+φ)}, z1 = sin(θ/2)e^{it}. */
function hopfFiberPoint(b,t){ const c=Math.sqrt(Math.max(0,(1+b[2])/2)), s=Math.sqrt(Math.max(0,(1-b[2])/2)),
  phi=Math.atan2(b[1],b[0]); return [c*Math.cos(t+phi),c*Math.sin(t+phi),s*Math.cos(t),s*Math.sin(t)]; }
function hopfBases(count,rings,latDeg){ const out=[];
  rings=Math.max(1,Math.min(rings,count)); const per=Math.max(1,Math.floor(count/rings));
  const centre=Math.min(Math.max(latDeg,10),170)*Math.PI/180, spread=Math.min(centre,Math.PI-centre)*.8;
  for(let r=0;r<rings;r++){ const th=rings===1?centre:centre-spread+2*spread*r/(rings-1), tw=r*Math.PI/per;
    for(let i=0;i<per;i++){ const a=tw+TAU*i/per; out.push([Math.sin(th)*Math.cos(a),Math.sin(th)*Math.sin(a),Math.cos(th)]); } }
  return out; }
function hopfFiber(b,samples,tiltXW,tiltZW){ const pts=[];
  for(let i=0;i<samples;i++){ const p=hopfFiberPoint(b,TAU*i/samples); rot(p,0,3,tiltXW); rot(p,2,3,tiltZW);
    const d=Math.max(1-p[3],1e-3); pts.push([p[0]/d,p[1]/d,p[2]/d]); }
  return pts; }
/* Gauss linking integral of two closed polylines, midpoint rule. */
function linking(a,b){ let s=0; const n=a.length,m=b.length;
  for(let i=0;i<n;i++){ const a0=a[i],a1=a[(i+1)%n], pa=[0,1,2].map(k=>(a0[k]+a1[k])/2), da=[0,1,2].map(k=>a1[k]-a0[k]);
    for(let j=0;j<m;j++){ const b0=b[j],b1=b[(j+1)%m], r=[0,1,2].map(k=>pa[k]-(b0[k]+b1[k])/2), db=[0,1,2].map(k=>b1[k]-b0[k]);
      const len=Math.hypot(...r); if(len<1e-9) continue;
      const cx=da[1]*db[2]-da[2]*db[1], cy=da[2]*db[0]-da[0]*db[2], cz=da[0]*db[1]-da[1]*db[0];
      s+=(r[0]*cx+r[1]*cy+r[2]*cz)/(len*len*len); } }
  return s/(4*Math.PI); }
function hopf(o){ const bases=hopfBases(o.fibers,o.rings,o.lat), samples=72, verts=[], edges=[], fibers=[];
  const tx=o.tilt*Math.PI/180, tz=o.tilt*.6*Math.PI/180;
  for(const b of bases){ const f=hopfFiber(b,samples,tx,tz);
    if(f.some(p=>Math.hypot(...p)>8)) continue;
    const base=verts.length; fibers.push(f);
    f.forEach((p,i)=>{ verts.push(p); edges.push([base+i,base+(i+1)%samples]); }); }
  const geo=autoFit({verts,edges,dim:3});
  const checks=[];
  if(fibers.length>=2){ const lk=linking(hopfFiber(bases[0],160,tx,tz),hopfFiber(bases[1],160,tx,tz));
    checks.push({ok:Math.abs(Math.abs(lk)-1)<.03, text:`fibers 1–2 link ${lk.toFixed(3)}`, want:'±1'}); }
  checks.push({ok:true, text:`${fibers.length}/${bases.length} fibers drawn`, want:'pole fiber clipped'});
  geo.checks=checks; return geo; }

/* ---------- strange attractors (StrangeAttractors.cs), double-precision RK4 ---------- */
const ATTRACTORS=[
  {name:'Lorenz', p:{a:10,b:28,c:8/3}, seed:[.1,0,0], f:(x,y,z,p)=>[p.a*(y-x), x*(p.b-z)-y, x*y-p.c*z]},
  {name:'Rössler', p:{a:.2,b:.2,c:5.7}, seed:[.1,0,0], f:(x,y,z,p)=>[-y-z, x+p.a*y, p.b+z*(x-p.c)]},
  {name:'Thomas', p:{b:.208186}, seed:[.1,0,0], f:(x,y,z,p)=>[Math.sin(y)-p.b*x, Math.sin(z)-p.b*y, Math.sin(x)-p.b*z]},
  {name:'Halvorsen', p:{a:1.89}, seed:[-1.48,-1.51,2.04], f:(x,y,z,p)=>[-p.a*x-4*y-4*z-y*y, -p.a*y-4*z-4*x-z*z, -p.a*z-4*x-4*y-x*x]},
  {name:'Aizawa', p:{a:.95,b:.7,c:.6,d:3.5,e:.25,f:.1}, seed:[.1,0,0],
   f:(x,y,z,p)=>[(z-p.b)*x-p.d*y, p.d*x+(z-p.b)*y, p.c+p.a*z-z*z*z/3-(x*x+y*y)*(1+p.e*z)+p.f*z*x*x*x]}
];
function rk4(sys,s,h){ const f=(a,b,c)=>sys.f(a,b,c,sys.p);
  const k1=f(s[0],s[1],s[2]), k2=f(s[0]+h/2*k1[0],s[1]+h/2*k1[1],s[2]+h/2*k1[2]),
        k3=f(s[0]+h/2*k2[0],s[1]+h/2*k2[1],s[2]+h/2*k2[2]), k4=f(s[0]+h*k3[0],s[1]+h*k3[1],s[2]+h*k3[2]);
  return [0,1,2].map(i=>s[i]+h/6*(k1[i]+2*k2[i]+2*k3[i]+k4[i])); }
function attractor(o){ const sys=ATTRACTORS[o.sys|0], h=sys.name==='Lorenz'||sys.name==='Halvorsen'?.005:.02;
  let s=sys.seed.slice(); for(let i=0;i<3000;i++) s=rk4(sys,s,h);
  const n=o.trail|0, verts=[], edges=[];
  for(let i=0;i<n;i++){ for(let k=0;k<2;k++) s=rk4(sys,s,h); verts.push(s.slice()); if(i) edges.push([i-1,i]); }
  const geo=autoFit({verts,edges,dim:3});
  /* Convergence check: halving h must cut the 0.5-time-unit error by about 2⁴ = 16. */
  const ref=run(h/64), e1=dist(run(h),ref), e2=dist(run(h/2),ref);
  geo.checks=[{ok:e1/e2>10&&e1/e2<24, text:`RK4 error ratio ${(e1/e2).toFixed(1)}`, want:'≈16 (4th order)'}];
  return geo;
  function run(step){ let q=s.slice(); const m=Math.round(.5/step); for(let i=0;i<m;i++) q=rk4(sys,q,step); return q; }
  function dist(a,b){ return Math.hypot(a[0]-b[0],a[1]-b[1],a[2]-b[2]); } }

/* ---------- Seifert surfaces (SeifertSurface.cs) ---------- */
const BRAIDS=[
  {name:'Trefoil', n:2, w:[1,1,1]},
  {name:'Figure-eight', n:3, w:[1,-2,1,-2]},
  {name:'Cinquefoil', n:2, w:[1,1,1,1,1]},
  {name:'Torus knot (3,4)', n:3, w:[1,2,1,2,1,2,1,2]},
  {name:'Torus knot (3,5)', n:3, w:[1,2,1,2,1,2,1,2,1,2]},
  {name:'Hopf link', n:2, w:[1,1]},
  {name:'Borromean rings', n:3, w:[1,-2,1,-2,1,-2]}
];
function braidComponents(w,n){ const perm=[...Array(n).keys()];
  for(const g of w){ const a=Math.abs(g)-1; [perm[a],perm[a+1]]=[perm[a+1],perm[a]]; }
  const seen=new Array(n).fill(false); let c=0;
  for(let i=0;i<n;i++){ if(seen[i]) continue; c++; for(let j=i;!seen[j];j=perm[j]) seen[j]=true; } return c; }
function seifert(o){ const br=BRAIDS[o.knot|0], w=br.w, n=br.n, c=w.length;
  const R=1, H=.35, bandW=.45, m=3, arc=4, rings=3, rows=8, delta=bandW*Math.PI/c;
  const polar=(r,a)=>[r*Math.cos(a),r*Math.sin(a),0], lerp=(a,b,t)=>a.map((x,i)=>x+(b[i]-x)*t);
  const rim=[];
  for(let k=0;k<c;k++){ const th=TAU*k/c, a=polar(R,th-delta), b=polar(R,th+delta);
    for(let j=0;j<m;j++) rim.push(lerp(a,b,j/m));
    const next=TAU*(k+1)/c-delta; for(let j=0;j<arc;j++) rim.push(polar(R,th+delta+(next-th-delta)*j/arc)); }
  const faces=[], N=rim.length;
  for(let d=0;d<n;d++){ const z=d*H, lift=p=>[p[0],p[1],p[2]+z];
    for(let j=0;j<N;j++){ const p0=rim[j],p1=rim[(j+1)%N];
      faces.push([lift([0,0,0]),lift(p0.map(x=>x/rings)),lift(p1.map(x=>x/rings))]);
      for(let r=1;r<rings;r++){ const s0=r/rings,s1=(r+1)/rings;
        faces.push([lift(p0.map(x=>x*s0)),lift(p0.map(x=>x*s1)),lift(p1.map(x=>x*s1)),lift(p1.map(x=>x*s0))]); } } }
  /* One half-twisted band per crossing, disk i to disk i+1. */
  for(let k=0;k<c;k++){ const i=Math.abs(w[k])-1, sg=Math.sign(w[k]), th=TAU*k/c,
      a=polar(R,th-delta), b=polar(R,th+delta), mid=lerp(a,b,.5), half=[(b[0]-a[0])/2,(b[1]-a[1])/2];
    const band=(t,s)=>{ const ang=Math.PI*s*sg, ca=s>=1?-1:Math.cos(ang), sa=s>=1?0:Math.sin(ang);
      const ox=half[0]*t, oy=half[1]*t; return [mid[0]+ox*ca-oy*sa, mid[1]+ox*sa+oy*ca, (i+s)*H]; };
    for(let r=0;r<rows;r++) for(let col=0;col<m;col++){ const t0=1-2*col/m, t1=1-2*(col+1)/m, s0=r/rows, s1=(r+1)/rows;
      faces.push([band(t0,s0),band(t1,s0),band(t1,s1),band(t0,s1)]); } }
  const geo=meshFromFaces(faces), comps=braidComponents(w,n), chi=n-c, genus=(2-chi-comps)/2;
  geo.verts=geo.verts.map(p=>[p[0],p[2]-(n-1)*H/2,p[1]]); autoFit(geo);
  geo.checks=[{ok:geo.chi===chi, text:`χ ${geo.chi}`, want:`strands − crossings = ${chi}`},
              {ok:geo.boundaryLoops===comps, text:`boundary ${geo.boundaryLoops} loop${geo.boundaryLoops===1?'':'s'}`, want:`${comps} component${comps===1?'':'s'}, genus ${genus}`}];
  return geo; }

/* ---------- Enneper pillar (ManifoldSurfaces Enneper + EnneperPillar.cs) ---------- */
function enneper(u,v,domain,phase,scale){ const p=(u*2-1)*domain, q=(v*2-1)*domain;
  const re=[p-p*p*p/3+p*q*q, q-q*q*q/3+q*p*p, p*p-q*q], im=[q-p*p*q+q*q*q/3, -(p+p*p*p/3-p*q*q), 2*p*q];
  const c=Math.cos(phase), s=Math.sin(phase); return [0,1,2].map(k=>(re[k]*c-im[k]*s)*scale); }
function rectBoundary(u,hx,hz){ const s=(((u%1)+1)%1)*4-.5, k=Math.floor(s), f=s-k;
  const C=i=>[[hx,hz],[-hx,hz],[-hx,-hz],[hx,-hz]][((i%4)+4)%4], a=C(k), b=C(k+1);
  return [a[0]+(b[0]-a[0])*f, a[1]+(b[1]-a[1])*f]; }
function hexBoundary(u,ap){ const s=(((u%1)+1)%1)*6-.5, k=Math.floor(s), f=s-k, r=ap/Math.cos(Math.PI/6),
  a0=(k+.5)*TAU/6, a1=a0+TAU/6, p=[r*Math.cos(a0),r*Math.sin(a0)], q=[r*Math.cos(a1),r*Math.sin(a1)];
  return [p[0]+(q[0]-p[0])*f, p[1]+(q[1]-p[1])*f]; }
function pillarPoint(sh,u,v,phase){ const t=Math.min(Math.max(v,0),1)*2-1, th=Math.min(Math.max(u,0),1)*TAU,
  w=t*t*t*t, keep=1-w, y=sh.size*sh.halfHeight*Math.sin(t*Math.PI/2), tw=sh.twist*t;
  if(sh.footprint===0){ const r=sh.size*(sh.waist+sh.flare*w)*(1+sh.fluting*Math.cos(4*th+phase+tw));
    return [r*Math.cos(th+tw), y, r*Math.sin(th+tw)]; }
  const flute=1+sh.fluting*keep*Math.cos(4*th+phase+tw), wr=sh.size*sh.waist*flute, wx=wr*Math.cos(th), wz=wr*Math.sin(th);
  const e=sh.footprint===2?hexBoundary(u,sh.half[0]):rectBoundary(u,sh.half[0],sh.half[1]);
  const hx=wx+(e[0]-wx)*w, hz=wz+(e[1]-wz)*w, a=tw*keep, ca=Math.cos(a), sa=Math.sin(a);
  return [hx*ca-hz*sa, y, hx*sa+hz*ca]; }
function latticeCentre(fp,col,row,d){ return fp===2?[(col+((row&1)?.5:0))*d,0,row*d*Math.sqrt(3)/2]:[col*d,0,row*d]; }
function enneperPillars(o){ const size=3.8, n=o.hall|0, fp=o.foot|0, d=o.spacing*size,
  sh={size, waist:.16, halfHeight:.9, flare:2.1, fluting:o.flute, twist:25*Math.PI/180, footprint:fp, half:fp===2?[d/2,d/2]:[d/2,d/2]};
  const cols=49, rows=25, verts=[], edges=[], tilt=Math.PI/4, ct=Math.cos(tilt), st=Math.sin(tilt), rings=[];
  for(let a=0;a<n;a++) for(let b=0;b<n;b++){ const col=a-Math.floor(n/2), row=b-Math.floor(n/2), off=latticeCentre(fp,col,row,d),
      phase=(a*n+b)*15*Math.PI/180, base=verts.length, top=[];
    for(let j=0;j<rows;j++) for(let i=0;i<cols;i++){ const u=i/(cols-1), v=j/(rows-1);
      const e=enneper(u,v,2,phase,size*.18), src=[e[0], e[1]*ct-e[2]*st, e[1]*st+e[2]*ct], p=pillarPoint(sh,u,v,phase);
      const q=[0,1,2].map(k=>src[k]+(p[k]-src[k])*o.closure+off[k]); verts.push(q); if(j===rows-1) top.push(q);
      if(i<cols-1) edges.push([base+j*cols+i,base+j*cols+i+1]); if(j<rows-1) edges.push([base+j*cols+i,base+(j+1)*cols+i]); }
    rings.push({col,row,top}); }
  const geo=autoFit({verts,edges,dim:3}); geo.checks=[];
  if(n>1&&o.closure>=1&&fp>0){ /* Every ceiling point on a shared cell edge must lie on the neighbour's ceiling ring. */
    let worst=0, tested=0; const A=rings.find(r=>r.col===0&&r.row===0), B=rings.find(r=>r.col===1&&r.row===0);
    if(A&&B){
      for(const p of A.top){ if(Math.abs(p[0]-latticeCentre(fp,0,0,d)[0]-d/2)>1e-6) continue; tested++;
        let best=Infinity; for(let j=0;j<B.top.length-1;j++){ const q=B.top[j],r=B.top[j+1],dx=r[0]-q[0],dz=r[2]-q[2];
          const tt=Math.max(0,Math.min(1,((p[0]-q[0])*dx+(p[2]-q[2])*dz)/(dx*dx+dz*dz||1)));
          best=Math.min(best,Math.hypot(q[0]+dx*tt-p[0],q[1]-p[1],q[2]+dz*tt-p[2])); } worst=Math.max(worst,best); } }
    geo.checks.push({ok:tested>0&&worst<1e-6*size, text:`shared edge gap ${worst.toExponential(1)}`, want:`0 on ${tested} points`}); }
  else geo.checks.push({ok:true, text:fp===0?'Round ends overlap freely':'merging needs closure 1 and a hall', want:'—'});
  return geo; }

/* ---------- Escher slices (Implicits + EscherSpace.cs), marching squares ---------- */
function gyroid(q){ return Math.sin(q[0])*Math.cos(q[1])+Math.sin(q[1])*Math.cos(q[2])+Math.sin(q[2])*Math.cos(q[0]); }
/* EscherSpace.Map: inversion, then Droste (which carries the staircase as height += periods per turn),
   else the screw, whose core blends the plain and sheared fields so the cut never cracks. */
function mapLattice(p,o){ const P=2/o.freq; let q=p.slice();
  if(o.mode===2){ const r2=Math.max(q[0]*q[0]+q[1]*q[1]+q[2]*q[2],1e-12), R=.6; q=q.map(x=>x*R*R/r2); }
  if(o.mode===1){ const rho=Math.max(Math.hypot(q[0],q[1]),.004), turns=Math.atan2(q[1],q[0])/TAU,
      lv=Math.log(rho/.5)/Math.log(o.dscale), n=o.sectors|0;
    const pt=[P*(lv+(o.twist|0)*turns), P*n*turns, P*(n*q[2]/(TAU*rho)+(o.disloc|0)*turns)];
    return {point:pt,plain:pt,weight:1}; }
  if(!o.disloc) return {point:q,plain:q,weight:1};
  const r=Math.hypot(q[0],q[1]), sh=[q[0],q[1],q[2]+o.disloc*P*(Math.atan2(q[1],q[0])/TAU)];
  return {point:sh,plain:q,weight:r<=0?0:smooth(Math.min(r/.1,1))}; }
function smooth(t){ return t*t*(3-2*t); }
function escherField(p,o){ const k=Math.PI*o.freq, g=q=>gyroid([q[0]*k,q[1]*k,q[2]*k]), l=mapLattice(p,o);
  const v=g(l.point); return l.weight>=1?v:g(l.plain)+(v-g(l.plain))*l.weight; }
function escherSlice(o){ const N=170, verts=[], edges=[], mode=o.mode|0, opts={...o,mode};
  /* Screw: the plane through the axis (y = 0), where floors and the seam both show. Droste and inversion: across the axis. */
  const P=(x,y)=>mode===0?[x,1e-4,y]:[x,y,.05];
  const val=new Float64Array((N+1)*(N+1));
  for(let j=0;j<=N;j++) for(let i=0;i<=N;i++) val[j*(N+1)+i]=escherField(P(-1+2*i/N,-1+2*j/N),opts);
  const at=(i,j)=>val[j*(N+1)+i], pos=(i,j)=>[-1+2*i/N,-1+2*j/N];
  const cross=(i0,j0,i1,j1)=>{ const a=at(i0,j0),b=at(i1,j1),t=a/(a-b),p=pos(i0,j0),q=pos(i1,j1); return [p[0]+(q[0]-p[0])*t,p[1]+(q[1]-p[1])*t,0]; };
  const seg=(a,b)=>{ const n=verts.length; verts.push(a,b); edges.push([n,n+1]); };
  for(let j=0;j<N;j++) for(let i=0;i<N;i++){
    const c=(at(i,j)<0?1:0)|(at(i+1,j)<0?2:0)|(at(i+1,j+1)<0?4:0)|(at(i,j+1)<0?8:0); if(c===0||c===15) continue;
    const e=[cross(i,j,i+1,j),cross(i+1,j,i+1,j+1),cross(i,j+1,i+1,j+1),cross(i,j,i,j+1)]; /* bottom right top left */
    const centre=(at(i,j)+at(i+1,j)+at(i+1,j+1)+at(i,j+1))/4<0;
    switch(c){ case 1: case 14: seg(e[3],e[0]); break; case 2: case 13: seg(e[0],e[1]); break;
      case 3: case 12: seg(e[3],e[1]); break; case 4: case 11: seg(e[1],e[2]); break;
      case 6: case 9: seg(e[0],e[2]); break; case 7: case 8: seg(e[3],e[2]); break;
      case 5: if(centre){ seg(e[3],e[2]); seg(e[0],e[1]); } else { seg(e[3],e[0]); seg(e[1],e[2]); } break;
      case 10: if(centre){ seg(e[3],e[0]); seg(e[1],e[2]); } else { seg(e[3],e[2]); seg(e[0],e[1]); } break; } }
  const geo={verts,edges,dim:3,flat:true};
  /* The seam check the C# tests make: the field across the atan2 cut must not jump. */
  let worst=0; for(let i=0;i<60;i++){ const r=.01+i*.015, z=-.8+i*.027;
    worst=Math.max(worst,Math.abs(escherField([-r,1e-6,z],opts)-escherField([-r,-1e-6,z],opts))); }
  const scale=mode===1?(()=>{ let d=0; for(let i=0;i<30;i++){ const p=[.2+.01*i,-.1+.007*i,.03*Math.sin(i)];
      d=Math.max(d,Math.abs(escherField(p,opts)-escherField(p.map(x=>x*o.dscale),opts))); } return d; })():null;
  geo.checks=[{ok:worst<5e-3, text:`seam jump ${worst.toExponential(1)}`, want:'≈0 for whole numbers'}];
  if(scale!==null) geo.checks.push({ok:scale<5e-3, text:`scale-invariance error ${scale.toExponential(1)}`, want:`f(${o.dscale}·p) = f(p)`});
  return geo; }

if(typeof module!=='undefined') module.exports={TAU,PHI,nCube,polytope4,platonic,metatron,surfaceGrid,SURF,clifford,calabi,accordion,
  hopf,attractor,ATTRACTORS,seifert,BRAIDS,enneperPillars,escherSlice,escherField,meshFromFaces,linking,hopfFiber,hopfBases};
