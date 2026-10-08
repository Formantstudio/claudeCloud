# ESCHER-STEP-PLAN — the Escher Step Scene Manager

The end goal. Everything built on 2026-10-05 — the shape sources, the swarm engines, the 4-D
activation layer, the implicit surface extractor, the kaleidoscopic folds — exists to feed this.

**The nutshell:** rooms made out of Seifert surfaces, gyroids and Calabi-Yau manifolds, extracted
as cubed or rounded architecture, bent by Curved World, strung together by Tunnels PathFlow, with
the world transforming in front of a steady camera. Penrose steps meets Alucard — gothic
architecture that should not be able to exist, lit like an ayahuasca vision.

---

## 1. The one mechanism that makes this honest

Impossible stairs are normally a cheat: they close only from one camera angle, or they need a
portal to hide the cut. There is a third way, and it falls straight out of the shapes already
built.

> **Status (2026-10-08).** Built and tested: the screw dislocation (§1, now `Engine/Implicit/ScrewDislocation.cs`,
> shared by `Implicits` and `EscherFields`), the cubed-to-rounded crossfade (`cubeness` on
> `DualContourEngine` and `ImplicitSurfaceChamber`), and three further seam-exact warps in
> `Engine/Implicit/EscherSpace.cs`: sphere inversion, the Droste spiral, and a scrolling window that
> climbs the staircase forever without drift. The code block in §1 below is kept as history and has a
> wrong period — see the corrected bullet under it.

**A screw dislocation in a triply periodic surface gives a genuine endless staircase.**

The gyroid, Schwarz P/D, Neovius and Lidinoid are *triply periodic* — they tile space in all three
axes, forever, with no seam. A screw dislocation shears the lattice so that going once around a
chosen axis advances you by exactly one vertical period:

```
field(p) with p.z replaced by  p.z + (period / 2pi) * atan2(p.y, p.x)
```

Walk a full circuit of the axis and you arrive one floor higher, in geometry identical to where you
started. Not a trick, not camera-dependent, not a portal: the surface really is a helicoid of
rooms, and it works from every angle, under perspective, with the camera free to move.

That is the Penrose staircase, built out of a minimal surface. It is the thing to build first,
because it is the whole premise and it is about fifteen lines in `ImplicitShapes.cs`.

### The code, ready to drop in

Written 2026-10-05 but **not applied**, so whoever owns `ImplicitShapes.cs` can take it without a
merge conflict. Two fields on `ImplicitSettings`, one call at the top of `Field()`, one method.

```csharp
// --- on ImplicitSettings
[Header("Escher step (screw dislocation)")]
[Tooltip("Vertical periods gained per full circuit of the axis. 1 means one trip round puts you "
       + "exactly one floor higher, in geometry identical to where you started. 0 = ordinary room.")]
[Range(-4f, 4f)] public float dislocation;
public Axis3 dislocationAxis = Axis3.Y;
[Tooltip("Distance from the axis below which the shear eases off. A dislocation is singular on "
       + "its own axis, so the core must be softened or the geometry tears there.")]
[Range(.01f, 2f)] public float dislocationCore = .25f;

// --- first line of Field(), before the switch
if (s.dislocation != 0f) p = Dislocate(s, p);

// --- new method
static Vector3 Dislocate(ImplicitSettings s, Vector3 p)
{
    // Bring the chosen axis to Z.
    Vector3 q = s.dislocationAxis == Axis3.X ? new Vector3(p.y, p.z, p.x)
              : s.dislocationAxis == Axis3.Y ? new Vector3(p.z, p.x, p.y)
              : p;

    float radius = new Vector2(q.x, q.y).magnitude;
    float core = Mathf.Max(s.dislocationCore, .01f);
    // 0 on the axis, 1 outside the core: fades the singularity out.
    float ease = radius <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Min(radius / core, 1f));

    if (ease > 0f)
    {
        float azimuth = Mathf.Atan2(q.y, q.x);                      // -pi .. pi
        float period  = Mathf.PI * 2f / Mathf.Max(s.frequency, .01f);
        q.z += s.dislocation * period * (azimuth / (Mathf.PI * 2f)) * ease;
    }

    // Put the axis back.
    return s.dislocationAxis == Axis3.X ? new Vector3(q.z, q.x, q.y)
         : s.dislocationAxis == Axis3.Y ? new Vector3(q.y, q.z, q.x)
         : q;
}

// --- and a guard, because only repeating surfaces give a staircase
public static bool SupportsDislocation(ImplicitShape shape) => IsPeriodic(shape);
```

