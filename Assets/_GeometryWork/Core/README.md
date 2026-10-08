# Geometry particle combo

Targets: the existing `Assets/GeometryFXParticles/DemoScene.unity` and matching `Assets/Scenes/ProceduralTunnelTester.unity` scene.

The scene installer runs once after script import **only if either supported scene is active**, outside Play mode, and retries after exiting Play mode. It retains the original particle library as an inactive object and saves the upgraded scene. If import occurs while another scene is active, open one of the targets and run **Tools > Geometry FX > Upgrade Current Demo**. It will not duplicate an existing combo root.

Select **Geometry Particle Combo** for controls. Play to preview. The compact controls toggle automatic morphing, density, Flowpath geometry, symbol accents and the optional parallax panel. Inspector controls include both morph endpoints, transition curve, surface thickness, fractal depth and 64/96/128-cubed field resolution. The chamber component controls twist, horizontal/vertical bend, wire opacity and slow bend animation. Curvature is clamped to ±30 and horizontal bend to ±15.

The **Curved Controller** scene object uses the same `CurvedWorldBridge`, `UserCurvedControllerManager` and `CurvedWorldAutomation` components as the nightclub workflow. Its slot Inspector provides Previous/Next and a selected-slot slider in Play mode. The chamber and Flowpath now receive bend ID 1 through owned copies of their URP materials. The chamber creates a controller only for edit preview; the shared bridge drives rendering during Play. Its old local bend fields remain serialized only for scenes that have not yet received the shared bridge.

Default slots are Off, Gentle Chamber Twist, Cylinder Rolloff, Cylinder Tower, Classic Runner, Big Tetrahedral Twist and Little Planet. Automation has a gentle continuous four-bar sine and an optional one-bar twist moment. Add a `MasterClock` reference for actual music sync; without one, automation runs at its inspector fallback BPM. The shape and camera remain independent of bend-slot selection.

## Asset audit and integration

| Asset | Finding / use |
| --- | --- |
| GeometryFXParticles | Two custom built-in particle shaders plus legacy additive/environment materials, not an HDRP-only pack. Custom shaders ported to URP while keeping shader names, material GUID references, texture/tint properties and stroke alpha-mask behavior. Installer converts legacy materials throughout this package through Unity's material API. Original symbol prefab provides restrained accents. |
| polyhedronGenerator | Uses the installed tetrahedron, dodecahedron and octahedron generators to derive convex face planes for the particle field. Topology is generated at initialization, not every frame. |
| Fast & Simple SDF Visual Effects for URP | Existing URP particle backend. Reuses the project's `FractalSdfParticles.vfx` fine-particle graph, also used by the 4D/SDF system. No HDRP graph substitution. |
| Stage4D / Shape4DSdfParticles | Established live 3D texture-to-particle workflow. The new component follows its texture normalization and property bindings without depending on Stage4D's global visibility or scene layer manager. |
| GeometryAlgorithms | Installed triangulation/Voronoi API; useful for future irregular cell topology. Not added to the rendering loop: regular solids already come from polyhedronGenerator. |
| Shapes FlowPath | Includes URP prefabs and a shared tunnel mesh collection. Reuses a tunnel mesh as two normalized accent shells with a dedicated Curved World-compatible URP material. Does not instantiate the entire 46-tunnel gallery or its HDRP-dependent MusicAnimator. |
| Parallax 3D Free | Already uses URP includes, but had an incorrect render-pipeline tag. Tag corrected to `UniversalPipeline`. Its room prefab is a switchable depth panel behind the core. Its shader is designed for a planar room illusion; this is **not** a nonlinear ray-marched chamber interior. |
| Curved World | Procedural chamber and Flowpath accents use the installed TwistedSpiral Z deformation on bend ID 2, separate from the stage bridge's ID 1. Shader-displaced renderer bounds are expanded to prevent straight-mesh culling. |
| WorldGridScan (confirmed by user) | Chamber triangle edges are driven by the existing `WorldGridScan` component and `WorldGridScan.hlsl`, initially using Weave. Guide-line visibility, scan patterns and the existing beat binding remain available. This is the requested scan system, not a new Compass Navigator feature. |
| Particle Plexus / Force Fields | Available alternatives, but not enabled here: they target ParticleSystem motion, whereas the centerpiece is a GPU VFX Graph consuming a distance volume. |

