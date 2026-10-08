# Geometry Engine (`Assets/_GeometryWork/Engine`)

The standalone, dependency-free core of the geometry work: pure math, mesh builders and thin
components that need nothing but `UnityEngine`. No Curved World, no control rig, no VFX Graph —
it compiles in any URP project, and the existing studio code (`Core/`, `_EscherWorldManagement/`)
reads it like any other assembly.

## Assemblies

| asmdef | Folder | Contents |
| --- | --- | --- |
| `GeometryEngine.Runtime` | `Engine/` | Everything below except the two folders that follow. Auto-referenced, so `Assembly-CSharp` (the studio code) sees it with no setup |
| `GeometryEngine.Editor` | `Engine/Editor/` | `GameObject ▸ Geometry Engine` creation menu |
| `GeometryEngine.Tests` | `Engine/Tests/Editor/` | NUnit edit-mode tests (Unity Test Runner) |

The asmdef sits on `Engine/`, not on `_GeometryWork/` as the plan sketched: `Core/` still depends on
Curved World and `PsychedelicLab.Control`, and an asmdef over it would cut it off from them. Pure
files moved in with their `.meta` (GUIDs kept): `IWireGeometry.cs`, `ImplicitShapes.cs`.

## The wire pipeline invariants — `Mesh/WireMeshBuilder.cs`

Every engine mesh goes through `WireMeshBuilder`, which is where the invariants live:

- unwelded — six vertices per quad, barycentrics in **UV1**, parameter UV in UV0;
- quad diagonal hidden by lifting the vanishing bary component by +1 (into [1,2]);
- `IndexFormat.UInt32` always; bounds fixed at ≥ 200 units so GPU displacement is never culled.

`Quad`, `Triangle`, `Polygon(points)` (a centroid fan whose spokes never draw, for n-gon faces),
`Grid(points, columns, rows)`, `Apply(mesh)`; buffers are reused, so rebuilding
at a fixed size does not allocate.

`TopologyReport.Measure(builder)` welds coincident positions and reports V, E, F, χ, boundary edges
and loops, non-manifold edges, orientation conflicts, connected components, degenerate faces and
non-finite positions. It is how the tests (and `Log topology` on any component) check the output.

## API

