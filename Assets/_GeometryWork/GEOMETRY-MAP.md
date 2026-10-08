# Geometry work — the full trace

Every 3D shape, particle and shape-management tool in this project, where it lives, and what it
actually does. Written because the work was scattered across six folders and two packages, so the
same thing kept getting rebuilt.

`Assets/_GeometryWork/` is the home for the project's own geometry work. Third-party packages stay
where they are (moving them breaks their internal include paths); this file traces them instead.

---

## 1. What was already here — found, not built

These existed before the current round of work. Several things were rebuilt that did not need to be,
so check this table before writing anything new.

| Tool | Path | What it actually does |
| --- | --- | --- |
| **`FractalMeshFactory`** | `Assets/PsychedelicLab/Warp/FractalMeshFactory.cs` | Bakes **Menger sponge**, **Sierpinski pyramid** and **Mandelbulb** to meshes on the CPU. Note: it is **voxel-face extraction**, not marching cubes — it tests `InsideBulb` per voxel and emits cube faces for exposed sides. Blocky by design |
| Baked fractal meshes | `Assets/PsychedelicLab/Warp/Meshes/` | `MandelbulbVoxels`, `Mandelbulb_P4_R40`, `Mandelbulb_P12_R40`, `MengerSponge`, `SierpinskiPyramid` — already generated, already assets |
| Fractal SDF assets | `Assets/PsychedelicLab/Warp/SDF/` | `FractalSdf_Mandelbulb`, `FractalSdf_Menger`, `FractalSdf_Sierpinski` |
| Fractal materials | `Assets/PsychedelicLab/Warp/` | `Mandelbulb.mat`, `MandelbulbP4`, `MandelbulbP12`, `MengerSponge.mat`, `SierpinskiPyramid.mat`, `SierpinskiPyramidGold.mat` |
| Warp rig | `Assets/PsychedelicLab/Warp/` | `WarpMaster.cs`, `WarpGroup.cs`, `ObjectWarp.cs`, `FractalDrift.cs` |
| **Ready-made shape prefabs** | `Assets/_TheCrazyShapes/` | `SDF Particles_ Mandelbulb`, `SDF Particles_ Menger`, `SDF Particles_ Sierpinski`, `Keijiro_ComputeMarchingCubes_Sample`, `Keijiro_NoiseBall6_Sample`, `Keijiro_Layered_VFX_BG_Dance`, `WallPieces` |
| Marching cubes adapter | `Assets/PsychedelicLab/ControlRig/Adapters/MarchingCubesPerformable.cs` | `IPerformable` wrapper so marching cubes is drivable from the control rig |
| **NoiseBall 6** | `Packages/jp.keijiro.noiseball6/` | Installed as a package (`NoiseBall.compute`, `NoiseBall.asmdef`) — direct vertex/index buffer writes from a compute shader |
| **Mesh to SDF** | `Packages/com.unity.demoteam.mesh-to-sdf` (source in `GitRepoKeijiro/`) | Unity DemoTeam. Rasterises a real mesh to a 3D SDF RenderTexture from its GPU vertex/index buffers — no CPU-readable mesh needed. `SDFTexture.cs` holds the volume transform, `MeshToSDF.cs` does the work |
| ComputeMarchingCubes | `GitRepoKeijiro/ComputeMarchingCubes/` | Keijiro's GPU marching cubes: `MarchingCubes.compute`, `MeshBuilder.cs`, `TriangleTable.cs`, plus `NoiseFieldGenerator.compute` as a worked field example. Reference copy, not in `Assets/` |
| **polyhedronGenerator** | `Assets/polyhedronGenerator/` | Platonics, prisms, antiprisms and **all 92 Johnson solids**. `MeshBuilder.edges()` returns an explicit edge list, which is the integration point. `prefabs/urp/wireframeParticle.prefab` is the mesh-particle prefab every swarm uses |
| **GeometryAlgorithms** | `Assets/GeometryAlgorithms/` | `Jobberwocky.GeometryAlgorithms.Source.API`: `VoronoiAPI`, `HullAPI`, `TriangulationAPI` over Hull2D/3D, Triangulation2D/3D, Voronoi2D/3D, Extrusion |
| Mandelbulb in the scan shader | `Assets/PsychedelicLab/Shaders/WorldGridScan.hlsl:131` | `GridScanMandelbulb(c, power, maxIter)` — escape-time, 12-iteration bounded loop. `WorldGridScan` already exposes `fractalPower` / `fractalScale` / `fractalIterations` |
| SDF particle backend | `Assets/Fast & Simple SDF Visual Effects for URP/` + `Assets/PsychedelicLab/Stage4D/FractalSdfParticles.vfx` | VFX Graph consuming a 3D distance texture. Nine exposed properties, **no size control** — see §3 |
| 4D stage | `Assets/PsychedelicLab/Stage4D/` | `HypercubeTunnel.cs`, `Shape4DSdfParticles`, `Slice4D` shader |
| Non-Euclidean references | `GitRepoKeijiro/HyperEngine`, `noneuclideanunity`, `Portals` | Hyperbolic/spherical rendering and portals. All built-in pipeline, need porting |