## Dekeract tetrahedron centerpiece

The demo upgrade now adds **Dekeract Tetrahedron Swarm**, reusing `polyhedronGenerator/prefabs/urp/wireframeParticle.prefab` and the installed tetrahedron mesh generator. A rotating 10-dimensional cube is projected into 3D: 1,024 vertices and 5,120 edges, with two moving tetrahedron particles per edge by default (11,264 mesh particles total). It morphs between a recognizable cube arrangement and its higher-dimensional projection. Particles assemble from a surrounding cloud on Play, with scatter/reassemble and projection controls in the demo panel. The SDF core is a rounded cube frame surrounding nested opposing tetrahedron shells.

The installer also upgrades an existing combo root once, without recreating the chamber or resetting its controls. Editor preview shows a stationary particle sculpture; motion starts in Play. This is a projected 10D structure with tetrahedral particles, not a claim that the 3D view displays all ten dimensions independently.

## Morph and rendering behavior

Seven SDF forms: tetrahedron, star tetrahedron, dodecahedron, Sierpinski-style tetrahedral fractal, three interlocking rings, octahedron and tetrahedron cube. The compute field blends signed-distance estimates and particles chase the changing surface; interpolation can change topology and is not an exact distance field at every intermediate shape. This is a volumetric particle cloud, not a full volumetric light-scattering renderer.

Default field resolution is 96 cubed, with 24,000 particles spawned per second and 1.8–3.2 second lifetimes. GPU cost and appearance still require checking in the actual Editor; high detail is optional. Shared materials/meshes are not changed per frame. Runtime texture, cloned compute shader, generated chamber mesh/material and child objects are released on disable.

## Verification status

Runtime and installer C# compiled against the project's current Unity assemblies, including the shared Curved World bridge hookup. Unity confirmed the combo/chamber upgrade and dekeract addition saved to the existing demo (`Temp/GeometryComboSetup.txt` and `Temp/DekeractSetup.txt`). The newer shared-controller hookup awaits Editor import/save confirmation (`Temp/GeometryCurvedSetup.txt`). Rendered appearance and performance remain unverified. No replacement scene or test project was created.

After import, check the Console for shader errors, Play through the six forms, toggle each depth layer, and inspect original library particles for remaining pink materials. This document must not be read as proof of a completed visual check.

## Particle size control and SDF variants

`FractalSdfParticles.vfx` (shared with Stage4D) sets particle size from a hard-coded
**Set Size over Life** curve and exposes only nine properties, none of them size. Particle
size there is locked to a fixed fraction of the sculpture, because the SDF texture always
spans the cloud transform's local unit cube — scaling one scales the other. So size could
not be controlled from C# at all.

`Combo/SdfSparkleParticles.vfx` is a separate copy with two more exposed parameters:

| Parameter | Type | Drives |
| --- | --- | --- |
| `ParticleSize` | AnimationCurve | the Set Size over Life curve that used to be fixed at 0.0026 → 0.0062 |
| `ParticleTexture` | Texture2D | the sprite each particle draws with |

Stage4D's original graph is untouched.

`SdfParticleSettings` (in `Combo/SdfParticleRig.cs`) is one serializable block holding size
scale, size curve, sprite, spawn rate, lifetime range, stick distance/force, attraction
force/speed and palette, plus named starting points (Fine Mist, Sparkle, Embers, Dense,
Chunky) via the `applyPreset` dropdown. `SdfParticleRig` owns the child GameObject and
VisualEffect and pushes the block every frame; every write is guarded, so a graph without
the newer parameters still runs and the component says so in its status line.

**Branching a variant:** duplicate `SdfSparkleParticles.vfx`, change what you want in the
VFX Graph window, and drop the copy into the `graph` field. No ScriptableObject, no
Resources folder — the variant lives on the GameObject.

