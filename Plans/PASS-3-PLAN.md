# Pass 3 — audit, verify, complete

Working plan for the third cloud pass. Each item ends in something checkable: a test that fails
before and passes after, a measured number, or a published page. Status is kept in this file as the
work lands.

Ground rules carried over: performance first; nothing claimed without a check; formulas that must be
sourced (Morin) are not written from memory; particles belong to Particle Mode (GeometryMode.md) and
stay out of scope; ESCHER-STEP-PLAN §7 decisions gate the scene manager, so it is not built here.

---

## P0 · Shape Bench (the claude.ai preview page)

- [ ] **0.1 Fix the 600-cell.** It builds 120 vertices but 96 edges: its 24-cell part is the
      (±1, ±1, 0, 0) orientation, which does not fit the 96 even-permutation vertices. Use 8 axis +
      16 half-cube + 96 vertices, sign changes on all four coordinates.
- [ ] **0.2 Verify every bench shape headlessly** with Node: run the page's geometry functions and
      compare V/E with the known counts (and with `Polytope4DLibrary`).
- [ ] **0.3 Show verification in the page:** expected V/E next to the measured counts, with a
      pass/fail mark, so a wrong construction is visible at a glance.
- [ ] **0.4 Add this project's newer shapes**, ported from the C# engine: Hopf fibers, strange
      attractors, Seifert surfaces, the Enneper pillar (closure dial, Round/Square/Hex footprints, a
      hall), and Escher slices (dislocated gyroid and Droste spiral cross-sections by marching squares).

## P1 · Parametric surface audit (`ManifoldSurfaces`, 30 surfaces)

- [ ] **1.1 NaN / Infinity sweep:** every surface over its whole (u, v) grid, edges included, across
      random settings inside each field's `[Range]`. Plan §5 asks for exactly this guarantee.
- [ ] **1.2 Fix whatever the sweep finds**, with a regression test per fix.

## P2 · Chamber topology audit

- [ ] **2.1 Expected topology per surface** (closed or not, orientable or not, χ, boundary loops) from
      the surfaces' known topology, measured through the real chamber mesh with `TopologyReport`.
- [ ] **2.2 Check `WrapsU` / `WrapsV` / `HasPoles`** against what each parameterisation actually does
      at its seams; fix wrong flags (a wrong flag is a visible seam gap or a doubled wire).

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
