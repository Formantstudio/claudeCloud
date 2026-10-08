# GeometricFractalPlan — exotic manifolds for the curved wireframe chamber

Plan only. Nothing here is built yet. The goal is a family of **high-complexity mathematical
surfaces and curve bundles** rendered the way the Curved Geometry Chamber is rendered: a
procedurally generated triangle mesh with GPU barycentric wireframe shading, bending with the
stage through Curved World, tinted by `WorldGridScan`.

A second backend — the Dekeract-style mesh-particle swarm — is also planned, for the shapes a
triangle mesh cannot carry (§2.2).

Read `Assets/GeometryFXParticles/Combo/README.md` for how the particle backends relate.

---

## 1. What already exists

### 1.1 Primary template — `CurvedGeometryChamber` (the thing to generalise)

`Assets/GeometryFXParticles/Combo/CurvedGeometryChamber.cs` plus
`Combo/CurvedChamber.shader`. Being precise about what it does, because nearly all of it is
reusable as-is:

| Mechanism | Where | Reuse |
| --- | --- | --- |
| Generates an **unwelded** triangle mesh: 6 unique vertices per quad, `indices[k] = k`, nothing shared | `Build()` lines 79–89 | as-is — unwelding is what makes the bary wire possible |
| Barycentric coordinate per corner written to **UV1**: `k%3==0 ? right : k%3==1 ? up : forward` | `Build()` line 87 | as-is, with one caveat (§2.1) |
| Wire from screen-space derivatives: `smoothstep(0, fwidth(bary)*1.25, bary)`, `wire = 1 − min(x,min(y,z))` | `CurvedChamber.shader` lines 28–29 | as-is — constant-width edges at any distance |
| `WorldGridHighlighting_float(positionWS, _Time.y, scan)` recolouring cyan → amber | shader lines 30–31 | as-is |
| Curved World bend ID 1 via `CURVEDWORLD_TRANSFORM_VERTEX`, keywords switched per bend type | shader lines 16–25, `Update()` lines 54–62 | as-is |
| `mesh.bounds = Bounds(zero, one*200)` because Curved World displaces on the GPU, outside straight-mesh bounds | `Build()` line 91 | **mandatory** — without it the mesh culls when bent |
| Material cloned per instance into `ownedMaterials`, registered with `bridge.AddSharedBend()`, restored and disposed in `Release()` | `Build()` / `Release()` | as-is |
| Edit-mode preview `CurvedWorldController` (bendID 1, manualUpdate) created only when not playing or when there is no bridge; the shared bridge drives at runtime | `Build()` lines 73–78 | as-is |
| `_WireOpacity` driven from `worldGridScan.guideLinesOn` | `Update()` line 64 | as-is |
| Rebuild only when a structural field actually changed | `Update()` line 42 | as-is |
| `[ExecuteAlways]`, `HideFlags.HideAndDontSave` on everything generated | class attribute, `Build()` | as-is |

**The gap is one method.** Every shape-specific thing in the chamber lives in
`Point(int side, int ring)` (lines 109–114) — 5 lines returning a cylinder point. Everything else
is backend. That single method is the seam.

### 1.2 Secondary template — `DekeractTetraSwarm` (still wanted, see §2.2)

`Combo/DekeractTetraSwarm.cs`: one `ParticleSystem` in `ParticleSystemRenderMode.Mesh` with a
generated tetrahedron, driven by `SetParticles` each frame. Node particles at vertices, edge
particles sliding along edges with a travelling phase, `scatter` / `assembleOnPlay`,
`1-exp(-regroupSpeed·dt)` smoothing, cyan→gold palette, per-particle tumble.

Uses `Assets/polyhedronGenerator/prefabs/urp/wireframeParticle.prefab` and
`Combo/Dekeract_Particles.mat` (both confirmed present). **Proven budget: 11,264 mesh
particles** (1024 vertices + 5120 edges × 2).

---

## 2. Architecture

### 2.1 Tier A — `CurvedManifoldChamber` (mesh + barycentric wire). The main path.

Generalise the chamber by splitting the parameterisation out behind a small interface, keeping
every mechanism in §1.1:

```
interface IManifoldSurface
{
    int  UResolution { get; }   // includes the wrap seam if WrapU
    int  VResolution { get; }
    bool WrapU { get; }
    bool WrapV { get; }
    int  PatchCount { get; }    // 1 for most; 25 for the Calabi-Yau quintic
    Vector3 Point(int patch, float u, float v, float time, float morph);
}
```

`CurvedManifoldChamber` owns the mesh build, the unwelding, the bary write, the bounds override,
the material clone, the bridge registration, the bend keywords and the `_WireOpacity` drive. Each
shape is one small `[Serializable]` class chosen by a dropdown, with its own parameters shown
beneath it — the same shape as `SdfParticleSettings`: no ScriptableObjects, variants living on the
GameObject you can see.

**Why this backend is the right one for these shapes**

- **Resolution is cheap.** Triangles, not particles. A 96×96 grid is 9,216 quads where the shipping
  chamber is 12×48 = 576. The CPU particle swarm could never carry these densities; a mesh does it
  without thinking.
- **Self-intersection is free.** The shader is **opaque with `Cull Off`**. Boy's surface, the Roman
  surface and the Clifford torus all pass through themselves, and opaque depth-tested geometry
  resolves that correctly. Transparent particles would need sorting and would not.
- **The bend comes along.** These surfaces inherit Curved World bend ID 1, so they warp with the
  rest of the stage exactly like the chamber does.

**Four gotchas this backend introduces**

1. **32-bit indices.** Unwelding costs 6 vertices per quad. The chamber gets away with 16-bit
   because 12×48×6 = 3,456. A 96×96 grid is 55,296 and the Calabi–Yau quintic is far past that, so
   `mesh.indexFormat = IndexFormat.UInt32` is required. Forgetting it silently wraps the mesh.