Three things that matter in it:

- **The period must match the field's own period.** The fields are evaluated at `pi * frequency * p`,
  so the lattice repeats every **`2 / frequency`**, not `2pi / frequency` as first written here (and as
  shipped in `EscherFields` and `EscherField4D.hlsl` until 2026-10-08). With `2pi / frequency` one
  circuit climbs pi floors and the field jumps by up to 0.86 (6.0 for Neovius) across the seam. Use
  any other number and the floors do not line up, which looks like a bug rather than a staircase.
- **The core must be eased.** A screw dislocation is singular on its own axis, where every azimuth
  meets at once. Without the `dislocationCore` fade the geometry tears along the spine. This is the
  one artefact the construction is prone to.
- **It only works on the periodic family.** `SupportsDislocation` returns `IsPeriodic`: a bounded
  surface such as the Barth sextic just gets twisted, because there is no repeat along the axis to
  advance into.

Two further facts worth keeping:

- **A gyroid is already two staircases that never meet.** It divides space into two congruent,
  interpenetrating labyrinths. Pick one side with the level offset and the other side becomes the
  impossible region you can see into but never reach.
- **"Cubed" and "rounded" are the two extractors that already exist.** Voxel-face extraction
  (`FractalMeshFactory` in `PsychedelicLab/Warp/`) gives blocky architecture: steps, landings,
  masonry. Naive surface nets (`ImplicitSurfaceChamber`) gives the smooth, rounded, organic read.
  The same field through either one is the same room built of stone or of flesh — which is exactly
  the gothic-versus-visionary register this is after.

---

## 2. How each shape becomes a room

| Shape | What it gives the architecture | Extractor |
| --- | --- | --- |
| **Gyroid / Schwarz P / Neovius / Lidinoid** | The corridors themselves. Triply periodic, so endless. With a screw dislocation, the staircase | Cubed for masonry, rounded for organic |
| **Barth sextic** | A cathedral: bounded, icosahedral, 65 singular points where the geometry pinches. A room with a ceiling | Rounded; the pinch points read as vaulting |
| **Mandelbox** | Balconies, galleries, recursive chambers. `scale` near -1.75 is the room register | Rounded, or raymarched for interiors |
| **Menger sponge** | Honest masonry. Cubic by construction, so cubed extraction is native | Cubed |
| **Calabi-Yau quintic** | The impossible centrepiece. 25 patches of lobed sheet, no inside or outside. Not a room — the thing *inside* a room that should not fit | Mesh, not extracted; it is parametric |
| **Seifert surfaces** | Ramps and spiral stairs with a knot for a boundary. The genus `(p-1)(q-1)/2` sets how many ways round | Ribbon sweep, not extracted |
| **Clifford torus / duocylinder** | The 4-D bubble contents: what the room turns into while the activation layer is open | Parametric, projected |
| **Klein bottle** | A corridor that returns to itself the wrong way up | Parametric |

The division matters: the *periodic implicit* shapes are architecture, the *parametric* shapes are
objects and events inside it. Trying to make a Calabi-Yau into a floor is the wrong job for it.

---

## 3. What already exists to build on

Built 2026-10-05, none of it yet verified in the Editor.