`GeometryParticleCombo` now carries a `particleSettings` block and a particle-size slider in
its on-screen panel. Its older `particlesPerSecond` / `surfaceThickness` / `palette` fields
still work and fold into the block on enable, so existing scenes keep their values. Run
**Tools > Geometry FX > Give the combo particle size control** to point an existing scene's
combo at the new graph.

## Tunnel sparkle skin

`TunnelSparkleParticles` gives a Shapes FlowPath tunnel the combo's look without editing
anything it already had:

- **Wire skin** — `Combo/TunnelSparkleWire.shader` / `.mat`, a new shader kept separate from
  `CurvedChamber` and `CurvedGeometryAccent`. Additive lattice wire with screen-constant line
  width, two layers of twinkling hashed sparkle points, `WorldGridScan` recolouring from the
  cyan wire colour to the amber spark colour, and Curved World bend ID 1 so it bends with the
  rest of the stage. The material is cloned per instance and the renderers' original
  materials are put back on disable, so `Flowpath_Cyan` and the shared tunnel mesh are
  unchanged.
- **Sparkle cloud** — `Combo/TunnelSdfField.compute` bakes a hollow ribbed tube SDF using the
  same storage convention as `GeometryMorph.compute` (distance ÷ cube side, texture spanning
  the local unit cube), which the sparkle graph then reads. The tube is fitted from the
  renderer bounds on build; radius, wall thickness, half length, ring depth/frequency,
  flute count/depth and twist are all exposed.

Run **Tools > Geometry FX > Sparkle the tunnel** with the tunnel object(s) selected, or with
none selected to catch every `Tunnel_*` in the open scene. It assigns the material, compute
and graph, finds the scene's `WorldGridScan` and `CurvedWorldBridge`, and marks the scene
dirty — it does not save for you, and it will not add a second component.

### Not yet used: `com.unity.demoteam.mesh-to-sdf`

The project has Unity DemoTeam's `MeshToSDF` / `SDFTexture` (from `GitRepoKeijiro/`), which
rasterises a real mesh into an SDF 3D RenderTexture on the GPU from the vertex/index buffers,
with no CPU-readable mesh needed. That would make the sparkle cloud hug the tunnel's actual
geometry instead of a fitted tube, and `Scenes/Keijiro/SdfVfxSamples/Blocks.vfx` is a working
in-project example of VFX Graph conforming particles to one. It is not wired up here because
`MeshToSDF` writes distances in its own units while `SdfSparkleParticles` expects
distance ÷ cube side, and that factor has not been checked against a running Editor.

### Unverified

Rendered appearance, performance and the two new VFX Graph parameters have not been opened in
the Editor. The graph surgery was done in YAML against the existing `ParticleColor` parameter
as a template; check the Console and the SdfSparkleParticles graph's blackboard first.

## Fractal accordion rings

`FractalRingChamber` is a tunnel of rings built the same way `CurvedGeometryChamber` is built —
unwelded triangle mesh, barycentric wire in UV1, Curved World bend ID 1, `WorldGridScan` tint,
material cloned per instance and registered with the shared bridge. It works with either
`CurvedChamber.mat` or `TunnelSparkleWire.mat`.

Assign its `chamber` field and it reads radius, length, scan and bend from an existing Curved
Geometry Chamber, so the rings sit concentric with it and bend with it. Nothing on that chamber is
written to.

| Group | Controls |
| --- | --- |
| Accordion | `accordion` (fold depth), `bellows` (folds along the tunnel), `extend` (stretch), `accordionOctaves` + `accordionGain` (folds inside folds) |
| Ring profile | `profileA` / `profileB` / `profileMorph` across Circle, Polygon, Superellipse, Star, Gear, plus `profileMorphAlong` so the cross-section changes as you travel the tunnel |
| Fractal detail | `fractalOut` master dial, `fractalDepth`, `fractalBaseFrequency`, `fractalLacunarity`, `fractalGain`, `fractalTwistAlong` |
| Rings and links | `bandFraction` (1 = solid tube, lower = separate ring bands), `connectors` + `connectorWidth` (the struts linking ring to ring) |
| Motion | `animate`, `cycleSeconds`, `extendMin`, `breathe`, `spin` |

