# Pass 3 — audit, verify, complete

Working plan for the third cloud pass. Each item ends in something checkable: a test that fails
before and passes after, a measured number, or a published page. Status is kept in this file as the
work lands.

Ground rules carried over: performance first; nothing claimed without a check; formulas that must be
sourced (Morin) are not written from memory; particles belong to Particle Mode (GeometryMode.md) and
stay out of scope; ESCHER-STEP-PLAN §7 decisions gate the scene manager, so it is not built here.

---

## P0 · Shape Bench (the claude.ai preview page)

- [x] **0.1 Fix the 600-cell.** It builds 120 vertices but 96 edges: its 24-cell part is the
      (±1, ±1, 0, 0) orientation, which does not fit the 96 even-permutation vertices. Use 8 axis +
      16 half-cube + 96 vertices, sign changes on all four coordinates.
- [x] **0.2 Verify every bench shape headlessly** with Node: run the page's geometry functions and
      compare V/E with the known counts (and with `Polytope4DLibrary`).
- [x] **0.3 Show verification in the page:** expected V/E next to the measured counts, with a
      pass/fail mark, so a wrong construction is visible at a glance.
- [x] **0.4 Add this project's newer shapes**, ported from the C# engine: Hopf fibers, strange
      attractors, Seifert surfaces, the Enneper pillar (closure dial, Round/Square/Hex footprints, a
      hall), and Escher slices (dislocated gyroid and Droste spiral cross-sections by marching squares).

**P0 result.** Published as version 2 of the Shape Bench. The 600-cell now gives 120 / 720.
Porting the Escher warps into the bench exposed two real faults in the C# pipeline, fixed in
`2e77872`: the screw core cracked along the cut inside the core (the old seam tests never probed
there), and a screw after the Droste map could not be seamless (the staircase now rides inside the
Droste coordinates). The bench source lives in `Tools/ShapeBench` (`build.py`, `check.js`: 98 checks).

## P1 · Parametric surface audit (`ManifoldSurfaces`, 30 surfaces)

- [x] **1.1 NaN / Infinity sweep:** every surface over its whole (u, v) grid, edges included, across
      random settings inside each field's `[Range]`. Plan §5 asks for exactly this guarantee.
- [x] **1.2 Fix whatever the sweep finds**, with a regression test per fix.

**P1 result.** No surface emits NaN or infinity anywhere on its square, at minimum, maximum or random
settings (`Tools/CoreTests/Tests/ManifoldSweep.cs`, settings randomised from each field's own
`[Range]` / `[Min]`). The sweep's near miss was the superellipsoid: `SignedPow` amplified float noise
around zero into a 1.4·10⁻³ seam gap; it now returns 0 below 10⁻⁶.

## P2 · Chamber topology audit

- [x] **2.1 Expected topology per surface** (closed or not, orientable or not, χ, boundary loops) from
      the surfaces' known topology, measured through the real chamber mesh with `TopologyReport`.
- [x] **2.2 Check `WrapsU` / `WrapsV` / `HasPoles`** against what each parameterisation actually does
      at its seams; fix wrong flags (a wrong flag is a visible seam gap or a doubled wire).

**P2 result.** Every grid that samples a surface had faults, all fixed with tests:

- *Chamber and combo lattices* put open-axis line i at i / (n − 1) over n quads, so an open u-axis drew
  one column of extrapolated surface past u = 1 and an open v-axis (whose Lerp clamps) a final row of
  zero-area quads — on 21 of the 30 surfaces. Now i / n on every axis.
- *Flags*: Dini and Bour do not close in u; the duocylinder closes in v only as the bare ridge, and a
  full cell has a pole. Settings-aware `WrapsU / WrapsV / HasPoles (surface, settings)` and a new
  `FlipsAcrossUSeam` (Klein; Möbius and trefoil ribbon at odd half-twists) carry this.