---

## 2. `_GeometryWork/Core/` — the project's own geometry work

Moved here from `Assets/GeometryFXParticles/Combo/` (guids preserved, so scene references are
intact). The two editor path constants were repointed.

### Shape management

| File | Role |
| --- | --- |
| `IWireGeometry.cs` | **Moved to `Engine/Core/`** (GUID kept). The one interface a chamber exposes so a particle swarm can ride it: `IsBuilt`, `GridU`, `GridV`, `SampleGrid(u,v)`. Every chamber implements it, so new shapes get particles with no new code |
| `ManifoldSurfaces.cs` | 30 parametric surfaces as one `ManifoldSurface` enum plus `ManifoldSettings`. Also `Manifolds.CalabiYau` (Fermat quintic slice, n² patches) and the wrap/pole classification the mesh builder needs |
| `AccordionSettings.cs` | The fractal accordion's parameters, plus `RingProfiles` (circle, polygon, superellipse, star, gear) |
| `RingFractal.cs` | Real recursions: `CantorPosition` (IFS ring spacing — clusters of clusters), `KochRadius` (triangle-wave series, sharp enough that the recursion reads), `ApollonianScale` / `SubdivideGap` |
| `RingGeometry.cs` | The accordion ring tunnel as a reusable builder, shared by the chamber and the combo |
| `ImplicitShapes.cs` | **Moved to `Engine/Implicit/`** (GUID kept), now with the screw dislocation. 13 implicit fields: gyroid, Schwarz P/D, Neovius, Lidinoid, Split-P, **Barth sextic**, Mandelbulb DE, **Mandelbox**, Menger, Sierpinski, torus, Goursat. Flags which are true signed distances and which merely tile |

### Chambers — mesh + barycentric wire

All use the same technique: unwelded triangles, bary coordinates in UV1, normalised UV0, 32-bit
indices, Curved World bend ID 1, `WorldGridScan` tint, material cloned per instance and registered
with the shared bridge.

| File | Role |
| --- | --- |
| `CurvedGeometryChamber.cs` | **The chamber.** Four modes: `Cylinder` (the original tunnel, default, unchanged), `Manifold` (any of the 30 surfaces with from/to morphing), `FractalRings` (the accordion), `CalabiYau` (n² patches) |
| `ManifoldComboChamber.cs` | Several manifolds at once, each with its own resolution, placement, spin, tint and morph. A layer can be a surface **or** an accordion ring tunnel. Nine presets, including `KleinHelicoidAccordion` |
| `ImplicitSurfaceChamber.cs` | Extracts an `ImplicitShape` zero crossing as a wire mesh via **naive surface nets** — one vertex per cell at the mean of its edge crossings, a quad per sign-changing grid edge. No 256-case table, and a surface rather than voxel faces, so it complements `FractalMeshFactory` instead of repeating it |
| `TunnelSparkleParticles.cs` | Skins an existing FlowPath tunnel mesh with the wire shader plus an SDF sparkle cloud |

### Swarms — mesh particles on nodes and edges (`Core/NCube/`)

