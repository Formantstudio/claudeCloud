# Master Execution Plan: Procedural Geometry & Manifold Engine

This document is the execution contract for the Claude Cloud session. It provides the exact mathematical formulations, algorithmic specifications, architectural requirements, and task checklist to turn this codebase into a state-of-the-art procedural geometry library for Unity URP.

---

## Session Objectives & Guardrails

1. **Pure Algorithmic Geometry:** Keep all code focused strictly on computational geometry, parametric equations, implicit surface extraction, n-dimensional topology, and wireframe shaders. No proprietary studio IP, audio stems, or cinematic timeline dependencies.
2. **Deterministic & High-Performance:** Zero allocations in `Update()`. Target 60–120 FPS generation where possible via Burst/Jobs, SIMD, or compute shaders.
3. **Preserve Wire Pipeline Invariants:**
   - Always unweld quad meshes (6 unique vertices per quad) when targeting the barycentric wireframe shader.
   - Barycentric coordinates in UV1: `(1,0,0)`, `(0,1,0)`, `(0,0,1)` for triangle 1, and `(1,0,0)`, `(0,1,0)`, `(0,0,1)` (or quad-diagonal-suppressed barycentrics) for triangle 2.
   - Set `mesh.indexFormat = IndexFormat.UInt32` whenever vertex count exceeds 65,535.
   - Always set `mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200f)` to avoid GPU displacement culling.
   - Opaque rendering with `Cull Off` so self-intersecting manifolds (Klein, Boy's, Roman) resolve naturally through depth testing.

---

## Phase 1: Unbuilt Manifolds & Curve Bundles (Math Completion)

Implement the missing mathematical systems detailed in `GEOMETRY-MAP.md` and `GeometricFractalPlan.md`:

### 1.1 Hopf Fibration Engine (`HopfFibrationEngine.cs`)
- **Math:** Map the 3-sphere $S^3 \subset \mathbb{R}^4$ to the 2-sphere $S^2$ via the Hopf map:
  $$h(z_0, z_1) = (2 z_0 \bar{z}_1, |z_0|^2 - |z_1|^2)$$
  For each point $(\theta, \phi)$ on $S^2$, the preimage is a great circle on $S^3$.
- Stereographically project these circles from $S^3$ to $\mathbb{R}^3$:
  $$X = \frac{x_1}{1 - x_4}, \quad Y = \frac{x_2}{1 - x_4}, \quad Z = \frac{x_3}{1 - x_4}$$
- The preimages form a family of mutually linked Villarceau circles nested on coaxial tori.
- Provide two extraction modes:
  1. **Swept Wire Tubes:** Generate unwelded tube meshes around $N$ chosen fiber circles.
  2. **Particle Bundle:** Swarms of edge-travelling particles orbiting the Villarceau circles with phase-shifted velocity.

### 1.2 Strange Attractor Suite with Adaptive RK4 (`AttractorEngine.cs`)
- Implement a high-performance Runge-Kutta 4th order (RK4) integrator for continuous chaotic dynamical systems:
  - **Lorenz System:** $\dot{x} = \sigma(y - x), \quad \dot{y} = x(\rho - z) - y, \quad \dot{z} = xy - \beta z$
  - **Rössler Attractor:** $\dot{x} = -y - z, \quad \dot{y} = x + ay, \quad \dot{z} = b + z(x - c)$
  - **Thomas Attractor (Cyclically Symmetric):** $\dot{x} = \sin(y) - bx, \quad \dot{y} = \sin(z) - by, \quad \dot{z} = \sin(x) - bz$
  - **Halvorsen Attractor:** $\dot{x} = -ax - 4y - 4z - y^2, \quad \text{cyclic in } y, z$
  - **Aizawa Attractor:** $\dot{x} = (z - b)x - dy, \quad \dot{y} = dx + (z - b)y, \quad \dot{z} = c + az - \frac{z^3}{3} - (x^2 + y^2)(1 + ez) + f z x^3$
  - **Clifford / De Jong Attractors:** 2D/3D iterated map orbits.
- Generate continuous ribbon meshes or curve trails with smooth Frenet-Serret / Bishop parallel transport frames.

### 1.3 Seifert Surface Builder (`SeifertSurfaceBuilder.cs`)
- Implement Seifert's algorithm for knots and links:
  1. Given a knot parameterized in 3D (e.g., $(p, q)$ torus knot, figure-eight knot, Whitehead link), compute planar regular projection.
  2. Form oriented Seifert circles from link crossings by smoothing crossings in an orientation-preserving way.
  3. Span topological disks over each Seifert circle at staggered heights.
  4. Connect discs at each crossing with half-twisted rectangular bands.
- Emit a single orientable two-sided unwelded quad mesh with barycentric UVs.

### 1.4 Morin Surface & Sphere Eversion (`MorinSurface.cs`)
- Provide the four-lobed immersion of the sphere representing the midpoint of sphere eversion (turning a sphere inside out without creases or tears):
  - Parametric formulation of Bernard Morin / François Apéry.
  - Expose eversion parameter $t \in [0, 1]$ smoothly morphing from standard sphere through Morin surface to reversed sphere.

### 1.5 Calabi-Yau 3-Fold Slice Enhancement
- Generalize `ManifoldSurfaces.cs` Calabi-Yau Fermat quintic:
  $$z_1^5 + z_2^5 = 1 \quad (z_1, z_2 \in \mathbb{C})$$
- Provide real 2D slices projected into $\mathbb{R}^3$, exposing the complex phase angle $\alpha$ and quintic rotation parameter $k \in \{0, 1, 2, 3, 4\}$.

---

## Phase 2: Surface Extraction & Escher Architecture

### 2.1 Dual Contouring Engine (`DualContourEngine.cs`)
- Current surface extraction uses naive surface nets (`ImplicitSurfaceChamber.cs`) which rounds out sharp edges.
- Implement **Dual Contouring** using Hermite data (exact surface points and surface normals $\nabla f$):
  - Solve the Quadratic Error Function (QEF) per cell via Singular Value Decomposition (SVD) or pseudo-inverse:
    $$\text{QEF}(x) = \sum_{i} ((x - p_i) \cdot n_i)^2$$
  - Accurately captures both smooth minimal surfaces (Gyroids) AND sharp fractal geometry (Menger Sponge, Mandelbox corners, Polyhedral rooms) without facet rounding.

### 2.2 Complete Escher Screw Dislocation Integration
- Standardize the screw dislocation into the unified implicit pipeline:
  $$q = \text{RotateToAxis}(p, \text{axis})$$
  $$\text{azimuth} = \text{atan2}(q.y, q.x)$$
  $$\text{period} = \frac{2\pi}{\text{frequency}}$$
  $$q.z \mathrel{+}= \text{dislocation} \cdot \text{period} \cdot \frac{\text{azimuth}}{2\pi} \cdot \text{SmoothStep}\left(0, 1, \frac{r}{\text{core}}\right)$$
  $$p' = \text{RotateFromAxis}(q, \text{axis})$$
- Verify seamless lattice alignment across:
  - Gyroid: $\sin(x)\cos(y) + \sin(y)\cos(z) + \sin(z)\cos(x) = 0$
  - Schwarz P: $\cos(x) + \cos(y) + \cos(z) = 0$
  - Schwarz D: $\cos(x)\cos(y)\cos(z) - \sin(x)\sin(y)\sin(z) = 0$
  - Neovius: $3(\cos(x) + \cos(y) + \cos(z)) + 4\cos(x)\cos(y)\cos(z) = 0$
  - Lidinoid: TPMS with hexagonal symmetry.

### 2.3 Compute Shader Surface Nets / Marching Cubes
- Upgrade `ImplicitSurfaceChamber` to optionally run on the GPU via compute shader:
  - Pass grid dimensions and field parameters to compute shader.
  - Evaluate implicit field in parallel across $64^3$ or $128^3$ voxels.
  - Emit vertices and indices directly into `GraphicsBuffer` (DrawProceduralIndirect) or copy back to `Mesh`.

---

## Phase 3: N-Dimensional Polytopes & Lie Algebra

### 3.1 True SO(4) Double Rotation Algebra
- Replace sequential Euler-style 4D rotations in `NCubeSwarmBase.cs` and `Hyper4DField.cs` with proper $SO(4) \cong (SU(2) \times SU(2)) / \mathbb{Z}_2$ isoclinic double rotations:
  - Left isoclinic rotation: rotates planes $xy + zw$ at rate $\omega_L$.
  - Right isoclinic rotation: rotates planes $xy - zw$ at rate $\omega_R$.
  - Any 4D rotation decomposes into one left and one right isoclinic rotation.
  - Eliminates gimbal lock and provides uniform geodesic paths on $S^3$.

### 3.2 Regular 4-Polytopes Suite (`Polytope4DLibrary.cs`)
- Fully implement all 6 convex regular 4-polytopes:
  1. **5-cell (Pentachoron):** 5 vertices, 10 edges, 10 faces, 5 cells.
  2. **8-cell (Tesseract):** 16 vertices, 32 edges, 24 faces, 8 cells.
  3. **16-cell (Hexadecachoron):** 8 vertices, 24 edges, 32 faces, 16 cells (dual of Tesseract).
  4. **24-cell (Icositetrachoron):** 24 vertices, 96 edges, 96 faces, 24 cells (self-dual, exceptional symmetry).
  5. **120-cell (Hecatonicosachoron):** 600 vertices, 1200 edges, 720 faces, 120 dodecahedral cells.
  6. **600-cell (Hexacosichoron):** 120 vertices, 720 edges, 1200 faces, 600 tetrahedral cells (dual of 120-cell).
- Dynamic stereographic and orthographic projection to 3D with edge-travelling swarms.

### 3.3 Conway Polyhedron Operator Pipeline
- Refactor `polyhedronGenerator/scripts/operators` into a clean, standalone functional geometry pipeline:
  - Input: arbitrary closed manifold mesh $(V, E, F)$.
  - Operators: Dual ($d$), Ambo ($a$), Truncate ($t$), Kis ($k$), Snub ($s$), Gyro ($g$), Chamfer ($c$), Whirl ($w$), Quinto ($q$).
  - Support chaining: e.g., $k(t(\text{Cube}))$, $g(d(\text{Dodecahedron}))$.

---

## Phase 4: URP Shaders & Wireframe Polish

### 4.1 Quad Diagonal Suppression
- In quad-based unwelded meshes, each quad consists of vertices:
  - Triangle 1: $v_0, v_1, v_2$
  - Triangle 2: $v_0, v_2, v_3$
- The shared diagonal edge is between $v_0$ and $v_2$.
- By setting the barycentric coordinate on the opposite vertex to a value $\ge 2.0$, screen-space derivatives never reach zero on the diagonal.
- Add an explicit inspector toggle: `ShowDiagonals (bool)` in wireframe shaders and mesh generators.

### 4.2 Advanced Wireframe Shader Features
- Screen-space constant thickness with sub-pixel antialiasing (`fwidth`).
- Depth fade: soften wire opacity as geometry approaches the camera near plane or recedes into the distance.
- Dual-tone gradient: map along $U$ or $V$ parameter coordinates, blending surface flow with edge intensity.
- Emissive lattice pulse: beat/time-synced running glow waves traversing UV lattice coordinates.

---

## Phase 5: Modular Packaging & Math Verification

1. **Assembly Definitions:**
   - `Assets/_GeometryWork/GeometryEngine.Runtime.asmdef`
   - `Assets/_GeometryWork/GeometryEngine.Editor.asmdef`
   - Ensure clean compilation with zero dependencies on studio-specific modules.
2. **Automated Unit Tests:**
   - Euler Characteristic Verification: Verify $V - E + F = \chi$ for all generated closed polyhedra and topological surfaces.
   - Non-NaN and Pole Safety Checks: Guarantee no mathematical domain poles (Boy's surface, supershapes, minimal surfaces) emit NaN or Infinity under any valid parameter input.
   - Normal Consistency: Ensure all computed normals have unit length and point outwards or consistently with surface orientation.

---

## Execution Checklist for Claude Cloud

Status after the first cloud session. Details, API and test list: `Assets/_GeometryWork/Engine/README.md`.

- [x] **Step 1:** Audit all existing scripts in `Assets/_GeometryWork/Core/`, `Assets/_EscherWorldManagement/`, and `Assets/polyhedronGenerator/`.
  Found and fixed: the 600-cell built 84 of its 120 vertices (sign flips on three coordinates only);
  the Escher screw dislocation used period 2π/f where the fields repeat every 2/f, tearing the seam.
  Calabi–Yau (§1.5) was already implemented. Pure files (`IWireGeometry`, `ImplicitShapes`) moved into
  the engine assembly with GUIDs kept.
- [ ] **Step 2:** Implement Phase 1 missing manifolds — **Hopf fibration, RK4 strange attractors (fixed and
  adaptive) and Seifert surfaces done**; **Morin surface / eversion still open**: it must be sourced, not
  written from memory (GEOMETRY-MAP §4), and the sources were unreachable from the session.
- [x] **Step 3:** Phase 2 Dual Contouring (`DualContouring`, `DualContourEngine`) and one screw dislocation
  (`ScrewDislocation`) for `Implicits` and `EscherFields`, seamless on all six TPMS. *§2.3 GPU extraction not started.*
- [x] **Step 4:** Phase 3 `Rotor4` SO(4) algebra (used by `Polytope4DSwarm` and, as the default
  `RotationModel.Bivector`, by `Hyperspace4DAxis` / `Hyper4DField`) and the verified `Polytope4DLibrary`.
  `NCubeSwarmBase` keeps its Givens chain on purpose: the quaternion-pair form exists only in 4-D.
  *§3.3 done in pass 3: `Engine/Polyhedra` (Conway seeds, d a k g c w q r + t j e o s b m n, notation parser, Hart canonical form) and `ConwayPolyhedronEngine`; see Plans/PASS-3-PLAN.md P4.*
- [x] **Step 5:** New standalone `GeometryEngine/Wire` shader: screen-space AA, `_ShowDiagonals`, depth fade,
  dual-tone gradient, lattice pulse. Mesh-side diagonal suppression is `WireMeshBuilder.hideQuadDiagonals`.
  Existing chamber shaders unchanged.
- [x] **Step 6:** `GeometryEngine.Runtime` / `.Editor` / `.Tests` asmdefs (scoped to `_GeometryWork/Engine`,
  since `Core/` still needs studio packages) and 70 NUnit tests: Euler characteristic, orientability,
  NaN/pole safety, outward normals, plus the math itself. Runnable without Unity:
  `dotnet test Tools/EngineTests/Tests`.
- [x] **Step 7:** Public APIs documented in `Assets/_GeometryWork/Engine/README.md` and XML docs;
  GEOMETRY-MAP updated. Not yet rendered in the Unity Editor.
