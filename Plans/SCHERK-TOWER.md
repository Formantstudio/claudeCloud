# Scherk towers

`ScherkTowerEngine` is the Scherk variant of `EnneperFoldReality`. It uses the same machinery: a pool
of generated `CurvedGeometryChamber` objects whose vertices come from the engine's
`surfaceDeformation` hook, the same shared cell budget, the same optional `WireParticleSwarm` layer on
the first object, the same transient generated geometry released on disable. Nothing outside
`ScherkTowerEngine.cs` was added or changed — no new enum entries, no new extractor, no edits to the
chamber or to the Enneper engine.

## Why Scherk for a room of pillars

The Enneper engine has to *arrange* copies. Its surface is one finite flower, so a colonnade is a
placement decision and the repetition is only as coherent as the spacing numbers are.

Scherk's second surface is singly periodic by construction: it repeats along its axis with period 2π,
forever. A tower is therefore one surface sampled over a longer parameter range rather than N copies
stacked, and the lobe spacing comes out of the mathematics instead of out of the inspector. That is
the whole reason to prefer it here.

## The surface

Scherk's second surface — the saddle tower — is the zero set of

    sinh(x) · sinh(y) = sin(z)

The engine does not extract this implicitly. It parametrises it exactly. Writing c = sin(z), the level
curve at height z is `sinh(x)·sinh(y) = c`, which is solved in closed form by

    sinh(x) = √|c| · eʷ          sinh(y) = sign(c) · √|c| · e⁻ʷ

because those two factors multiply to exactly c for every w. So

    x(w, z) = asinh( √|c| · eʷ )
    y(w, z) = sign(c) · asinh( √|c| · e⁻ʷ )
    z(w, z) = z

with **w across the saddle** (the chamber's u) and **z up the tower** (the chamber's v). Every point
this produces lies on the real surface. It is an exact chart, not an approximation of one.

## What the chart covers, and what it does not

This is visible in the result, so it is worth stating plainly.

At each half period (z = 0, π, 2π, …) c passes through zero, both coordinates collapse to the origin,
and the chart pinches to a point. The true surface at those heights is the full pair of lines x = 0
and y = 0. So the chart traces **one saddle lobe per half period, pinched at the waists between
them** — a column of saddle lobes on a narrow spine. That is the pillar reading wanted here, but it is
a genuine sub-sheet of Scherk's surface rather than all of it.

**Branches 2** adds the x-negative chart as a second chamber. That is the other half of each lobe
pair, and it is what makes the four-wing cross section read correctly. Branches 1 halves the cost and
shows one wing pair.

## Exact versus deformed

Only **Periods** and **Wing Span** stay inside the exact surface — the first is how much of the axis is
sampled, the second how far out along w the wings are truncated (the surface itself extends forever;
large values flatten it towards its asymptotic planes).

Everything under *Deformations* leaves the minimal surface, and says so:

- **Twist Per Period** — degrees of rotation about the axis per half period. 0 is exact Scherk; nonzero
  makes a helical column that is a designed shape, not a minimal one.
- **Waist Hold** — floors |c| so the chart does not collapse at each half period. It moves those rings
  off the true surface by exactly the floor. 0 leaves the honest pinch; around 0.35 the column reads as
  continuous stone rather than beads on a string.
- **Taper** — column entasis towards the top.
- **Tower Form** — blends out of whatever **Base Surface** is set to, so the tower can morph from an
  ordinary manifold.

The component context menu **Exact Scherk surface (neutral deformations)** sets all four back to
neutral in one click, so the mathematically honest form is always one action away.

## Placement

Four arrangements, all showing the identical surface — only the transforms differ.

- **PillarHall** — a square grid of towers, `columns²` of them. The room.
- **Colonnade** — a single row, for a corridor with a clear sightline.
- **Rotunda** — a ring about the origin, for an orbiting camera.
- **SingleTower** — one, centred. The reference view.

**Yaw Per Tower** stops neighbours presenting identical silhouettes. **Stagger Phase** lifts alternate
towers by a fraction of a lobe, which breaks up the flat band that otherwise forms where every tower's
waists line up across the hall — the giveaway that a hall is one shape repeated.

**Spacing** below roughly twice **Tower Radius** makes the wings interpenetrate.

## Framing and budget

**Frame single Scherk tower for camera** (context menu) switches to SingleTower, puts the engine in
front of the camera and sizes it to fill the frame. It solves for *height* rather than width, because a
tower's binding dimension is vertical — the opposite of the Enneper flower's framing helper.

**Surface Cell Budget** is shared across every tower *and* every branch, so a 3×3 hall with 2 branches
spreads one budget over 18 charts rather than allocating a full grid to each. Raise **Resolution** only
after raising the budget, or the per-chart grid stays capped by the division.

## Not done here

Material generation, scene presets and a `Tools >` setup menu equivalent to
`EnneperFoldRealitySetup` are not part of this file. Assign any of the existing wire materials in
`Assets/_GeometryWork/ShaderSets/` to **Tower Material**; the `02_Stretched` group suits it, since the
w direction stretches the parameterisation exactly the way those materials' triplanar grid is meant
to handle.

Nothing here has been rendered yet.