| File | Role |
| --- | --- |
| `NodeEdgeSwarmBase.cs` | All the Dekeract mechanisms: one `ParticleSystem` in mesh mode with a generated tetrahedron, node particles, edge travellers with a travelling phase, scatter/reassemble to a Fibonacci sphere, `1-exp(-regroupSpeed·dt)` smoothing, cyan→gold palette, tumble. Plus **recursive fractal clustering** (4-map IFS per particle) and **swirl** (twist / vortex / inversion / spherize) |
| `NCubeSwarmBase.cs` | n-cube topology (2^n vertices, n·2^(n-1) edges) and the n-D → 3-D perspective chain |
| `TesseractSwarm` … `DekeractSwarm` | One script per dimension, 4 through 10 |
| `MetatronCubeSwarm.cs` | 13 nodes, all 78 lines (C(13,2) = 78). The flat glyph is the cuboctahedron plus centre viewed down a 3-fold axis, so `flatten` is one dial from Vector Equilibrium to glyph. Length-class filter reveals the solids inside |
| `Polytope4DSwarm.cs` | All six regular 4-polytopes including the **120-cell** (600 vertices, 1200 edges). Edges derived by shortest pairwise distance, never hand-written. Vertices now come from `Engine/Polytopes/Polytope4DLibrary` (the old 600-cell orbit was wrong, see §3) and rotation is one `Rotor4` per frame |
| `PolyhedronSwarm.cs` | Bridges polyhedronGenerator: Platonics, prisms, antiprisms, all 92 Johnson solids via `MeshBuilder.edges()` |
| `WireParticleSwarm.cs` | The swarm that rides any `IWireGeometry`, so every chamber and every future shape gets the particle layer |

`DekeractTetraSwarm.cs` is the original and is untouched — the n-cube family is separate.

### SDF particle path

| File | Role |
| --- | --- |
| `SdfSparkleParticles.vfx` | A copy of `FractalSdfParticles.vfx` with **`ParticleSize`** and **`ParticleTexture`** exposed. Stage4D's original is unmodified |
| `SdfParticleRig.cs` | `SdfParticleSettings` (size scale and curve, sprite, spawn rate, lifetime, stick/attraction, palette, named presets) plus the rig that owns the VisualEffect and pushes them. Every write guarded |
| `TunnelSdfField.compute` | Bakes a hollow ribbed tube SDF, using GeometryMorph's storage convention (distance ÷ cube side) |
| `GeometryMorph.compute` | The original seven-form morphing SDF field |
| `GeometryParticleCombo.cs` | The original combo, now driving particles through `SdfParticleRig` so size is controllable |

### Shaders

| File | Role |
| --- | --- |
| `CurvedChamber.shader` | Opaque barycentric wire, `Cull Off`, WorldGridScan tint, bend ID 1 |
| `TunnelSparkleWire.shader` | Additive lattice wire with sparkles on lattice nodes, sparsity gating and a sub-pixel fade. `_UVScale` corrects index-space UVs |
| `CurvedGeometryAccent.shader`, `GeometryAdditive.shader` | The original additive accent shaders |

### Materials — `_GeometryWork/Materials/`

One material per shape family rather than one shared material, so each family's lattice density,
glow and sparsity can be tuned without disturbing the others. All wire materials use
`PsychedelicLab/Tunnel Sparkle Wire`; all particle materials use `GeometryAdditive`.

| Wire material | For | Tuned toward |
| --- | --- | --- |
| `Wire_Chamber_Cyan` | the default chamber | the project's cyan / amber |
| `Wire_Accordion_Rings` | fractal accordion | few lines around, many along, fast flow |
| `Wire_Manifold_Teal` | general manifolds | even lattice both ways |
| `Wire_Klein_Violet` | Klein bottle | violet, higher glow for the self-intersection |
| `Wire_Clifford_Ice` | Clifford torus / duocylinder | low opacity so the inversion stays readable |
| `Wire_Helicoid_Gold` | helicoid | very few lines around, many along, strong flow |
| `Wire_CalabiYau_Gold` | Calabi-Yau | coarse lattice, thick lines, dense sparkle — 25 patches are already busy |
| `Wire_Gyroid_Teal` | gyroid and the TPMS family | fine lattice, thin lines |
| `Wire_Mandel_Ember` | Mandelbulb / Mandelbox | finest lattice, highest glow |
| `Wire_Barth_Rose` | Barth sextic | rose, for the 65 double points |
| `Wire_Menger_Ice` | Menger / Sierpinski | finest lattice, muted sparkle |
| `Wire_Metatron_Gold` | Metatron's Cube | coarse, bright, low sparsity |
| `Wire_Polytope_Ice` | 4-polytopes and n-cubes | low opacity for 1,200-edge figures |
| `Wire_Knot_Lime` | knot tubes and ribbons | many lines along, few around |
| `Wire_Minimal_Pearl` | minimal surfaces | near-white, faint, sparse |

