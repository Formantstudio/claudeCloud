"""Builds shape-bench.html (the claude.ai Shape Bench page) from template.html + geometry.js.

    python3 Tools/ShapeBench/build.py      # writes Tools/ShapeBench/shape-bench.html
    node Tools/ShapeBench/check.js          # verifies every shape headlessly

geometry.js mirrors the C# engine (Polytope4DLibrary, HopfFibration, StrangeAttractors,
SeifertSurface, EnneperPillar, EscherSpace). Keep the two in step: check.js runs the same checks
the C# tests make.
"""
import os
here = os.path.dirname(os.path.abspath(__file__))
geometry = open(os.path.join(here, 'geometry.js')).read()
template = open(os.path.join(here, 'template.html')).read()
assert '/*GEOMETRY*/' in template
open(os.path.join(here, 'shape-bench.html'), 'w').write(template.replace('/*GEOMETRY*/', geometry))
print('wrote shape-bench.html')