- *Vertex grid* (`GeometryFractalEngine`, now `ManifoldVertexGrid`): the Klein bottle and Möbius band
  closed their u-seam onto the same row, drawing chords across the surface; they now join row j to
  the mirrored row. The Roman surface (wraps in v, pole row) shifts rows by half a step instead of
  insetting, which had stretched its closing edge to two steps.
- *Combo chamber* evaluated each grid node four times (once per quad corner); it now fills a lattice
  once and scatters, the chamber's pattern.

Tests: every surface through the chamber and the combo, welded, matches its expected χ, boundary
loops and orientability with no degenerate, non-manifold or non-finite faces at two odd grid sizes;
the flags are checked against the formulas over 40 random settings; vertex-grid seam edges are no
longer than interior ones. Odd grids matter: even ones land lines on real singularities (horn-torus
pinch, Whitney and Kuen pinch points) and on the 2:1 Roman, cross-cap and Henneberg maps.

## P3 · Scherk tower verification

- [ ] **3.1 The exact-chart claim:** with neutral deformations every tower point satisfies
      sinh x · sinh y = sin z (in surface units).
- [ ] **3.2 The hall claim:** points satisfy z = H·tanh(ln|cos x / cos y| / H); the hall is one
      continuous surface (no cracks between cells).
- [ ] **3.3 Fix anything that does not hold.**

## P4 · Conway operator pipeline (plan §3.3)

- [ ] **4.1 Polygon mesh core** (`Engine/Polyhedra`): vertices + oriented faces, edge map, validation.
- [ ] **4.2 Seeds:** T C O D I and prism / antiprism / pyramid families.
- [ ] **4.3 Primitive operators:** dual, ambo, kis, gyro, chamfer, whirl, quinto; derived truncate
      (dkd), join (da), expand (aa), ortho (de), snub (dgd), bevel (ta), meta (kj), needle (kd).
- [ ] **4.4 Notation parser:** strings such as `tI`, `dkdC`, `gaD`, applied right to left.
- [ ] **4.5 Canonicalisation** (Hart): planarise faces and make edges tangent to the unit sphere.
- [ ] **4.6 Tests:** V/E/F for named polyhedra (truncated icosahedron 60/90/32, snub cube 24/60/38, …),
      χ = 2, manifold, oriented, planarity improvement from canonicalisation.
- [ ] **4.7 Wire output:** `WireMeshBuilder.Polygon` fans that hide every interior diagonal, and a
      `ConwayPolyhedronEngine` component.

## P5 · Performance, round 2

- [ ] **5.1 `SampleGrid` frame caching:** particle swarms call it thousands of times a frame; reuse
      the frame constants the fill computed.
- [ ] **5.2 `ManifoldComboChamber`:** check for the same per-corner re-evaluation; fix with parity.
- [ ] **5.3 Escher rooms (`EscherSurfaceBuilder`):** profile the marching-cubes emission and speed it
      up with output parity.
- [ ] **5.4 Re-measure everything** and record before/after.

## P6 · Structure

- [ ] **6.1** `KaleidoFold` axis permutation → `ScrewDislocation.ToAxis` (one implementation).
- [ ] **6.2** `Polytope4DSwarm.Label` and `GeometryFractalEngine` read `Polytope4DLibrary` directly.
- [ ] **6.3** Sweep the CoreTests build for warnings worth fixing.

## P7 · GPU parity

- [ ] **7.1** Port `EscherSpace` (inversion, Droste, scroll) to `EscherField4D.hlsl` and its uniform
      publisher, so portal shaders and CPU rooms agree. Reviewed, not compiled (no HLSL compiler here).

## P8 · Morin surface

- [ ] **8.1** One more sourcing attempt. Implement only with a cited formula, verified by property
      tests (immersion, 4-fold symmetry swapping sides, χ = 2). Otherwise stays blocked.

## P9 · Close out

- [ ] **9.1** Upgrade-plan checklist, GEOMETRY-MAP, Engine README, root README.
- [ ] **9.2** Full test runs (EngineTests, CoreTests), commit, push.