| Particle material | Tint |
| --- | --- |
| `Particles_Cyan` | the Dekeract default |
| `Particles_Gold` | warm |
| `Particles_Violet` | cool |
| `Particles_Ember` | hot, brightest |
| `Particles_Ice` | pale blue |
| `Particles_Fine` | dim with a higher soft-intersection fade, for very dense swarms |

`ComboLayer.materialOverride` lets each layer of a combo use its own, and the
`KleinHelicoidAccordion` preset assigns three different ones.

---

## 2a. `_GeometryWork/Engine/` — the standalone engine

Its own assembly (`GeometryEngine.Runtime`, UnityEngine only), so it compiles without Curved World
or the control rig. Full API in `Engine/README.md`; summary:

| File | Role |
| --- | --- |
| `Mesh/WireMeshBuilder.cs` | The wire invariants in one place: unwelded quads, bary in UV1, +1 diagonal lift, UInt32, 200-unit bounds |
| `Mesh/TopologyReport.cs` | Welded V/E/F, χ, boundary loops, orientability, components, NaNs — the self-check every test and component uses |
| `Math/Rotor4.cs` | SO(4) as a left/right quaternion pair; all six plane angles at once as one bivector |
| `Polytopes/Polytope4DLibrary.cs` | The six regular 4-polytopes with vertices, edges and 2-faces, verified |
| `Curves/CurveSweep.cs` | Rotation-minimising frames with holonomy correction; tubes and ribbons |
| `Manifolds/HopfFibration.cs` + `Components/HopfFibrationEngine.cs` | Hopf fibers as linked tubes and a particle bundle |
| `Manifolds/StrangeAttractors.cs` + `Components/AttractorEngine.cs` | Lorenz, Rössler, Thomas, Halvorsen, Aizawa (RK4, fixed or adaptive) and Clifford / De Jong maps |
| `Manifolds/SeifertSurface.cs` + `Components/SeifertSurfaceBuilder.cs` | Seifert's algorithm on closed braids |
| `Implicit/ScrewDislocation.cs`, `Implicit/EscherSpace.cs` | The Escher step and the harder warps (inversion, Droste spiral, drift-free scroll), shared by `Implicits` and `EscherFields` |
| `Manifolds/EnneperPillar.cs` | The closed Enneper pillar and its lattice footprints (Square/Hex ends that tile a hall into one vault) |
| `Implicit/DualContouring.cs` + `Components/DualContourEngine.cs` | QEF dual contouring; keeps the corners surface nets rounds off |
| `Shaders/GeometryEngineWire.shader` | `GeometryEngine/Wire`: standalone URP wire with depth fade, gradient, pulse, diagonal toggle |
| `Tests/Editor/` | 70 NUnit tests; also runnable without Unity via `dotnet test Tools/EngineTests/Tests` |

`Hyperspace4DAxis` gained `RotationModel.Bivector` (default), which rotates through `Rotor4`;
`SequentialPlanes` keeps the old Givens chain.

---

## 3. Facts worth not rediscovering

- **`Cull Off` + opaque is why self-intersecting immersions work.** Klein bottle, Roman surface and
  the Clifford torus all pass through themselves; depth-tested opaque geometry resolves that
  correctly, transparent particles would need sorting.
- **`IndexFormat.UInt32` is mandatory everywhere here.** Unwelding costs 6 vertices per quad, so
  96 × 64 is already 36,864.
- **The quad diagonal draws unless suppressed.** Adding 1 to the bary component that vanishes along
  the shared diagonal keeps it in [1,2], so it never triggers. No extra vertex attribute.