| Type | File | What it is |
| --- | --- | --- |
| `Rotor4`, `Plane4` | `Math/Rotor4.cs` | SO(4) as a left/right unit-quaternion pair, P' = L·P·R. `FromBivector(xy, xz, xw, yz, yw, zw)` applies all six plane angles at once (no order, no gimbal lock); `Plane`, `Double`, `Isoclinic`, composition `*`, `Inverse`, geodesic `Slerp`, `ToMatrix`, `InvariantAngles`. Angles follow `Hyperspace4DAxis`' Givens convention exactly |
| `Polytope4DLibrary`, `Polytope4D`, `RegularPolytope4D` | `Polytopes/` | The six regular 4-polytopes: unit-sphere vertices, edges, 2-faces (chordless planar cycles), cached. `Stereographic`, `Perspective` projections |
| `CurveSweep` | `Curves/CurveSweep.cs` | Rotation-minimising (double-reflection) frames with holonomy correction for closed curves; `Tube`, `Ribbon`, `ResampleByArcLength` |
| `HopfFibration`, `HopfBaseSet` | `Manifolds/HopfFibration.cs` | `FiberPoint`, the Hopf map `Map`, stereographic `Project` and `ConformalScale`, base-point sets, `Fiber` polylines, Gauss `LinkingNumber` |
| `StrangeAttractors`, `Attractor`, `AttractorParameters` | `Manifolds/StrangeAttractors.cs` | Lorenz, Rössler, Thomas, Halvorsen, Aizawa (double-precision RK4: `Rk4Step`, `Integrate`, step-doubling `IntegrateAdaptive`); Clifford and De Jong maps (`Iterate`) |
| `SeifertSurface`, `SeifertPreset` | `Manifolds/SeifertSurface.cs` | Seifert's algorithm on closed braids: braid words (`Word`, `Parse`), `Components`, `EulerCharacteristic`, `Genus`, `Build` |
| `ScrewDislocation`, `Axis3` | `Implicit/ScrewDislocation.cs` | The Escher step as one shared field-space transform; `TpmsPeriod(frequency)`. `Axis3` is the project's only definition (KaleidoFold and Hyper4DField use it) |
| `EscherSpace` | `Implicit/EscherSpace.cs` | The harder Escher warps, seam-exact: sphere inversion (Möbius), Droste spiral (scale-periodic, Print Gallery twist, staircase folded in), screw dislocation with a field-level core blend (no crack), drift-free scrolling window. `Map` + `Evaluate` are called by `Implicits`, `EscherFields`, and mirrored in `EscherField4D.hlsl` |
| `DualContouring`, `IScalarField` | `Implicit/DualContouring.cs` | QEF dual contouring with a truncated eigen pseudo-inverse; `solveQef = false` gives surface nets. `cubeness` blends vertices to cell centres (voxel masonry, same topology); `parallel` threads sampling and placement with identical output |
| `EnneperPillar`, `PillarFootprint` | `Manifolds/EnneperPillar.cs` | The closed Enneper pillar: Round (original) or a Square/Hexagonal lattice footprint whose ends tile the hall into one C¹ vault; row-separable evaluation; lattice centres and corner-snapping column counts |
| `Implicits`, `ImplicitSettings`, `ImplicitShape` | `Implicit/ImplicitShapes.cs` | The 13 implicit fields (moved here), now with `dislocation`, `dislocationAxis`, `dislocationCore` |
| `Polyhedron` | `Polyhedra/Polyhedron.cs` | Polygon mesh with oriented face loops: edge map, CCW vertex rings, `Validate` (closed, oriented, unpinched), Newell normals, planarity, `Emit` to a wire mesh |
| `Conway` | `Polyhedra/Conway.cs` | Conway notation: seeds T C O D I, Pn An Yn; primitives d a k g c w q r; derived t j e o s b m n; kn / tn; `Parse("tI")` right to left with a face budget; Hart canonical form (`Canonicalize`, via the dual when that relaxes better, or between every step with `Parse(..., canonicalIterations)`) |
| `IWireGeometry` | `Core/IWireGeometry.cs` | The particle-swarm contract (moved here so engine components implement it) |

## Components

All derive from `WireMeshComponent` (`[RequireComponent(MeshFilter, MeshRenderer)]`): assign any
wire material, rebuild on change or while `animate` is on, `Status` readout, `Log topology` context
menu. All implement `IWireGeometry`, so a `WireParticleSwarm` on the same object rides them.

| Component | Menu | Notes |
| --- | --- | --- |
| `HopfFibrationEngine` | Hopf Fibration | Swept fiber tubes (constant thickness on S³ when `conformalTubes`), 4-D rotation rates per plane, fibers through the pole clipped; particles flow along fibers, sheared by latitude |
| `AttractorEngine` | Strange Attractor | Trajectory integrated once and cached; animation slides a window along it. Tube, ribbon or dust |
| `SeifertSurfaceBuilder` | Seifert Surface | Presets (trefoil, figure-eight, cinquefoil, T(p,q), Hopf link, Borromean) or a braid word (`"1 -2 1 -2"` / `"aBaB"`) |
| `ConwayPolyhedronEngine` | Conway Polyhedron | Any Conway notation, optionally canonical; faces drawn as whole polygons; particles ride the edges |
| `DualContourEngine` | Dual Contour Surface | Any `ImplicitShape` with the screw dislocation and `EscherSpace` warps; DC or surface nets; cubeness; threaded; animated re-extraction on a timer |

