// Headless check of every Shape Bench shape: counts against known values and each build's own checks.
// Run: node Tools/ShapeBench/check.js
const G=require('./geometry.js');
const want={'5':[5,10],'8':[16,32],'16':[8,24],'24':[24,96],'120':[600,1200],'600':[120,720]};
let fail=0; const ok=(c,m)=>{ console.log((c?'PASS ':'FAIL ')+m); if(!c) fail++; };
for(const k in want){ const g=G.polytope4(k); ok(g.verts.length===want[k][0]&&g.edges.length===want[k][1], `${k}-cell ${g.verts.length}/${g.edges.length} want ${want[k]}`); }
for(const [k,v] of Object.entries({tetra:[4,6],cube:[8,12],octa:[6,12],icosa:[12,30],dodeca:[20,30]})){ const g=G.platonic(k); ok(g.verts.length===v[0]&&g.edges.length===v[1], `${k} ${g.verts.length}/${g.edges.length} want ${v}`); }
for(let n=4;n<=10;n++){ const g=G.nCube(n); ok(g.verts.length===1<<n&&g.edges.length===n*(1<<(n-1)), `${n}-cube`); }
const m=G.metatron(1); ok(m.verts.length===13&&m.edges.length===78,'metatron');
const run=(name,geo)=>{ for(const c of geo.checks||[]) ok(c.ok, `${name}: ${c.text} (want ${c.want})`);
  ok(geo.verts.every(p=>p.every(Number.isFinite)), `${name}: all finite, ${geo.verts.length} nodes ${geo.edges.length} edges`); };
run('hopf', G.hopf({fibers:36,rings:3,lat:90,tilt:20}));
run('hopf tilt0', G.hopf({fibers:24,rings:2,lat:70,tilt:0}));
for(let s=0;s<G.ATTRACTORS.length;s++) run('attractor '+G.ATTRACTORS[s].name, G.attractor({sys:s,trail:2500}));
for(let k=0;k<G.BRAIDS.length;k++) run('seifert '+G.BRAIDS[k].name, G.seifert({knot:k}));
for(const foot of [0,1,2]) for(const closure of [0,.5,1]) run(`pillars foot ${foot} closure ${closure}`, G.enneperPillars({hall:3,foot,closure,flute:.1,spacing:1.6}));
for(const mode of [0,1,2]) for(const disloc of [0,1,2]) run(`escher mode ${mode} disloc ${disloc}`, G.escherSlice({mode,disloc,freq:2,dscale:4,sectors:6,twist:1}));
run('escher droste twist 0', G.escherSlice({mode:1,disloc:0,freq:2,dscale:3,sectors:5,twist:0}));
console.log(fail? fail+' FAILED':'ALL PASS');
