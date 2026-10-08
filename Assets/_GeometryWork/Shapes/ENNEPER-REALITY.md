# Escher Enneper folds

These are arrangements of the existing Enneper Fold Flower surface. They produce the appearance of repeating architecture; they do not create enclosed rooms, collision volumes or portal worlds.

## Pillar closure

The new engine includes a geometric **Pillar Closure** deformation. At 0 it preserves the Enneper flower, including its return/curl. At 1 it reshapes the surface into a continuous vertical waist with outward-flaring floor and ceiling ends; the upper end opens down into the stem. This is a designed pillar deformation, not a mathematically minimal Enneper surface at full closure. The original Fold Flower objects are unchanged.

**Waist Radius**, **Pillar Half Height**, **End Flare**, **Fold Fluting**, and **Pillar Twist** tune the shape. The closed pillar is centered on the engine's vertical axis. Its circumference meets at the seam, and both ends join the same stem. Dense grid rendering and surface-following particle sampling use the same deformation. The saved new presets use full closure.

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