2. **The quad diagonal is drawn.** With bary `(1,0,0) (0,1,0) (0,0,1)` per triangle, the wire
   appears wherever *any* component nears zero — which includes the diagonal splitting each quad.
   The chamber accepts this. For a clean quad lattice, set the bary component opposite the shared
   diagonal to a large value so it never triggers. This is a look decision, not a bug — decide it
   per shape (§5).
3. **Vertex budget per frame.** `Point()` is called `patches × uRes × vRes × 6` times on rebuild.
   Fine when rebuilds are structural only (the chamber already gates this). Animated shapes that
   change every frame need either a `Mesh.MarkDynamic()` + `SetVertices` path or to move the
   animation into the vertex shader.
4. **Degenerate poles.** Shapes with a pole (a disk centre, a sphere pole) produce zero-area
   triangles there. `fwidth(bary)` on a degenerate triangle gives garbage and flickers. Collapse
   the pole ring to a triangle fan or nudge the radius off zero.

### 2.2 Tier B — `WireManifoldSwarm` (particles). For curve bundles.

A triangle mesh is the wrong object for a **bundle of curves**. The Hopf fibration is a family of
circles, and the attractors are trajectories — neither is a surface. These go to the particle
backend, generalising `DekeractTetraSwarm` the same way (its `Render()` currently inlines the
10-cube):

```
interface IManifoldCurves
{
    int  CurveCount { get; }
    int  SamplesPerCurve { get; }
    Vector3 Sample(int curve, float t, float time, float morph);
}
```

This keeps `scatter`, `assembleOnPlay`, the `follow` smoothing, the palette and the tumble. It is
also the backend where the Dekeract itself becomes just one more source.

The alternative for Hopf — sweeping a tube along each fiber and rendering it as Tier A mesh —
is a real option (§3.2) and gives the bary wire on the fibers. Worth trying both; the swarm is
cheaper to build first.

### 2.3 Tier C — sparkle overlay (optional, later)

To put the geometry combo's sparkle on top of a Tier A surface, bake the parameterisation into a
**position map** (a `RenderTexture` where pixel `(u,v)` holds `xyz`) and feed it to a VFX Graph as
an attribute map. `FractalSdfParticles.vfx:1789` already has an `attributeMap` slot, and
`Assets/PsychedelicLab/CameraFX/Akvfx/` uses this pattern — so it is in the project twice.

**The parametric shapes in §3.1–3.6 and §3.8 must not go through the SDF path.**
`SdfSparkleParticles.vfx` conforms particles to a *signed* distance field, and those are
immersions with self-intersections and no inside/outside, so no signed distance exists.

**The implicit shapes in §3.9–3.11 are the opposite case** — a gyroid genuinely divides space into
two labyrinths, and the Mandelbulb has a distance estimator. Those *do* have a signed field and
the SDF particle backend works on them directly.

### 2.4 Tier D — implicit surfaces (escape-time and triply periodic)

The Mandelbulb and the gyroid family are not parameterised at all. They are **fields**: a function
`f(x,y,z)` whose zero crossing (or escape boundary) is the surface. Two ways to render them, and
this project already has the parts for both.

**D1 — field → mesh, via marching cubes.** `GitRepoKeijiro/ComputeMarchingCubes/` has the whole
pipeline: `MarchingCubes.compute`, `MeshBuilder.cs`, `TriangleTable.cs`, and
`NoiseField/NoiseFieldGenerator.compute` as a worked example of a compute field driving it. Point
it at a gyroid or Mandelbulb field and it emits a real `Mesh` — **which then takes the chamber's
barycentric wire, the Curved World bend and `WorldGridScan` like any other Tier A surface.** This
is the bridge that puts fractals into the chamber look rather than off in their own screen effect.

Caveat: marching cubes emits an indexed soup with no UVs and no unwelded bary coordinates. A
post-pass has to unweld it and write UV1 — doable (the triangles are already separate in the
buffer) but it is real work, and world-space triplanar UVs are the only sane choice for UV0 since
there is no natural parameterisation.

Note `ComputeMarchingCubes` lives in `GitRepoKeijiro/`, not `Assets/`. It is a reference to adapt,
not an imported package.

**D2 — raymarched, for interiors.** "Mandelbulb rooms" means the camera is *inside* the fractal,
and no mesh can do that well. That is a distance-estimator raymarch in a box-bounded shader or a
URP fullscreen pass.

The project is already partway there: **`Assets/PsychedelicLab/Shaders/WorldGridScan.hlsl:131`
has `GridScanMandelbulb(float3 c, float power, int maxIter)`**, an escape-time Mandelbulb with a
12-iteration bounded loop, and `WorldGridScan` already carries `fractalPower`, `fractalScale` and
`fractalIterations` fields that the scene sets to 8 / 0.12 / 8. So the Mandelbulb is in the
project today as a *scan pattern*. Promoting it to a room means converting escape-time to a
distance estimate (the standard `0.5·log(r)·r/dr` running-derivative form) and marching it.

### 2.5 Which shape goes where

| Shape | Backend | Why |
| --- | --- | --- |
| **Basics** (torus, Möbius, duocylinder, sphere, superellipsoid, torus-knot tube, helicoid, Klein bottle) | **A** | all grids on the unit square — §3.0, built first |
| Calabi–Yau quintic | A | 25 patches of grid; needs mesh density |
| Clifford torus | A | the duocylinder's ridge; falls out of §3.0 |
| Boy's surface | A | grid on a disk |
| Roman surface | A | grid on a sphere |
| Torus-knot Möbius ribbons | A | swept ribbon is naturally a quad grid |
| **Gyroid and the TPMS family** | **D1**, and the SDF particle path | implicit, genuinely signed — §3.9 |
| **Mandelbulb as a mesh** | **D1** | implicit, distance-estimated — §3.10 |
| **Mandelbulb rooms** | **D2** | camera is inside it; no mesh works — §3.10 |
| **Seifert surfaces** | A | twisted bands; reuses the §3.5 ribbon sweep — §3.11 |
| **Mandelbrot / Julia imagery** | texture | a field baked to a RenderTexture, not geometry — §3.12 |
| Hopf fibration | B, or A via swept tubes | curve bundle |
| Attractors | B | trajectories, not a parameterised surface |
| Penrose steps | neither | camera trick (§3.4) |