- **`CurvedGeometryChamber` used to write index-space UVs** (`Vector2(s, z)`, so 0..sides and
  0..rings). Any shader multiplying UV by a density aliased into grain. It writes normalised UVs now,
  and `_UVScale` exists for meshes that do not.
- **`ParticleSystem` modules return by value.** `system.emission.enabled = false` does not compile;
  it needs a local first. This cost a round of CS1612 errors.
- **The SDF graph has no size control.** `FractalSdfParticles.vfx` sets size from a hard-coded
  Set-Size-over-Life curve and particle size is locked to a fixed fraction of the sculpture, because
  the SDF texture always spans the cloud's local unit cube. The only fix was exposing it in a copy.
- **Frenet frames flip** wherever curvature vanishes, which turns a swept ribbon inside out. The knot
  tube and ribbon use a reference-vector frame instead.
- **Signed powers are required** in the superellipsoid and supershape: `Mathf.Pow` on a negative base
  returns NaN and stretches the mesh to the horizon.
- **Boy's surface has interior poles.** The Bryant–Kusner denominator vanishes at |w| ≈ 0.7257,
  inside the domain. The map stays continuous but needs the epsilon guard or it emits NaNs.
- **TPMS fields are level sets, not distances.** The gradient magnitude varies, so marching normals
  and particle stick distances come out uneven unless normalised by |∇f|.
- **Orbits need sign changes on all four coordinates.** The 600-cell orbit once flipped only
  three, so any even permutation that moved the zero off `w` never negated the fourth coordinate:
  84 vertices and 60 edges instead of 120 and 720, silently.
- **A screw dislocation's period is the field's period in the coordinates being sheared.** Fields
  evaluated at π·frequency·p repeat every 2/frequency, not 2π/frequency; with the wrong one the
  staircase tears at the atan2 seam. Whole-number dislocations only.
- **A chamber node is shared by four quads.** Evaluate the surface per lattice node and scatter
  it into the six unwelded slots; evaluating per quad corner did the same work four times.
- **A 4-D extension must vanish at W = 0.** Adding cos w (or 3cos w) to a TPMS shifts its level set
  at W = 0; the rooms' Gyroid, P and Neovius were wrong surfaces until the engine cross-check.
- **One enum per name per namespace, across assemblies.** Two `Axis3`s in `PsychedelicLab.GeometryFX`
  compile separately and fail together.
- **(L, R) and (−L, −R) are the same 4-D rotation; (L, −R) is not.** Sign-align the pair, never
  one half, before slerping rotors.
- **`TunnelSparkleWire 1.mat` is a hand-tuned working material** assigned as the chamber's
  `chamberMaterial` in `ProceduralTunnelTester-2`. Not a stray duplicate — do not delete it.

---

## 4. Still not built

| Shape | Status / needs |
| --- | --- |
| Hopf fibration | **Built** — `Engine/Components/HopfFibrationEngine` |
| Attractors (Thomas, Halvorsen, Lorenz, …) | **Built** — `Engine/Components/AttractorEngine` (CPU, cached trajectory; a per-particle GPU integrator is still open) |
| Seifert surfaces | **Built** — `Engine/Components/SeifertSurfaceBuilder`, on closed braids |
| Morin surface / sphere eversion | Parameterisation must be sourced against reference images, not written from memory. Still open: sources were unreachable from the cloud session |
| Mandelbox **rooms** (camera inside) | A raymarcher. `ImplicitShapes.Mandelbox` already provides the DE |
| Penrose steps | `GitRepoKeijiro/Portals` is the honest route — a staircase whose top flight is a portal to the bottom closes the loop from any angle |
| Mandelbrot / Julia imagery | A compute shader to a RenderTexture, feeding `ParticleTexture` or the wire tint |
| GPU implicit extraction | Plan §2.3: compute-shader surface nets / marching cubes |
| Conway operator pipeline | Plan §3.3: refactor `polyhedronGenerator/scripts/operators` |

## 5. Unverified

The engine's math (§2a) is unit-tested (70 tests, runnable without Unity), but nothing in §2 or §2a
has rendered in the Editor yet. The parameterisations come from standard
formulations; the failure modes listed in §3 are the known ones, not observed ones. The only
measured numbers anywhere here are the original chamber's 12 × 48 grid and the Dekeract's 11,264
particles.