`Shaders/GeometryEngineWire.shader` (`GeometryEngine/Wire`) is the standalone material: constant
screen-width AA wire, `_ShowDiagonals`, near/far depth fade, dual-tone gradient along U or V, and a
travelling lattice pulse with a script-driven `_PulsePhase` for beat sync. The chamber materials
also work on these meshes.

## Performance

Measured outside Unity (Release .NET 8, 4 cores), so absolute numbers will differ in the Editor; the
ratios are the point.

| Workload | Before | After | How |
| --- | --- | --- | --- |
| One 180×180 Enneper pillar, per frame | 38 ms | 6 ms | `CurvedGeometryChamber` evaluates each lattice node once (was four times) with per-frame constants hoisted; pillar evaluated row by row; base surface skipped when the deformation replaces it. Chamber output bit-identical across 8 recorded configurations |
| Plain Enneper chamber, per frame | 8.6 ms | 3.6 ms | Same lattice evaluation |
| 64³ gyroid + dislocation, surface nets | 143 ms | 80 ms | Threaded sampling and placement, identical output |
| 64³ gyroid + dislocation, dual contouring | 554 ms | 177 ms | Same |
| Chamber `SampleGrid` (per particle) | 127 ns | 79 ns | The frame constants the fill computed are reused instead of rebuilt per sample (pass 3) |
| `ManifoldComboChamber`, 3 morphing 96×64 layers | 13.4 ms | 5.3 ms | Each node evaluated once into a lattice (was once per quad corner) (pass 3) |
| Escher room, 96³ gyroid + dislocation | 365 ms | 112 ms | Sampling and marching on worker threads, z-slabs merged in order; only the edges a case uses (pass 3). Bit-identical to the serial pass |
| Escher room, 48³ gyroid + dislocation | 49 ms | 24 ms | Same |
| Scherk hall / colonnade | 2 charts | 1 chart | The default two branches built the same periodic surface twice (pass 3) |

Every chamber-pool engine (Enneper, Scherk, ShapePrinter) gets the chamber speed-up. The second pass
reported that threading the Escher rooms' sampling gave no gain; that measurement included the
stand-in's mesh upload, which dominates outside Unity. Measured on its own, sampling was the largest
real cost (217 of 365 ms at 96³), and threading it is kept. Room timings exclude the mesh upload.

## Tests

- In Unity: Test Runner ▸ EditMode ▸ `GeometryEngine.Tests`.
- Without Unity: `dotnet test Tools/EngineTests/Tests` compiles this folder against a small
  UnityEngine stand-in and runs the same tests (166 at the end of pass 3).
- `dotnet test Tools/CoreTests` compiles the studio-facing files (chamber, combo chamber,
  manifolds, Enneper and Scherk engines, the NCube swarms, hyperspace, Escher fields and rooms)
  against the stand-in plus stubs for the packages not in this repository, and runs behaviour tests
  that need them (154 at the end of pass 3): Enneper hall ceilings meet on shared edges; the engine
  and room fields agree under every warp; every parametric surface is finite everywhere and has its
  expected topology through the chamber and the combo; the seam flags match the formulas; Scherk
  points satisfy their equations; threaded rooms equal serial rooms; the portal shader's field (a
  line-for-line C# transcription) equals the room field; the HLSL compiles.
- `python3 Tools/ShaderCheck/check.py` type-checks every `.shader` pass and keyword variant with
  glslang (`apt install glslang-tools`), against stubs for URP core, Curved World and WorldGridScan.

What they pin down: Rotor4 against the Givens matrices, orthogonality, det = 1, composition,
subgroup property and slerp; all six polytopes' V/E/F/C, degree and edge length, and the 600-cell
as a group; Hopf fibers on S³, mapping to their base point, projecting to circles and linking once;
RK4 fourth-order convergence, adaptive accuracy and cost, bounded flows, Lorenz mean z; tubes as
orientable tori/cylinders; Seifert χ, genus, components, orientability and connectedness; seamless
dislocation on all six TPMS and axes; dual contouring closed/oriented/on-surface and corner-sharp;
and every component free of NaN in both its mesh and its particle sampler.