---

## 3. The shapes, ranked by implementation complexity

§3.0 is the basic set and gets built first. §3.1 onward is ranked hardest-first.

### 3.0 The basic set — build these first. Tier A.

All of these are grids on the unit square `(u,v) ∈ [0,1]²`, which is the thing that makes them a
single batch of work: one domain, one topology helper, and **any pair of them morphs by a straight
`Lerp`** because the grids correspond vertex for vertex. That answers the §5 question about
morphable pairs for this family — within §3.0 every pair is fair game.

| Shape | Parameterisation (`a = 2πu`, `b = 2πv`) |
| --- | --- |
| Cylinder | the existing chamber shape — kept as source #0 for the identity check |
| Torus | `((R + r cos b) cos a, (R + r cos b) sin a, r sin b)` |
| Möbius strip | `((R + h cos(k a)) cos a, (R + h cos(k a)) sin a, h sin(k a))`, `h = w(2v−1)`, `k = halfTwists/2` |
| Sphere | standard spherical, `θ = πv`, `φ = a` |
| Superellipsoid | `(cos^{n₁}θ · cos^{n₂}φ, cos^{n₁}θ · sin^{n₂}φ, sin^{n₁}θ)` with signed powers — rounded cubes through octahedra through stars |
| Torus-knot tube `(p,q)` | circle of radius `r` swept along `c(t) = ((R + r₂cos(qt))cos(pt), (R + r₂cos(qt))sin(pt), r₂sin(qt))` |
| Helicoid | `(s cos b, s sin b, c·b)`, `s = 2u−1` — a minimal surface, very legible in wire |
| Klein bottle | figure-8 immersion: `((R + cos(a/2)sin b − sin(a/2)sin 2b)cos a, …sin a, sin(a/2)sin b + cos(a/2)sin 2b)` |
| **Duocylinder** | 4-D, see below |

**Duocylinder.** The genuinely 4-D one, and worth being precise about: it is the product of two
disks, `D² × D²`. Its boundary is two solid tori glued along a shared **ridge**, and that ridge is
exactly the Clifford torus:

```
ridge  : (cos a, sin a, cos b, sin b) / √2
cell 1 : (s·cos a, s·sin a, cos b, sin b) / √2     s ∈ [0,1]
cell 2 : (cos a, sin a, s·cos b, s·sin b) / √2
```

So **the Clifford torus (§3.6) is the duocylinder's ridge** — one source with a `cell` selector and
a `fill` parameter covers both, and §3.6's projection/rotation table applies unchanged. Needs the
shared `Rotor4` helper and a 4-D → 3-D projection mode (stereographic / perspective / orthographic).

Superellipsoid note: the signed power `sign(x)·|x|^n` is required. A plain `pow` on a negative base
returns NaN and will blow the mesh out to the horizon.

### 3.1 Calabi–Yau quintic — hardest, highest payoff. Tier A.

The iconic visualisation (Hanson) is a real 2-D slice of the **Fermat quintic** `z₁ⁿ + z₂ⁿ = 1` in
`C²`, drawn as `n²` patches. For `n = 5` that is **25 patches** — which is what `PatchCount` on
`IManifoldSurface` exists for.

Patch `(k₁, k₂)`, with `k₁, k₂ ∈ {0 … n-1}`:

```
z₁ = exp(2πi·k₁/n) · (cos(x + iy))^(2/n)
z₂ = exp(2πi·k₂/n) · (sin(x + iy))^(2/n)
x ∈ [0, π/2],  y ∈ [-1, 1]
```

Project `C²` (4 real dimensions) to 3-D with an animatable angle `α`:

```
X = Re(z₁)
Y = Re(z₂)
Z = Im(z₁)·cos α + Im(z₂)·sin α
```

`α` is the projection control and maps onto the chamber's existing animated-parameter idiom —
sweeping it is the money shot.

**Why it is hard**

- Complex fractional power needs the polar form: `w^(2/n) = |w|^(2/n) · exp(i·(2/n)·arg w)`.
  A naive `pow` tears along branch cuts at patch seams.
- Patches must share seam vertices, or the wireframe shows gaps where lobes meet. Unwelding makes
  this a bookkeeping job: generate the shared grid first, then unweld per triangle.
- 25 patches × 32×16 = 12,800 quads = **76,800 unwelded vertices**. Comfortable as a mesh, and
  squarely past the 16-bit index limit (§2.1 gotcha 1).
- Expose `n`. `n = 3` and `n = 4` are far cheaper and still read as exotic — good for finding the
  look before paying for `n = 5`.

### 3.2 Hopf fibration — hard, highest payoff per element. Tier B (or A).

The map `S³ → S²`. Every point of `S²` lifts to a great circle in `S³`; project those circles to
`R³` and they become interlocking Villarceau circles on nested tori.

Fiber over `p = (sin θ cos φ, sin θ sin φ, cos θ) ∈ S²`, for `ψ ∈ [0, 2π)`:

```
q(ψ) = ( cos(θ/2)·cos ψ,
         cos(θ/2)·sin ψ,
         sin(θ/2)·cos(ψ+φ),
         sin(θ/2)·sin(ψ+φ) )   ∈ R⁴
```

then stereographic-project `R⁴ → R³`.

**Why it is hard**

- The fiber through the stereographic projection pole maps to infinity. Must be culled, the pole
  nudged off-axis, or radii clamped — otherwise one ring explodes across the screen.