Context-menu shortcuts on the component: Collapse accordion, Stretch accordion, Fractal out,
Clean rings.

The bellows is a real one: a spacing weight is computed per ring interval and then cumulatively
summed, so rings bunch where the weight is small rather than sliding along a fixed grid.

Two implementation notes worth keeping:

- **`IndexFormat.UInt32` is mandatory.** Unwelding costs 6 vertices per quad, so 48 sides × 64
  rings is already 18,432 vertices and realistic settings go well past 65535.
- **Quad diagonals are hidden by default.** The wire draws wherever the smallest barycentric
  component nears zero, which includes the diagonal splitting each quad. Adding 1 to the component
  that vanishes along that diagonal keeps it in [1,2] so it never draws — no extra vertex
  attribute needed. Turn `hideQuadDiagonals` off to get the chamber's triangle look back.

Run **Tools > Geometry FX > Add fractal accordion rings**. It lands on the scene's Curved Geometry
Chamber if there is one (linked automatically), otherwise on the selection.

## TunnelSparkleWire graininess fix

The first version of `TunnelSparkleWire` looked grainy on the chamber. Cause:
`CurvedGeometryChamber` writes **index-space UVs** (`CurvedGeometryChamber.cs:84` passes
`new Vector2(s, z)`), so UV0 runs 0..sides and 0..rings, not 0..1. The shader then multiplied
those by its lattice and sparkle densities, producing hundreds of cells per pixel, which aliases
into noise.

Three changes:

- `_UVScale` — set it to `(1/uRange, 1/vRange)` for index-space UVs. `FractalRingChamber` writes
  normalised UVs and sets this to `(1,1)` itself, and the FBX tunnel meshes are already normalised.
- **Sparkles sit on lattice nodes, not in cells.** One candidate per lattice intersection,
  gated by `_SparkleSparsity` (0.82 default, so most nodes stay dark) and positioned by
  `_SparkleSnap`. That is what makes it sparse and deliberate instead of a dusting of grain.
- **`_MinCellPixels` sub-pixel fade.** Both the wire and the sparkles fade out before their grid
  goes below this many pixels. Without it a dense grid saturates every fragment and reads as grey
  haze at distance.

`_LatticeU` / `_LatticeV` are now absolute line counts over the whole surface.
`FractalRingChamber` drives them from `sides / latticeDivisor` and `rings / latticeDivisor`, so
wire lines and sparkles land on real ring vertices.

## Manifold chamber — the basic shape set

`CurvedManifoldChamber` + `ManifoldSurfaces.cs` are step 1 of `GeometricFractalPlan.md`: the
chamber's rendering technique (unwelded mesh, barycentric wire in UV1, Curved World bend ID 1,
`WorldGridScan` tint, cloned material on the shared bridge) with the shape pulled out so it is a
dropdown instead of a hard-coded cylinder.

Shapes in `ManifoldSurface`:

| Shape | Notes |
| --- | --- |
| Cylinder | the original chamber shape, kept so the generalisation can be checked against what it replaced |
| Torus | `radius` major, `minorRadius` minor |
| Sphere | has poles — see below |
| MobiusStrip | `halfTwists` 1 = Möbius, 2 = full-twist annulus, 3 = triple |
| Superellipsoid | `squareness` / `roundness` sweep rounded cube → sphere → octahedron → star |
| TorusKnotTube | `(knotP, knotQ)`; `(2,3)` is the trefoil |
| Helicoid | minimal surface; the only shape open in **both** u and v |
| KleinBottle | figure-8 immersion |
| Duocylinder | 4-D: `D² × D²`, two solid tori glued along a ridge |
| CliffordTorus | that ridge on its own |

`from`, `to` and `morph` crossfade any pair, because every shape is a grid on the unit square and
the vertices correspond one for one. `autoCycle` walks the whole list. Context menu has
Show 'from' only / Show 'to' only / Next shape pair.