## Bugs found and fixed on the way

- **600-cell had 84 of 120 vertices** (60 of 720 edges): its orbit flipped signs on three
  coordinates only. `Polytope4DSwarm` and `GeometryFractalEngine` now read `Polytope4DLibrary`.
- **Escher screw dislocation used the wrong period** (2π/f instead of 2/f for fields evaluated at
  π·f·p): one circuit advanced π floors, so the field jumped by up to 0.86 (6.0 for Neovius) across
  the seam. Now ~1e-4.
- **Rotor slerp could flip one half alone**, turning a rotation into its composition with −I.
  Caught by its test before it shipped.
- **Dual contouring winding was inverted** and vertices built on grid corners lying exactly on
  the surface sat up to 14% of a cell off it; both caught by tests and fixed.
- **Two `Axis3` enums in one namespace** (KaleidoFold's and the engine's) made every use ambiguous
  once the engine became its own assembly — a Unity compile error, caught by `Tools/CoreTests`.
- **Enneper pillars** took their flute phase from a global value (ignoring phase per copy and level,
  and dropping the swing when paused), their flared ends interpenetrated in halls, and NestedFlower /
  FoldCorridor rotated copies about their own tilted axis, so crowns were not symmetric.
- **The 4-D room fields were not the named surfaces at W = 0** (Schwarz P off by 1, Neovius by 3,
  the 4-D gyroid not a gyroid), on CPU and GPU; the GPU field also had the old dislocation period.
- **Pass 3.** Every open-axis surface grid sampled line i at i/(n−1) over n quads (a column of
  extrapolated surface and a row of zero-area quads, on 21 of 30 surfaces, in the chamber and the
  combo); the Klein bottle and Möbius band's node grid closed onto the wrong row; Dini, Bour and the
  duocylinder had wrong seam flags; the superellipsoid's seam opened by 1.4·10⁻³. The screw core
  cracked inside the core and could not coexist with the Droste map (fixed through the Shape Bench).
  Scherk: exact lobes collapsed whole rows to a point, even-column halls cut through cells, and halls
  were built twice. Snub solids did not canonicalise from operator positions. On the GPU, the portal
  field lacked the Mandelbulb, hard-coded four fields' parameters, ignored W influence, cracked at
  the screw core and took pow() of negative numbers.

## Not done yet

- **Morin surface / sphere eversion (plan 1.4).** The plan and GEOMETRY-MAP both require the
  parameterisation to be sourced and checked, not written from memory. Pass 3 tried again: search
  works, but every source host (arXiv, Wikipedia, virtualmathmuseum, math.uiuc.edu, cp4space) is
  blocked by this environment's egress policy. Leads for a session with access, from the search:
  Kusner's minimal surface, whose inversion is a Morin surface, has an explicit Weierstrass
  representation (cited in arXiv 2506.23359, "The Willmore Energy Landscape of Spheres…"); Bednorz &
  Bednorz (2017), "Analytic sphere eversion using ruled surfaces"; Apéry (1992), the closed halfway
  model as the zero set of a degree-8 polynomial. `TopologyReport` already gives the checks: χ = 2,
  four-fold symmetry swapping the sides, a quadruple point on the axis, no NaN.
- **GPU extraction (plan 2.3)** and **Burst/Jobs** paths — not started.
- **Calabi–Yau (plan 1.5)** was already implemented (`Manifolds.CalabiYau`, `CurvedGeometryChamber`
  mode `CalabiYau`, degree 2–7 with n² patches and a projection angle); left as is.
- The new shader features live in `GeometryEngine/Wire`; the existing chamber shaders are unchanged.
- Nothing here has been rendered in the Unity Editor yet. The math is tested; the C# compiles
  against a stand-in, and every shader type-checks under glslang with stubbed URP / Curved World
  includes, but none of it has run in Unity.