- Base-point choice is the whole look. Offer latitude rings on `S²` (→ nested tori, the classic
  picture) and a Fibonacci sphere (→ an even, denser weave).
- **Tier B budget:** 96 fibers × 96 samples = 9,216, inside the proven 11,264.
- **Tier A option:** sweep a small tube along each fiber. Then the fibers get the bary wire and the
  bend, which matches the chamber look much better — but it needs a frame along the curve, with the
  same Frenet caveat as §3.5. A circle has constant non-zero curvature, so Frenet is actually safe
  here; it is the knot in §3.5 that is not.

### 3.3 Boy's surface — hard. Tier A.

An immersion of `RP²` in `R³` with **no singularities**, unlike the Roman surface.
Bryant–Kusner, for complex `w` with `|w| ≤ 1`:

```
den = w⁶ + √5·w³ − 1
g₁  = −3/2 · Im( w(1 − w⁴) / den )
g₂  = −3/2 · Re( w(1 + w⁴) / den )
g₃  =        Im( (1 + w⁶) / den ) − 1/2
g   = g₁² + g₂² + g₃²
point = (g₁/g, g₂/g, g₃/g)
```

**Why it is hard**

- `den` has **zeros inside the domain**: `w³ = (3−√5)/2 ≈ 0.381966`, so `|w| ≈ 0.7257`. The map is
  still continuous there (all three `gᵢ → ∞`, so `gᵢ/g → 0`), but it needs an epsilon guard or it
  emits NaNs — which in a mesh means exploded triangles stretching to the horizon.
- Domain is the unit **disk**, so topology is polar: radial × circumferential, `WrapU` true,
  `WrapV` false. The `r = 0` centre is a degenerate pole — §2.1 gotcha 4 applies, collapse it to a
  fan.
- The `r = 1` boundary double-covers (the `RP²` identification). The grid has to wrap antipodally
  or the seam shows as a visible crease.

### 3.4 Penrose steps — hard, and the odd one out. Neither backend.

**This is not a shape, it is a camera trick.** The impossible staircase only closes under one
specific projection, so it cannot be a parameterised surface and does not fit
`IManifoldSurface`.

Honest options:

1. **View-space shear** — build a genuine 4-flight loop of steps, then shear vertices in *view*
   space so the loop closes for the current camera. Camera-coupled; works from one angle.
2. **Hidden cut** — build a real helical staircase and hide the discontinuity behind an occluder.
   Works in motion, fails if the camera orbits past the cut.

Either way it **requires a locked orthographic shot** and belongs in a scripted sting, not in a
free-orbit chamber. Flagged now so it is not discovered late.

**Third option, added 2026-10-05:** `GitRepoKeijiro/Portals` (SebLague) does recursive portal
rendering with travellers crossing between views. A staircase whose top flight *is* a portal back
to the bottom closes the loop honestly — it works from any angle and under perspective, which
neither of the options above manage. That is the better route if the shot needs to move, at the
cost of a render-texture pass per portal.

### 3.4b Non-Euclidean space — new capability, worth folding in

Three repos were added to the palette on 2026-10-05 specifically for this (see `CLAUDE.md` for the
table): `HyperEngine` (HackerPoet, the Unity backend behind *Hyperbolica*, MIT, hyperbolic **and**
spherical, with a bundled PDF overview), `noneuclideanunity` (mmagdics, elliptic/hyperbolic done
entirely in the **vertex shader**), and `Portals` (SebLague).

Why this matters to the shapes above: `noneuclideanunity`'s approach — curved-space transform in
the vertex shader, leaving vertex buffers and standard matrices alone — is **structurally the same
move Curved World already makes in these chambers**, and `CurvedChamber.shader` /
`TunnelSparkleWire.shader` already run a vertex-stage transform via `CURVEDWORLD_TRANSFORM_VERTEX`.
So hyperbolic or elliptic rendering could slot into the same stage rather than needing a separate
pipeline. A hyperbolic gyroid or a Clifford torus in elliptic space is then a shader keyword, not
a new backend.

All three are **built-in-pipeline era, not URP**, so the shaders need porting — the same situation
as `Skinner`. `HyperEngine` is the only one of the three with a real API surface to call; the other
two are techniques to read and reimplement. Nothing here is planned yet beyond noting that the
capability now exists and where it would attach.

### 3.5 Torus-knot Möbius ribbons — medium. Tier A.

A Möbius band:

```
x = (R + v·cos(u/2))·cos u
y = (R + v·cos(u/2))·sin u
z =      v·sin(u/2)
u ∈ [0, 2π),  v ∈ [−w, w]
```

Replace the `u/2` half-twist with `(q/p)·u` for `(p,q)` twisted bands — `1/2` is Möbius, `1` is a
full-twist annulus. For "torus ring" variants, sweep the ribbon along a `(p,q)` torus-knot
centreline:

```
c(t) = ( (R + r·cos(q t))·cos(p t),
         (R + r·cos(q t))·sin(p t),
          r·sin(q t) )
```

This is the most natural Tier A shape of the set: a swept ribbon *is* a quad grid, and `WrapU`
true / `WrapV` false with an odd number of half-twists gives the Möbius seam for free.

**The one real difficulty:** sweeping needs a frame along the curve, and the **Frenet frame blows
up wherever curvature vanishes** — the ribbon flips inside out. Use a rotation-minimising frame
(double-reflection method), not Frenet. This is the thing that eats an afternoon if unplanned.

Also cheap and visually busy: `m` Möbius strips arranged around a shared large torus, each with
its own phase.

### 3.6 Clifford torus and 4-D projections — medium-easy. Tier A.

The flat torus in `S³ ⊂ R⁴`:

```
(x, y, z, w) = (cos u, sin u, cos v, sin v) / √2
```

Trivial to generate. **All the interest is in the projection family**, which is the "various
projections" ask:

| Projection | Formula | Look |
| --- | --- | --- |
| Stereographic from `(0,0,0,1)` | `(x,y,z)/(1−w)` | the classic donut that turns inside-out |
| Perspective with `w`-distance | `(x,y,z)/(d−w)` | `d` controls how violent the inversion is |
| Orthographic drop-`w` | `(x,y,z)` | degenerate, useful as a morph endpoint |

Crossed with the 4-D rotation type:

- **Simple** — one plane (`xy` or `zw`).
- **Double / isoclinic** — two orthogonal planes at once (`xy` + `zw`, equal rates). This produces
  the famous inside-out turn.
- **Mixed planes** (`xz`, `yw`) — reads as the torus tumbling through itself.

`DekeractTetraSwarm` already builds 2-plane rotations as a sequence (`Render()` lines 87–95). Lift
that into a shared `Rotor4` helper used by the Clifford torus, the Hopf fibration and Calabi–Yau.

**Unification worth knowing:** the Clifford torus *is* the Hopf preimage of a latitude circle on
`S²`. §3.2 and §3.6 are one piece of machinery with two front-ends. Build Hopf and the Clifford
torus is nearly free.

### 3.7 Cyclically symmetric attractors — easy math, high payoff. Tier B.

Not manifolds — **trajectories**.

| Attractor | System | Note |
| --- | --- | --- |
| Thomas cyclically symmetric | `ẋ = sin y − b x`, `ẏ = sin z − b y`, `ż = sin x − b z`, `b ≈ 0.208186` | the genuinely 3-fold symmetric one |
| Halvorsen | `ẋ = −a x − 4y − 4z − y²`, cyclic in `(x,y,z)`, `a ≈ 1.89` | also cyclically symmetric |
| Classic Lorenz | `ẋ = σ(y−x)`, `ẏ = x(ρ−z) − y`, `ż = xy − βz`, `σ=10, ρ=28, β=8/3` | the iconic butterfly |

**Best fit:** do not keep trail history. Give each of ~8,192 particles its own position and step
the ODE with RK4 every frame. The cloud shimmers and the structure emerges from density — and it
matches the swarm's existing per-particle idiom exactly.

Gotchas: fixed small `dt` (chaotic systems diverge with variable `dt`, so accumulate and take
fixed sub-steps rather than feeding `Time.deltaTime` straight in); respawn particles that escape a
bounds check; the classic Lorenz needs a ~0.01 scale to sit in the same radius as the others.

### 3.8 Roman surface — easiest, and the right morph partner. Tier A.

Steiner's Roman surface is the image of the unit sphere under:

```
(X, Y, Z) ↦ r·(Y·Z, X·Z, X·Y)
```

That is the whole parameterisation. Antipodal points collapse, so it is `RP²` — **the same
topological object as Boy's surface**, with 3 double lines and 6 pinch points where Boy's is
clean.

**So Roman ↔ Boy's is a morph pair.** Both are grids on the same domain with corresponding
vertices, so the morph is a straight `Lerp` — and one slider crossfading a singular immersion into
a smooth one is a better shot than either alone. On Tier A this is especially good: the bary wire
makes the double lines and pinch points legible as they resolve.

### 3.9 Gyroid and the triply periodic minimal surfaces — Tier D1 + SDF. Medium.

The gyroid is an implicit surface with a famously compact approximation:

```
sin x · cos y + sin y · cos z + sin z · cos x = 0
```

The whole TPMS family works the same way, and all are worth having since they are one `switch`:

| Surface | Implicit form |
| --- | --- |
| Gyroid | `sin x cos y + sin y cos z + sin z cos x = 0` |
| Schwarz P | `cos x + cos y + cos z = 0` |
| Schwarz D | `sin x sin y sin z + sin x cos y cos z + cos x sin y cos z + cos x cos y sin z = 0` |
| Neovius | `3(cos x + cos y + cos z) + 4 cos x cos y cos z = 0` |
| Lidinoid | `sin2x cos y sin z + sin2y cos z sin x + sin2z cos x sin y − cos2x cos2y − cos2y cos2z − cos2z cos2x = 0` |

**Why this one is the best value in the whole document**

- It is **triply periodic**, so it tiles. "Rooms" and endless corridors come for free: scale the
  domain and the camera can fly through it indefinitely without a seam.
- Unlike every parametric shape above, it is **genuinely signed** — the gyroid divides space into
  two interlocking labyrinths, so there is a real inside and outside. That means it works on
  **both** backends: marching-cubes mesh with the bary wire (D1), *and* the existing
  `SdfSparkleParticles.vfx` path with no new plumbing, because a thickened gyroid
  (`|f(x)| − t`) is a usable distance field. Nothing else in this plan gets both.
- Offsetting the constant (`= c` instead of `= 0`) sweeps it from one labyrinth through the minimal
  surface to the other. That is a free, very good-looking animation.

Caveat: the trigonometric form is a *level set*, not a true distance field — the gradient
magnitude varies, so particle stick distances and marching-cubes normals will be slightly uneven.
Normalising by `|∇f|` fixes it and is cheap since the gradient is analytic.

### 3.10 Mandelbulb — Tier D1 for the mesh, D2 for the rooms. Hard.

**Already half-built in this project.** `Assets/PsychedelicLab/Shaders/WorldGridScan.hlsl:131`
has `GridScanMandelbulb(float3 c, float power, int maxIter)` — a standard escape-time Mandelbulb
with a `[loop]`-bounded 12 iterations — and `WorldGridScan` already carries `fractalPower`,
`fractalScale` and `fractalIterations`, which the tester scene sets to 8 / 0.12 / 8. The fractal
exists today as a *scan pattern* on the grid.

Two targets, different work:

**Mesh (D1).** Escape-time alone is not enough for marching cubes or for raymarching — it is a step
function. Convert it to a distance estimate with the standard running-derivative form:

```
dr = power · r^(power−1) · dr + 1        accumulated each iteration
DE = 0.5 · log(r) · r / dr
```

