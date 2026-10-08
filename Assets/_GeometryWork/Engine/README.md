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

`Quad`, `Triangle`, `Grid(points, columns, rows)`, `Apply(mesh)`; buffers are reused, so rebuilding
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
| `ScrewDislocation`, `Axis3` | `Implicit/ScrewDislocation.cs` | The Escher step as one shared field-space transform; `TpmsPeriod(frequency)` |
| `DualContouring`, `IScalarField` | `Implicit/DualContouring.cs` | QEF dual contouring with a truncated eigen pseudo-inverse; `solveQef = false` gives surface nets for comparison |
| `Implicits`, `ImplicitSettings`, `ImplicitShape` | `Implicit/ImplicitShapes.cs` | The 13 implicit fields (moved here), now with `dislocation`, `dislocationAxis`, `dislocationCore` |
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
| `DualContourEngine` | Dual Contour Surface | Any `ImplicitShape` with the screw dislocation; DC or surface nets; animated re-extraction on a timer |

`Shaders/GeometryEngineWire.shader` (`GeometryEngine/Wire`) is the standalone material: constant
screen-width AA wire, `_ShowDiagonals`, near/far depth fade, dual-tone gradient along U or V, and a
travelling lattice pulse with a script-driven `_PulsePhase` for beat sync. The chamber materials
also work on these meshes.

## Tests

- In Unity: Test Runner ▸ EditMode ▸ `GeometryEngine.Tests`.
- Without Unity: `dotnet test Tools/EngineTests/Tests` compiles this folder against a small
  UnityEngine stand-in and runs the same tests (70 at the time of writing).

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

## Not done yet

- **Morin surface / sphere eversion (plan 1.4).** The plan and GEOMETRY-MAP both require the
  parameterisation to be sourced and checked, not written from memory, and the reference sources
  (arXiv, Wikipedia, mathcurve, Berkeley) were unreachable from the session that built this.
  `TopologyReport` already gives the checks it will need: χ = 2, orientable, no NaN.
- **GPU extraction (plan 2.3)** and **Burst/Jobs** paths — not started.
- **Conway operator pipeline (plan 3.3)** — not started; `polyhedronGenerator` is untouched.
- **Calabi–Yau (plan 1.5)** was already implemented (`Manifolds.CalabiYau`, `CurvedGeometryChamber`
  mode `CalabiYau`, degree 2–7 with n² patches and a projection angle); left as is.
- The new shader features live in `GeometryEngine/Wire`; the existing chamber shaders are unchanged.
- Nothing here has been rendered in the Unity Editor yet. The math is tested; the components,
  editor menu and shader have been compiled (C#) or reviewed (HLSL) but not run in Unity.