| Piece | Where | Role here |
| --- | --- | --- |
| `ImplicitShapes` / `ImplicitSurfaceChamber` | `_GeometryWork/Core/` | 13 fields and the rounded extractor. The rooms |
| `FractalMeshFactory` | `PsychedelicLab/Warp/` | The cubed extractor, already written and already baking Menger and Mandelbulb |
| `Hyperspace4DAxis` | `_GeometryWork/Core/` | Six SO(4) planes, rocking, cue-driven activation envelope. The transformation trigger |
| `Hyper4DField` / `Hyper4DRing` | `_GeometryWork/Core/` | Local 4-D bubbles. The world stays intact; bubbles open inside it |
| `KaleidoFold` | `_GeometryWork/Core/` | Sector mirroring and plane folds in shape space. Endless-temple walls |
| `CurvedGeometryChamber` | `_GeometryWork/Core/` | Four modes, bend ID 1, 30 parametric surfaces |
| `GeometryFractalEngine` | `_GeometryWork/Core/NCube/` | Composable source + operator stacks |
| `GeometricParticleSwarmEngine` | `_GeometryWork/Core/NCube/` | Skeletal frames, girders, rails, nested building blocks |
| `CurvedWorldBridge` | `PsychedelicLab/ControlRig/Fx/` | 8 bend presets, shared keyword management, per-camera publish |
| Shapes FlowPath | `Assets/Tazlel/Shapes FlowPath/` | 46 tunnel meshes. The connective corridors between rooms |
| `GitRepoKeijiro/Portals` | palette | The fallback for cuts a dislocation cannot close |
| `GitRepoKeijiro/HyperEngine`, `noneuclideanunity` | palette | Hyperbolic and spherical space, if the rooms should also be non-Euclidean |
| `MasterClock`, `KickReactivity` | `PsychedelicLab/ControlRig/Clock/` | The cues that drive every transformation |

---

## 4. Escher Step Scene Manager — the architecture

One manager owning a list of **rooms** and a **path** through them, with the world transforming on
cues rather than on camera movement.

### 4.1 A room

```
EscherRoom
  field            : ImplicitSettings        which surface
  extraction       : Cubed | Rounded | Both  masonry or flesh
  dislocation      : float                   vertical periods per circuit (0 = ordinary room)
  cell             : Vector3Int              how many lattice periods the room spans
  bend             : CurvedWorldBridge preset + amount
  kaleido          : KaleidoSettings         wall folding
  occupants        : shape sources           the Calabi-Yau, the Seifert ramp, the swarms
  bubbles          : Hyper4DRing             where 4-D opens
  materials        : per-role wire and particle materials
```

Because the field is periodic, a room is not a mesh that has to be placed — it is a *window onto
an infinite lattice*. Moving the window moves the room, which is why the scroller can keep going.

### 4.2 Transformation, not travel

The camera is steady and looks down the corridor. The world changes around it:

- `MasterClock` bar and beat cues fire `Hyperspace4DAxis.Trigger()`; the activation envelope opens
  the bubbles, the W planes rock, 4-D geometry appears inside the intact 3-D corridor.
- The room's `level` offset drifts, which sweeps a gyroid from one labyrinth to the other — the
  walls become the openings and the openings become the walls, in place.
- `CurvedWorldBridge` changes preset, so the corridor bends away: Little Planet closes it into a
  sphere, Cylindrical Tower stands it on end.
- The kaleidoscope sector count steps, and the wall pattern refolds.
- `extraction` crossfades Cubed to Rounded: the masonry softens into flesh. This is the single most
  on-theme transition available and it costs one dial.

### 4.3 Stringing rooms together

Tunnels PathFlow meshes are the corridors between rooms, already skinned by
`TunnelSparkleParticles` and bent by the same bend ID. A room's exit is a tunnel mouth; the tunnel
runs to the next room's entry. With a dislocation in play the "next" room can be the same room one
period up, which is the staircase.

---

## 5. Build order

1. **Screw dislocation in `ImplicitShapes`** (section 1). Fifteen lines, and it is the premise. One
   room, one axis, walkable, verified by eye before anything else is built on it.
2. **Cubed extraction alongside rounded.** `FractalMeshFactory` already does voxel faces; it needs
   wiring into `ImplicitSurfaceChamber` as a second mode, with the unweld-and-write-UV1 pass so the
   barycentric wire works on it. Then the Cubed-to-Rounded crossfade.
3. **`EscherRoom` as a component** — field, extraction, dislocation, cell count, bend, kaleido. One
   room that can be looked at and tuned.
4. **Cue-driven transformation** — wire the room's level drift, bend preset and sector count to
   `MasterClock` bars, next to the 4-D activation that already listens there.
5. **`EscherStepSceneManager`** — a list of rooms, a path, and the crossfades between them.
6. **Tunnels PathFlow corridors** between rooms, reusing the existing tunnel skin.
7. **Occupants**: Calabi-Yau inside a room, Seifert ramps as stairs, the swarm engines as the
   skeletal frames and rails.