4-D controls (Duocylinder, CliffordTorus): `projection` is Stereographic / Perspective /
Orthographic, `simpleRates` rotates in the xy and zw planes — **equal rates give the isoclinic
inside-out turn** — and `mixedRates` rotates in xz and yw, which reads as tumbling through itself.
`cell` picks Ridge / CellA / CellB / BothCells and `cellFill` is how far up the cell wall to draw.

Four things that needed handling, all of them called out in the plan beforehand:

- **`IndexFormat.UInt32`.** 96 × 64 unwelded is 36,864 vertices, past the 16-bit limit.
- **Signed powers in the superellipsoid.** `Mathf.Pow` on a negative base returns NaN, which would
  stretch the mesh to the horizon. `SignedPow` fixes it.
- **Poles.** Sphere and Superellipsoid collapse a whole grid ring to a point, making zero-area
  triangles where `fwidth(bary)` is meaningless and the wire flickers. `v` is inset by half a cell
  when either endpoint of the morph has poles.
- **Wrap vs open.** A wrapping axis divides by the cell count so the last column lands back on the
  first; an open one divides by `count - 1` so the grid reaches the far edge exactly. The helicoid
  is the only shape open in u.

Also: the Clifford torus is the duocylinder's **ridge**, so both come from one function with a
`cell` selector rather than two implementations.

Run **Tools > Geometry FX > Add manifold chamber**. It links to the scene's Curved Geometry
Chamber if there is one and borrows its radius, length, scan and bend.

## Two layers per shape: wire + swarm

The wire mesh is one layer. The other is `WireParticleSwarm` — the Dekeract's rendering riding any
`IWireGeometry`, which both `FractalRingChamber` and `CurvedManifoldChamber` implement. Any shape
added later gets the particle layer with no new code; only the density and clustering need tuning
per shape.

Same mechanisms as `DekeractTetraSwarm`, because those are what make it land: one `ParticleSystem`
in mesh render mode with a generated tetrahedron, node particles on the grid, travellers sliding
with a travelling phase, scatter/reassemble to a Fibonacci sphere, `1-exp(-regroupSpeed·dt)`
smoothing, cyan→gold palette, per-particle tumble.

**What is new: `fractalLevels`.** Each base sample spawns a recursive cluster of children through a
4-map IFS in the surface tangent frame — the child index is read as a base-4 address, each digit
offsetting toward one of four corners at a geometrically shrinking scale. So the particle cloud is
a genuinely self-similar set of clumps rather than an even dusting. `clusterRatio` below 0.5 leaves
gaps at every level, which is what makes the recursion visible; `childSizeFalloff` shrinks deeper
children so the clumps have hierarchy. Cost is `4^levels` per base sample, so level 2 is 16× and
level 3 is 64× — `maxParticles` is a hard cap and `Status` reports when it is hit.

Both menu items now attach the swarm automatically, using
`polyhedronGenerator/prefabs/urp/wireframeParticle.prefab` and `Combo/Dekeract_Particles.mat`.

## The ring fractals are now real recursions

The first version's "fractal" was a sum of cosines. With `gain × lacunarity > 1` that is a
Weierstrass–Mandelbrot series and so fractal in the limit, but at depth 3 and 22% amplitude it read
as a mild wobble rather than structure. `RingFractal.cs` replaces it with constructions whose
self-similarity is visible at affordable depths:

| Function | What it does |
| --- | --- |
| `CantorPosition` | Ring spacing through an IFS (`f0 = s·x`, `f1 = s·x + (1−s)`). With `s < 0.5` there is a gap at every level, so rings bunch into clusters, clusters into super-clusters. `s = 1/3` is the classic middle-thirds set. **This is the accordion that genuinely subdivides** |
| `KochRadius` | Triangle-wave series instead of cosine, so each octave adds sharp corners. Order 3 already reads as a Koch edge rather than a ripple |
| `ApollonianScale` / `SubdivideGap` | Recursively inserted child rings in the gaps, with their nesting level available for sizing |

New controls on `FractalRingChamber`: `cantorSpacing` / `cantorDepth` / `cantorRatio` for the
clustering, `fractalAmplitude` and a depth now up to 10 for the edge, and `nestDepth` / `nestRatio`
for nested child rings.
