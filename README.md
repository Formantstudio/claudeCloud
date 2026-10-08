# Claude Cloud — Procedural Geometry & Manifold Engine

This repository isolates the pure procedural geometry, mathematical manifolds, implicit fields, 4D/n-D polytopes, and wireframe shader engines from the studio project.

**Goal:** Transform this mathematical foundation into an ultra-high-performance, standalone, mathematically rigorous procedural geometry and manifold engine for Unity URP.

---

## Repository Structure

```
├── Assets/
│   ├── _GeometryWork/          # Primary geometry engines, shapes, shaders, materials
│   │   ├── Engine/             # Standalone engine assembly (UnityEngine only): SO(4) rotors,
│   │   │                       #   4-polytopes, Hopf, attractors, Seifert, dual contouring,
│   │   │                       #   Escher warps, Conway polyhedra, tests
│   │   ├── Core/               # Chamber engines, manifolds, implicit fields, swarms
│   │   │   ├── NCube/          # 4D-10D hypercubes, 120-cell, Metatron, projections
│   │   │   └── Editor/         # Inspector tools and scene scaffolding
│   │   ├── Shapes/             # Enneper reality, Scherk towers, ShapePrinter
│   │   ├── ShaderSets/         # URP barycentric wire shaders & presets
│   │   └── Materials/          # Family-tuned wire & particle materials
│   ├── _EscherWorldManagement/
│   │   ├── Rooms/              # TPMS architectural rooms, marching tables, 4D fields
│   │   ├── Hyperspace/         # 4D rotation axes, fields, kaleidoscopic folding
│   │   └── CurvedWorld/        # Curved world bridge & automation
│   ├── polyhedronGenerator/    # Platonic, Archimedean, Johnson solids & Conway operators
│   └── PsychedelicLab/Warp/    # CPU fractal voxel meshing (Menger, Sierpinski, Mandelbulb)
├── Tools/EngineTests/          # Runs the engine's NUnit tests under plain .NET (no Unity needed)
├── Tools/CoreTests/            # Compile + behaviour checks for Core, Shapes and Escher files
├── Tools/ShaderCheck/          # glslang type-check of every shader pass and keyword variant
├── Tools/ShapeBench/           # Source of the Shape Bench preview page (build.py, check.js)
└── Plans/                      # Specifications, math roadmaps, and execution checklists
    ├── CLAUDE_CLOUD_UPGRADE_PLAN.md   # MASTER EXECUTION PLAN FOR THIS CLOUD SESSION
    ├── GEOMETRY-MAP.md                # Full inventory of existing geometry assets
    ├── GeometricFractalPlan.md        # Mathematical manifold & swarm specifications
    ├── ESCHER-STEP-PLAN.md            # Screw dislocation & infinite Escher staircases
    ├── GeometryMode.md                # Mode boundaries & rules of engagement
    ├── ENNEPER-REALITY.md             # Enneper minimal surface fold specifications
    ├── PASS-3-PLAN.md                 # Third pass: audit, verify, complete (with results)
    └── SCHERK-TOWER.md                # Scherk tower doubly-periodic minimal surfaces
```

---

## The Core Mathematical Foundations

1. **Unwelded Barycentric Wireframe Chamber:**
   Meshes are generated unwelded (6 vertices per quad, unique index per vertex) with barycentric coordinates encoded in UV1. Shaders calculate constant-pixel-width wireframes via `smoothstep(0, fwidth(bary) * 1.25, bary)`. Rendered opaque with `Cull Off` so self-intersecting immersions (Klein bottle, Roman surface, Boy's surface, Clifford torus) resolve cleanly via the depth buffer without sorting.

2. **Parametric Manifolds (30 Surfaces):**
   Continuous mappings $f(u, v) \to \mathbb{R}^3$ including minimal surfaces (Costa, Enneper, Henneberg, Scherk, catenoid, helicoid), solitons (Breather, Kuen, pseudosphere), projective planes (Boy's, Roman, cross-cap), and complex manifolds (Calabi-Yau Fermat quintic slices).

3. **Implicit Surfaces & TPMS Architecture:**
   Triply Periodic Minimal Surfaces (Gyroid, Schwarz P/D, Neovius, Lidinoid, Split-P) that tile $\mathbb{R}^3$ infinitely.
   **The Escher Screw Dislocation:** Replacing $z$ with $z + \frac{\text{period}}{2\pi} \text{atan2}(y, x)$ introduces a topological dislocation that transforms periodic labyrinths into genuine endless staircases from every perspective angle.

4. **N-Dimensional Polytope Projections:**
   Regular polytopes in 4D through 10D ($n$-cubes from Tesseract to Dekeract, 120-cell, 600-cell) projected to 3D with edge-travelling particle swarms and IFS clustering.

---

## How to Run This Cloud Session

Open `Plans/CLAUDE_CLOUD_UPGRADE_PLAN.md` and follow the phased execution roadmap. Its checklist
records what is done; `Assets/_GeometryWork/Engine/README.md` documents the engine API.

## Testing

- In Unity: Test Runner ▸ EditMode ▸ `GeometryEngine.Tests`.
- Without Unity (.NET 8 SDK): `dotnet test Tools/EngineTests/Tests` (engine math, 166 tests) and
  `dotnet test Tools/CoreTests` (compiles the studio-facing chamber, shape, swarm and Escher files
  against stubs and runs their behaviour tests, 154 tests).
- Shaders: `python3 Tools/ShaderCheck/check.py` (needs `glslang-tools`) type-checks every pass.
- Shape Bench: `node Tools/ShapeBench/check.js` verifies the preview page's geometry.