That turns the existing iteration loop into a usable field in about five lines, which then feeds
`ComputeMarchingCubes` and comes out as a wire-shaded mesh.

**Rooms (D2).** The camera inside the fractal. Raymarch the DE in a box-bounded shader or a URP
fullscreen pass. The hard parts are the ones every Mandelbulb raymarcher hits: step count vs frame
time, the `log`-based DE being an *under*-estimate near the boundary so it needs a safety factor
below 1.0, and surface normals from gradient differencing being noisy at high iteration counts.
Budget this as its own piece of work, not as one more entry in a shape dropdown.

Worth knowing: raising `power` above 8 and animating it is the single most striking thing a
Mandelbulb does, and `GridScanMandelbulb` already takes it as a parameter.

### 3.11 Seifert surfaces — Tier A. Medium-hard, and it reuses §3.5.

The orientable surface whose boundary is a given knot or link. For the `(p,q)` torus knot the
Seifert genus is `(p−1)(q−1)/2`, which is a nice concrete handle: `(2,3)` trefoil → genus 1,
`(3,5)` → genus 4.

Two constructions, and the choice matters:

1. **Seifert's algorithm (recommended).** Take the knot's projection, resolve every crossing into
   `p` disjoint circles, fill them as `p` flat annular sheets, then join them with `q` half-twisted
   bands at the crossings. Every piece is a swept ribbon — **so this reuses the §3.5 ribbon sweep
   and its rotation-minimising frame directly.** Build §3.5 first and this is mostly assembly.
2. **Milnor fibre.** For `f(z₁,z₂) = z₁^p + z₂^q`, the Seifert surface is the fibre
   `arg f = θ₀` intersected with `S³`, stereographically projected. Mathematically the elegant
   one and it animates beautifully by sweeping `θ₀`, but it needs a root-finding step per grid
   point rather than a closed form. Second pass.

### 3.12 Mandelbrot and Julia imagery — texture, not geometry

The 2-D escape-time sets are not shapes for this system; they are **images**, and they are most
useful as inputs to things that already exist:

- `ParticleTexture` on `SdfSparkleParticles.vfx` — a Julia set as the particle sprite.
- An emission or tint map on the chamber wire, driven through `TunnelSparkleWire`.
- A dome or backdrop plane behind the chamber.
- Fed through `Assets/SeamlessSGExtension` so the result tiles without a seam.

Implementation is a compute shader writing escape-time into a `RenderTexture`, plus **orbit traps**
for the colouring (plain iteration-count banding looks dated; trapping the minimum distance to a
point or line gives the smooth, filigree look). A `LUT Pack` LUT on top ties it to the project's
existing grade.

This is small, independent work and can be done any time — it does not block or depend on anything
else here.

---

## 4. Build order

Each step ends with something on screen; nothing depends on an unbuilt later step.

**Basics first, weird ones after.**

1. **`CurvedManifoldChamber` + `Rotor4` + the whole §3.0 basic set** — torus, Möbius, sphere,
   superellipsoid, torus-knot tube, helicoid, Klein bottle, duocylinder, with the existing cylinder
   as source #0 so the first version renders *identically to today* and proves the extraction lost
   nothing. All one domain, so they ship together, and `from`/`to`/`morph` across any pair comes
   free. Includes `IndexFormat.UInt32` and degenerate-pole handling.
2. **Roman surface** (§3.8) then **Boy's surface** (§3.3) and the **Roman ↔ Boy's morph** — the
   first of the weird ones, and cheap now that the backend exists.
3. **Gyroid / TPMS family** (§3.9) via `ComputeMarchingCubes`, including the unweld-and-write-UV1
   post-pass that D1 needs. Best value per hour in the document, and it unlocks the "rooms" idea
   without a raymarcher.
4. **Gyroid on the SDF particle path** (§3.9) — nearly free once the field function exists, since
   `SdfSparkleParticles.vfx` already takes a signed field.
5. **Torus-knot Möbius ribbons** (§3.5) with the rotation-minimising frame, then **Seifert
   surfaces** (§3.11) which reuse that sweep.
6. **Mandelbrot / Julia textures** (§3.12). Independent, small, can slot in anywhere.
7. **Mandelbulb distance estimator** (§3.10) → mesh through D1, reusing step 3's pipeline.
8. **Mandelbulb rooms** (§3.10) — the D2 raymarcher, as its own piece of work.
9. **Calabi–Yau at `n = 3`** (§3.1) then `n = 5`. Mesh density only; no new backend.
10. **`WireManifoldSwarm`** extracted from `DekeractTetraSwarm` with the 10-cube as source #0 (same
    identity check as step 1), then **Hopf fibration** (§3.2) and **attractors** (§3.7).
11. **Tier C sparkle overlay** (§2.3), if the surfaces want it.
12. **Penrose steps** (§3.4) as a separate camera-locked piece.

---

## 5. Decisions needed

Do not build past these.

- **Quad diagonals: shown or hidden?** (§2.1 gotcha 2.) The chamber shows them today. Hiding them
  gives a clean quad lattice and changes the look of every shape here. Possibly a per-shape toggle,
  possibly a global one — but it needs deciding before the shapes are tuned, because it changes
  what "looks right" means.
- **~~One shape with a dropdown, or a `from`/`to` morph pair?~~** *Settled for §3.0:* every basic
  shape is a grid on the unit square, so all of them share a domain and any pair morphs by a
  straight `Lerp`. `from`/`to`/`morph` it is. Still open for the later families — Boy's surface is a
  disk and Calabi–Yau is 25 patches, so those cannot crossfade with the basics and need either a
  hard cut or a dissolve through `scatter`.
- **Animated shapes: CPU rebuild or vertex shader?** (§2.1 gotcha 3.) The chamber only rebuilds on
  structural change. Calabi–Yau's projection sweep and the 4-D rotations change geometry every
  frame. Either move them into the vertex shader (fast, but the bary wire and bend already occupy
  that stage) or accept a `SetVertices` path. This decides how fluid the signature moves can be.
