# Escher Enneper folds

These are arrangements of the existing Enneper Fold Flower surface. They produce the appearance of repeating architecture; they do not create enclosed rooms, collision volumes or portal worlds.

## Pillar closure

The new engine includes a geometric **Pillar Closure** deformation. At 0 it preserves the Enneper flower, including its return/curl. At 1 it reshapes the surface into a continuous vertical waist with outward-flaring floor and ceiling ends; the upper end opens down into the stem. This is a designed pillar deformation, not a mathematically minimal Enneper surface at full closure. The original Fold Flower objects are unchanged.

**Waist Radius**, **Pillar Half Height**, **End Flare**, **Fold Fluting**, and **Pillar Twist** tune the shape. The closed pillar is centered on the engine's vertical axis. Its circumference meets at the seam, and both ends join the same stem. Dense grid rendering and surface-following particle sampling use the same deformation. The saved new presets use full closure.

The pillar math lives in `Engine/Manifolds/EnneperPillar.cs` (pure, unit-tested). Each copy's flutes turn with that copy's own fold phase (Phase Per Copy, Phase Per Level and the swing), the same phase its Enneper surface uses, so a closing fold and its pillar never disagree.

### Footprint: how pillars merge into one hall

**Footprint** decides what the flared floor and ceiling open out to:

- **Round:** the original pillar — a circle of radius Waist + End Flare. Neighbouring flares overlap freely.
- **Square / Hexagonal:** each end becomes the pillar's lattice cell. Twist and fluting fade to zero at the ends, so neighbouring pillars share each cell edge exactly, at the same height, and both arrive there horizontally. The floors and ceilings merge into one continuous vault across the hall instead of passing through each other. In a hall, Spread (and Depth Spacing, for Square) is the cell size, and the column count snaps to 24k + 1 so every cell corner is a grid vertex.
- **Auto (default):** Round for a single pillar, Square once Consistent Copies places several at full closure.

### Arrangement symmetry

NestedFlower and FoldCorridor turn each copy about the sightline *after* the fold tilt, so the crown is exactly n-fold symmetric about Z and closed pillars stand as spokes. (Earlier versions spun each copy about its own tilted axis, which broke the symmetry and leaned the pillars at different angles.)

### Cost

One 180×180 pillar costs about 6 ms per frame to fill, down from about 38 ms (Release, measured outside Unity): the chamber evaluates each lattice node once instead of four times, per-frame constants are computed once, the pillar is evaluated row by row, and at full closure the Enneper point under the pillar is no longer computed only to be discarded.

Use `Tools > Geometry FX > Enneper > Add Escher Fold Variations (Disabled)`. The three entries join the existing Shape Arsenal in the current scene, or a new Enneper-only catalog if none exists. Activate one entry using the arsenal inspector or its hierarchy checkbox.

- **NestedFlower:** self-similar, shrinking crowns of folds.
- **FoldCorridor:** receding, twisting repetitions with a central sightline.
- **SpiralPillars:** proportional Enneper folds repeated in rows, retaining the reference pillar orientation.

Start with one copy and one level: all arrangements then display the same centered Enneper surface. Use the component context menu **Frame single Enneper pillar for camera** to place the fold in front of the camera with its reference 45-degree tilt. The camera remains stationary. Framing Distance and View Fill control the composition. It works with a camera at the origin, rotation zero, looking along +Z.

The detailed surface uses resolution 180, matching the approximately 180x180 original. Lattice Divisor and Hide Quad Diagonals match the reference grid. Surface Cell Budget shares the available detail across additional copies. Increase Copies Per Level and Levels only when repetition is wanted; Spread, Depth Spacing and Level Twist then place those copies. Single-copy mode has no sideways offset or nonuniform pillar stretch. Copies are separate surfaces, not a welded tunnel.

Phase Degrees changes the Enneper associate-family phase. Domain changes the extent of the sampled surface. Phase Per Copy and Phase Per Level stagger the shapes. Animate Folds advances the phase and arrangement in Play mode. Preview Animation explicitly enables editor animation; otherwise use the phase slider. Cycle Seconds, Fold Swing and Rotation Degrees Per Second set the motion.

The original Enneper mapping is recovered with Domain 2 and Phase 0. The new phase blends the real and imaginary parts of the same analytic Enneper parametrization. It is not a blend to an unrelated manifold. Repetition is a finite self-similar arrangement, not an infinite mesh or a distance-estimated fractal.

The generator uses CurvedGeometryChamber, its wire material, Curved World registration and WorldGridScan. Generated geometry is transient and released on disable. Optional first-level WireParticleSwarm instances share a particle ceiling; particle layers start off. Particle Management can target these generated particle renderers.

Parallax 3D, GeometryAlgorithms and Calabi-Yau components are not dependencies of this version. Camera perspective supplies real parallax between the repeated folds. Future extensions can add optional parallax-window skins and cellular sampling without replacing the fold generator.

Reusable prefabs live beside the other shape presets in this folder. Keep the user's existing scene camera and active arsenal selection intact when adding variations.
