# GeometryMode.md

**Scope: shapes and their movement. Nothing else.**

A Mode is the working set for one kind of work: the libraries it draws from, the folder
finished code lands in, and an explicit list of what is out of scope so a session can't
drift. If a file isn't named here, it isn't part of Geometry Mode.

Particles are **not** in this Mode — see "Handoff to Particle Mode" at the bottom.

---

## Work folder

**`Assets/_GeometryWork/`** — all finished geometry code goes here. Existing layout:
`Core/` (engines), `Shapes/` (individual shape builds), `ShaderSets/` (material catalogue),
`Materials/` (wire materials).

## Library roots — the four things this Mode is built out of

| Root | What it provides | Entry points |
|---|---|---|
| `Assets/GeometryAlgorithms/` | Computational geometry: convex hulls (2D/3D), Delaunay triangulation (2D/3D), Voronoi (2D/3D), extrusion | `Source/API`, `Source/Algorithms`, `Source/Core`; `Manual_GeometryAlgorithms.pdf` |
| `Assets/polyhedronGenerator/` | Platonic/Johnson solids + Conway operators — the generator for anything polyhedral | `PolyhedronGenerator.cs`, `MeshBuilder.cs`, `WireFrameMeshGenerator.cs`, `scripts/solids/`, `scripts/operators/` (Kis, Dual, Truncate, Gyro, Chamfer, Inset, Whirl, Quinto, Subdivide) |
| `Assets/ProceduralToolkit/` | Mesh construction toolkit — `MeshDraft` is the workhorse for building a mesh in code | `Runtime/MeshDraft.cs`, `MeshDraftPrimitives.cs`, `CompoundMeshDraft.cs`, `Runtime/Geometry/`, `Tessellator.cs` |
| `Assets/Amazing Assets/Curved World/` | The space bend itself (`Shaders/`, `Scripts/`) | **Must not move** — chamber shaders `#include` this path literally. Drive it through `CurvedWorldBridge`, never directly |

## 1. Shape engines

`Assets/_GeometryWork/Core/`

| File | Role |
|---|---|
| `CurvedGeometryChamber.cs` + `CurvedChamber.shader` | The main chamber: builds an **unwelded** mesh, draws barycentric wires, opaque with `Cull Off` so self-intersecting shapes read correctly |
| `ManifoldSurfaces.cs` + `ManifoldComboChamber.cs` | Parametric manifold bank — Klein, helicoid, torus knots, Calabi-Yau |
| `ImplicitShapes.cs` + `ImplicitSurfaceChamber.cs` | Implicit/SDF bank — gyroid, Barth, Goursat, Mandelbulb, Mandelbox, Lidinoid |
| `ShapeArsenal.cs` | Shape registry — where a new shape gets named and listed |
| `IWireGeometry.cs` | Interface every wire-drawn shape implements |
| `RingGeometry.cs`, `RingFractal.cs` | Ring and ring-fractal family |
| `NCube/` | N-dimensional cube/polytope vertex maths |

`Assets/_GeometryWork/Shapes/`

| File | Role |
|---|---|
| `ShapePrinter.cs` | Prints a shape into the scene |
| `ScherkTowerEngine.cs` | Scherk tower (`SCHERK-TOWER.md`) |
| `EnneperFoldReality.cs` + `EnneperStableGrid.shader` | Enneper fold (`ENNEPER-REALITY.md`) |

## 2. Movement — how a shape deforms, folds and bends

| File | Role |
|---|---|
| `Core/GeometryMorph.compute` | GPU morph between two shapes |
| `Core/GeometricMonsterMotion.cs` + `MonsterIsoField.compute` | Iso-field deformation ("monster" motion) |
| `Core/AccordionSettings.cs` | Accordion extension/compression |
| `Core/TunnelSdfField.compute` | Tunnel SDF field |
| `_EscherWorldManagement/Hyperspace/Hyperspace4DAxis.cs` | 4D rotation axis — the w-rotation that makes a 4D shape appear to move |
| `_EscherWorldManagement/Hyperspace/Hyper4DField.cs` | 4D field driving that projection |
| `_EscherWorldManagement/Hyperspace/KaleidoFold.cs` | Kaleidoscopic space fold |
| `_EscherWorldManagement/CurvedWorld/CurvedWorldBridge.cs` | **Single owner of the bend keywords at runtime.** Geometry's one sanctioned contact with the world warp |
| `_EscherWorldManagement/CurvedWorld/CurvedWorldAutomation.cs` + `UserCurvedControllerManager.cs` | Animating the bend over time (`CURVED-WORLD-AUTOMATION.md`) |

## 3. Escher rooms — architecture made of surfaces

`Assets/_EscherWorldManagement/Rooms/`: `EscherSurfaceBuilder.cs`,
`EscherMarchingTable.cs`, `EscherFields.cs`, `EscherField4D.hlsl`, `EscherRoom.cs`,
`EscherFluidSkin.cs`

These are surface extraction, so they belong to this Mode. Their materials already sit in
`_GeometryWork/ShaderSets/06_EscherRooms/`.

## 4. Shader sets — look up before building

`Assets/_GeometryWork/ShaderSets/` — `01_Chambers`, `02_Stretched`, `03_Implicit`,
`04_Projected4D`, `05_Edges`, `06_EscherRooms`. *(`07_Swarms` is Particle Mode.)*

Shared wire code: `EscherWireCore.hlsl`, `EscherShapeWire.shader`,
`EscherLayerWire.shader`, `EscherHyperWire.shader`.
Shared shaders: `CurvedGeometryAccent.shader`, `GeometryAdditive.shader`.
Wire materials: `_GeometryWork/Materials/Wire_*`.

## 5. Existing inventory
`Assets/_GeometryWork/GEOMETRY-MAP.md` — includes the fact that `FractalMeshFactory`
already bakes Menger/Sierpinski/Mandelbulb. Check it before building a shape from scratch.

---

## Handoff to Particle Mode
These are geometry-adjacent and will be that Mode's problem, because they reach into many
shader groups and the Keijiro repos. Listed so they are not touched from here:

`Core/DekeractTetraSwarm.cs` · `Core/WireParticleSwarm.cs` · `Core/GeometryParticleCombo.cs` ·
`Core/SdfParticleRig.cs` + `SdfSparkleParticles.vfx` · `Core/TunnelSparkleParticles.cs` +
`TunnelSparkleWire.shader` · `ShaderSets/07_Swarms/` · `Materials/Particles_*` ·
`Assets/GeometryFXParticles/` · `Assets/_ParticleManagement/` ·
`Assets/_TheCrazyShapes/` SDF-particle prefabs · the Metavido / VFX Graph work ·
`Assets/Fast & Simple SDF Visual Effects for URP/`

## Out of scope entirely
`_EscherWorldManagement/Camera/` (camera direction) ·
`_EscherWorldManagement/Portals/` (render compositing) ·
`_EscherWorldManagement/WorldTransform/` (`TinyPlanetPass` is the logo sting,
`MatrixWallFader` is a transition) · post-processing and shader packs.

## Two rules that govern shape work
1. Surfaces use the **unwelded-mesh + barycentric-wire** path, opaque with `Cull Off` —
   which is why self-intersecting immersions render. Weld-based mesh optimisation breaks it.
2. Shapes with no inside/outside do **not** go through the SDF path.