- **Does this join the HUD / mixer?** Per the project convention that everything starts neutral and
  the mixer shows selector + intensity + amount macros, these need a selector and an intensity.
  Not planned here yet.
- **Tier A or Tier B for Hopf?** (§3.2.) Swept tubes get the bary wire and the bend; the swarm is
  cheaper to build. Worth one test before committing.

## 6. Unverified

All of the above is on paper. The parameterisations are written from standard formulations, and the
gotchas called out — branch cuts (§3.1), interior poles (§3.3), the projection pole (§3.2), Frenet
degeneracy (§3.5), fixed-`dt` integration (§3.7), 32-bit indices and degenerate poles (§2.1) — are
known failure modes, not observed ones. No shape here has been run. The only measured numbers in
this document are the existing chamber's 12×48 grid and the Dekeract's 11,264 particles; every
other count is arithmetic, not a performance claim.

---

## 7. Shape-by-shape checklist

Each shape gets checked on its own; "the backend compiles" is not the same as "the shape is
right". Work down the list, one row at a time, and fill in the verdict.

**Every shape needs both layers.** The wire mesh is one layer; the Dekeract-style particle swarm is
the other, and a shape is not done until it has both and they agree. `WireParticleSwarm` rides any
`IWireGeometry`, so both chambers and every shape added later get the particle layer without new
code — but the *density, clustering and sizes* have to be tuned per shape, because a sphere, a
helicoid and a torus knot have wildly different surface areas for the same grid.

| # | Shape | Wire | Swarm | What specifically to check | Verdict |
|---|---|---|---|---|---|
| 1 | Cylinder | built | available | Must match the original Curved Geometry Chamber exactly. This is the regression test for the whole extraction | not checked |
| 2 | Torus | built | available | `minorRadius` vs `radius` ratio; inner-surface wire density vs outer | not checked |
| 3 | Sphere | built | available | **Poles.** The `0.5/builtV` inset should stop the wire flickering; confirm there is no pinch artefact | not checked |
| 4 | MobiusStrip | built | available | The seam where the half-twist closes. `halfTwists` 1 vs 2 should look clearly different | not checked |
| 5 | Superellipsoid | built | available | Signed-power path: no NaNs at the extremes of `squareness`/`roundness`. Check the corners at high values | not checked |
| 6 | TorusKnotTube | built | available | **Frame flips.** Uses a reference-vector frame, not Frenet. Watch for the tube twisting at low-curvature points on high `(p,q)` | not checked |
| 7 | Helicoid | built | available | The only shape open in **both** u and v. Confirm the edges are clean and the wire is not wrapping | not checked |
| 8 | KleinBottle | built | available | Self-intersection reads correctly as opaque. The figure-8 pass-through is the whole point | not checked |
| 9 | Duocylinder | built | available | The ridge join in `BothCells` must be continuous. Sweep `cellFill` 0→1 and the projection modes | not checked |
| 10 | CliffordTorus | built | available | **Stereographic pole guard** at `w → 1`. The isoclinic turn needs equal `simpleRates` | not checked |
| 11 | Fractal accordion rings | built | built | Cantor clustering actually subdividing; `bandFraction` and `connectors` reading as rings-and-struts | not checked |
| 11b | Metatron's Cube | n/a | built | 13 nodes / 78 lines; `flatten` 0 to 1 from Vector Equilibrium to flat glyph; length-class filter revealing the inner solids | not checked |
| 11c | Tesseract to Dekeract (4-10) | n/a | built | One script per dimension. Counts match 2^n and n*2^(n-1); projection chain must not collapse the figure at high n | not checked |
| 11d | Polyhedron (Platonic / prism / Johnson) | n/a | built | `MeshBuilder.edges()` dedup; Johnson index range; weld distance leaving shared corners as single nodes | not checked |
| 11e | Swirl deformers (all swarms) | n/a | built | twist / vortex / inversion / spherize must each be identity at 0 | not checked |
| 12 | Roman surface | not built | — | — | — |
| 13 | Boy's surface | not built | — | — | — |
| 14 | Gyroid / TPMS | not built | — | — | — |
| 15 | Mandelbulb mesh | not built | — | — | — |
| 16 | Mandelbulb rooms | not built | — | — | — |
| 17 | Seifert surfaces | not built | — | — | — |
| 18 | Calabi–Yau | not built | — | — | — |
| 19 | Hopf fibration | not built | — | — | — |
| 20 | Attractors | not built | — | — | — |
| 21 | Penrose steps | not built | — | — | — |

### Per-shape things that will need tuning, not fixing

- **Particle density.** `nodesU` × `nodesV` × `4^fractalLevels` is the node count before
  travellers. At the defaults (32 × 24, 1 level) that is 3,072 nodes × 4 = 12,288, already near the
  Dekeract's proven 11,264. The `maxParticles` cap is a hard stop and `Status` says when it is hit.
- **Cluster spread** is a fraction of a *grid cell*, measured from the surface tangents, so it
  adapts to shape scale automatically — but `clusterRatio` below 0.5 is what makes the recursion
  visible, and shapes with very uneven parameterisation (sphere near the poles, knot tubes) will
  want it lower.
- **Lattice divisor** on the wire should stay matched to the swarm's grid, or the particles will sit
  between wire lines instead of on them.

---

## 8. Second wave of shapes

Added after the first build pass. Same two-layer rule: wire where it makes sense, swarm always.

### 8.1 Regular 4-polytopes, including the 120-cell — Tier B (swarm). Buildable now.

The six regular 4-polytopes are all node-and-edge figures, so they drop straight onto
`NodeEdgeSwarmBase` next to the n-cubes:

| Polytope | Vertices | Edges | Cells | Particles at 2/edge |
| --- | --- | --- | --- | --- |
| 5-cell (simplex) | 5 | 10 | 5 tetrahedra | 25 |
| 16-cell | 8 | 24 | 16 tetrahedra | 56 |
| 8-cell (tesseract) | 16 | 32 | 8 cubes | 80 — already built |
| 24-cell | 24 | 96 | 24 octahedra | 216 |
| **120-cell** | **600** | **1200** | **120 dodecahedra** | **3,000** |
| 600-cell | 120 | 720 | 600 tetrahedra | 1,560 |

The 120-cell is the headline and it is **cheaper than the Dekeract** — 3,000 particles against
11,264. No reason not to build it.

**The construction trick that makes this tractable:** do not hand-write 1,200 edges. Generate the
600 vertices from the H4 coordinate orbits (golden-ratio permutations), then compute all pairwise
distances once at build, take the minimum, and declare an edge wherever a pair sits within
tolerance of it. 600 vertices is 180,000 pairs — nothing, once, on rebuild. The same routine then
gives every polytope in the table for free, and it is the approach `PolyhedronSwarm` already uses
for the 3-D solids.

4-D rotation and projection reuse the n-cube path (`NCubeSwarmBase.Collapse`), since a 4-polytope
is just the n = 4 case with a different vertex set.

### 8.2 Morin surface and the sphere eversion — Tier A. Hard.

The Morin surface is the **halfway model of a sphere eversion**: the exact midpoint of turning a
sphere inside out without tearing or creasing. It has 4-fold symmetry and is an immersion of the
*sphere*, which makes it a different object from Boy's surface — Boy's is the halfway model for
RP-squared with 3-fold symmetry (section 3.3). Worth keeping that distinction straight, because the
two get conflated constantly.

**The real prize is the animation, not the still.** A static Morin surface is a knot of lobes; the
eversion — sphere, to Morin surface, to inside-out sphere — is one of the great pieces of
mathematical animation, and the chamber's morph machinery is already built for exactly that kind of
sweep.

Honest warning: the parameterisations (Apery, Francis, Morin) are **not** as clean as the
Bryant-Kusner formula for Boy's surface, and the published forms differ in convention. This one
needs its formula sourced carefully and checked against reference images before any of it is
trusted — it should not be generated from memory.

### 8.3 Barth sextic — Tier D1 (implicit). Medium.

A degree-6 algebraic surface with **65 ordinary double points**, the maximum known for its degree,
and full icosahedral symmetry. Implicit, with `p` the golden ratio:

```
4(p^2 x^2 - y^2)(p^2 y^2 - z^2)(p^2 z^2 - x^2) - (1 + 2p)(x^2 + y^2 + z^2 - w^2)^2 w^2 = 0
```

`w` is a homogenising parameter, normally fixed at 1; animating it swells and splits the surface,
which is a free and very good-looking sweep.

Goes through the same marching-cubes path as the gyroid and the Mandelbulb (section 2.4, D1), so
once that pipeline exists this is one more field function. Being a polynomial it is far cheaper to
evaluate than the Mandelbulb, and unlike the gyroid it is bounded, so the volume fits it without
tiling.

Caveat: the 65 double points are exactly where the gradient vanishes, so marching-cubes normals
degenerate there. Expect pinching at the singular points — honest to the surface, but it means the
wire will read better than smooth shading.

### 8.4 Mandelbox — the infinite rooms. Tier D2. Hard.

**This is the one that actually looks like endless architecture.** The Mandelbulb (section 3.10) is
organic and bulbous; the "infinite rooms" look — corridors, chambers, balconies repeating inward
forever — is the **Mandelbox** (Tom Lowe), and its distance estimator is short and well documented:

```
boxFold    : v = clamp(v, -1, 1) * 2 - v
sphereFold : m = |v|^2
             if m < minR^2      : v *= fixedR^2/minR^2 ; dr *= fixedR^2/minR^2
             else if m < fixedR^2 : v *= fixedR^2/m    ; dr *= fixedR^2/m
step       : v = scale*v + c ; dr = dr*|scale| + 1
DE         : |v| / |dr|
```

`scale` is the whole character: negative values (around -1.5 to -2) give the classic room-and-
corridor interiors, positive values give a more crystalline shell. This should be the **first** D2
raymarcher built, before the Mandelbulb rooms, because it pays off faster and the DE is simpler.

Also worth having on the same raymarcher, since they are a few lines each once the marcher exists:
**Menger sponge** (cube fold, genuinely architectural) and **kaleidoscopic IFS** (plane folds plus
a scale, the source of most "infinite temple" renders).

### 8.5 Metatron's Cube — built

Now `MetatronCubeSwarm`. The construction note worth repeating: the classic flat glyph is **the
cuboctahedron plus its centre viewed down a 3-fold axis**, which is why the figure has an outer
hexagon and an inner hexagon rotated 30 degrees. So the component builds the 3-D Vector Equilibrium
and one `flatten` dial collapses it to the glyph, instead of treating the 2-D and 3-D forms as
unrelated point sets. 13 nodes, all 78 lines, C(13,2) = 78.

### 8.6 Next up, by explicit priority

**Clifford torus and Calabi-Yau, done intensely**, using `Assets/GeometryAlgorithms`
(`Jobberwocky.GeometryAlgorithms.Source.API`: `VoronoiAPI`, `HullAPI`, `TriangulationAPI`, over
Hull3D / Triangulation3D / Voronoi3D / Extrusion) together with the GeometryFXParticles systems,
rather than as plain parametric grids.

What that buys over sections 3.1 and 3.6: the Calabi-Yau's 25 patches and the Clifford torus's
self-intersecting sheets can be **retriangulated and cell-decomposed** instead of drawn as a
regular quad grid. Voronoi cells over the surface give a shattered, panelised read, and the hull
and extrusion passes let each patch become solid shell geometry rather than a membrane. Both are
looks a parametric grid cannot produce, and they are the reason to route these two shapes through
the geometry library instead of the plain grid path.