8. **Barth sextic cathedral** and **Mandelbox galleries** as further room types.
9. **Portals** (`GitRepoKeijiro/Portals`) only where a dislocation cannot close the loop.
10. **Non-Euclidean** (`HyperEngine`, `noneuclideanunity`) as a late option: the vertex-stage
    transform is the same stage Curved World already occupies, so it is a keyword, not a rewrite.

---

## 6. Art direction

Recorded because it decides the palette and the extraction mode, not just the mood.

Gothic and visionary at once — Alucard, Van Helsing — carried by **cubed masonry** in cold cyan and
ice, against **rounded organic** surfaces in gold, ember and violet. The existing
`_GeometryWork/Materials/` set already splits this way: `Wire_Menger_Ice` and `Wire_Polytope_Ice`
for the architecture, `Wire_Mandel_Ember` and `Wire_Klein_Violet` for the flesh. The transition
between them is the Cubed-to-Rounded crossfade, so the material change and the geometry change are
one move.

Psychedelic register comes from the kaleidoscopic folds and the 4-D bubbles, not from colour
cycling. The restraint that makes it work: the corridor stays still and the world moves.

---

## 7. Decisions needed

Do not build past these.

- **Does the camera ever move?** Everything in section 4.2 assumes it does not, and a steady camera
  is what makes the 4-D rocking legible (`Hyperspace4DAxis.SetUpForView`). If the camera flies the
  corridor instead, the dislocation becomes the primary mechanism and the bubbles become secondary.
- **One infinite lattice or discrete rooms?** A periodic field is a window onto an infinite
  structure, which argues for one lattice and a moving window. Discrete rooms are easier to art
  direct. These lead to different managers.
- **Cubed extraction: voxel faces or quantised surface nets?** `FractalMeshFactory`'s voxel faces
  are true blocks. Snapping surface-net vertices to a grid gives blocks that still carry the
  surface's normals. The second is cheaper to crossfade from rounded.
- **How much is per-frame?** The rounded extractor samples `resolution^3` per rebuild. A room drifting
  its level every frame at 64^3 is 262,144 field evaluations a frame. Either the drift is stepped, or
  extraction moves to a compute shader, or `ComputeMarchingCubes` takes over. This decides whether
  the world can transform continuously or in cuts.

---

## 7a. Harder manipulations now available

All in `EscherSpace`, all exact at their seams (a jump in the sampling coordinates only ever by whole
lattice periods), applied identically by the engine's implicit fields and the rooms:

| Warp | What it does | Seam rule |
| --- | --- | --- |
| Sphere inversion | Folds the whole infinite lattice inside a ball, infinity at the centre — Circle Limit in 3-D. Conformal, so walls keep their angles | None needed: a smooth involution |
| Droste spiral | Log-cylindrical lattice coordinates: the structure is exactly invariant under scaling by `s`, rooms nested in rooms toward the axis; with a twist, one turn steps one scale level (Print Gallery) | Whole-number sectors and twist |
| Screw dislocation | One floor per turn | Whole-number dislocation |
| Scrolling window | The lattice slides through the room; wrapped to one period, so the climb never accumulates float error | Applied last, in lattice coordinates |
| Cubeness | Vertex → cell centre blend: masonry to flesh with identical topology | — |

The 4-D room fields were also corrected: at W = 0 the rooms' Gyroid, Schwarz P and Neovius were not
those surfaces (P was the level −1 surface, Neovius shifted by 3, and the 4-D gyroid was not a gyroid
at any W). Their fourth-dimension terms now vanish at W = 0, on the CPU and in `EscherField4D.hlsl`.
The GPU path supports the dislocation only; the other warps are CPU-side for now.

On §7's per-frame question: extraction now runs on worker threads. A 64³ gyroid with a dislocation
re-extracts in about 80 ms (surface nets) or 180 ms (dual contouring) on four cores, against 143 and
554 ms serially — fast enough for stepped transformation on bars, not yet for every frame.

## 8. Unverified

Everything listed in section 3 was written on 2026-10-05 and **none of it has compiled or rendered
yet** — the move into `_GeometryWork/` alone forces a full reimport. The screw dislocation in
section 1 is standard crystallography applied to a level set and has not been tried here. No
performance number in this document has been measured; the only measured figures anywhere in the
geometry work are the original chamber's 12 x 48 grid and `DekeractTetraSwarm`'s 11,264 particles.
