// Shared wire/sparkle core for the EscherShapeWire family.
//
// Exists because the same ~60 lines were pasted into EnneperStableGrid and EscherShapeWire, and the
// reserved-word bug (`float2 line = ...`, which is the geometry-shader primitive type and will not
// parse) therefore shipped in both at once. Three more shaders are planned on top of this, so the
// duplication stops here.
//
// Every function takes its parameters explicitly and reads no material globals, so each shader keeps
// its own UnityPerMaterial CBUFFER and stays SRP-batcher compatible without the files having to agree
// on a property list.
//
// Naming rule for anything added here: never `line`, `point`, `sample`, `texture`, `vector`, `matrix`
// or `triangle` — all reserved in HLSL.
#ifndef ESCHER_WIRE_CORE_INCLUDED
#define ESCHER_WIRE_CORE_INCLUDED

#define ESCHER_TAU 6.2831853

// Distance to the nearest grid line measured in *pixels*, so a line is the same thickness near and
// far and does not thin out where the parameterisation stretches. A smoothstep in UV units cannot do
// that. The second term fades a direction out before its own cells go subpixel; without it a dense
// grid saturates every fragment and reads as grain rather than as lines.
float EscherStableLines(float2 g, float linePixels, float minCellPixels)
{
    float2 derivative = max(fwidth(g), 1e-5);
    float2 distancePixels = abs(frac(g + .5) - .5) / derivative;
    float2 lineMask = 1 - smoothstep(max(0, linePixels * .5 - .5),
                                     linePixels * .5 + .5, distancePixels);
    // max(..., 3) keeps the two smoothstep edges apart: at minCellPixels == 1 both would be 2 and
    // smoothstep divides by zero.
    lineMask *= smoothstep(2, max(minCellPixels + 1, 3), 1 / derivative);
    return max(lineMask.x, lineMask.y);
}

// Barycentric edge draw, also pixel-measured. The chambers add 1 to the component that vanishes along
// a quad's shared diagonal, which parks that component in [1,2] and keeps the diagonal out of the
// minimum so it never draws.
float EscherBaryEdges(float3 bary, float widthPixels)
{
    float3 derivative = max(fwidth(bary), 1e-5);
    float3 pixels = bary / derivative;
    float nearest = min(pixels.x, min(pixels.y, pixels.z));
    return 1 - smoothstep(max(0, widthPixels * .5 - .5), widthPixels * .5 + .5, nearest);
}

// Barycentric edges with the density fade the plain version lacks. Without it a dense mesh fills
// solid rather than reading as a wire: once a quad shrinks towards the line width, every fragment in
// it is within half a width of some edge. `minQuadPixels` is the on-screen quad size below which the
// edges stop being drawn at all, which is the same bargain EscherStableLines makes for grid cells.
float EscherBaryEdgesFaded(float3 bary, float widthPixels, float minQuadPixels)
{
    float3 derivative = max(fwidth(bary), 1e-5);
    float3 pixels = bary / derivative;
    float nearest = min(pixels.x, min(pixels.y, pixels.z));
    float edge = 1 - smoothstep(max(0, widthPixels * .5 - .5), widthPixels * .5 + .5, nearest);
    // A bary coordinate spans 0..1 across the quad, so its reciprocal derivative is the quad size.
    float quadPixels = 1 / max(length(derivative), 1e-6);
    return edge * smoothstep(minQuadPixels, minQuadPixels * 2.5, quadPixels);
}

float EscherCellFade(float2 g, float minCellPixels)
{
    float px = 1.0 / max(length(fwidth(g)), 1e-6);
    return saturate((px - minCellPixels) / max(minCellPixels, 1e-4));
}

float2 EscherHash22(float2 c)
{
    float3 p = float3(c.xy, c.x + c.y * 7.0);
    p = frac(p * float3(443.897, 441.423, 437.195));
    p += dot(p, p.yzx + 19.19);
    return frac(float2(p.x + p.y, p.y + p.z) * p.z);
}

// One twinkling point per grid *node*, so sparkles land on the intersections instead of scattering
// across the faces. The sparsity gate leaves most nodes dark; that is what keeps it sparse rather
// than noisy. `snap` 1 puts every spark exactly on its node, 0 lets it wander within the cell.
float EscherSparkLayer(float2 g, float sizeScale, float seed,
                       float sparsity, float snap, float size, float speed, float minCellPixels)
{
    float2 node = round(g);
    float2 d = g - node;
    float2 h = EscherHash22(node + seed);
    if (h.x < sparsity) return 0;

    float2 offset = (h - 0.5) * 0.9 * (1.0 - snap);
    float dist = length(d - offset) / max(size * sizeScale, 1e-4);
    float point_ = exp(-dist * dist * 5.0);

    float phase = (h.x + h.y * 3.1) * ESCHER_TAU;
    float twinkle = 0.5 + 0.5 * sin(_Time.y * speed * ESCHER_TAU + phase);
    return point_ * twinkle * twinkle * twinkle * EscherCellFade(g, minCellPixels);
}

// Triplanar weights from the *geometric* normal, taken from the screen-space derivatives of the
// position rather than from a vertex normal — marching-cubes and other implicit-surface output
// carries no usable normals, and this is the whole reason triplanar is the default mode for them.
float3 EscherGeometricPlanarWeights(float3 positionWS, float sharpness)
{
    float3 n = abs(cross(ddx(positionWS), ddy(positionWS)));
    n /= max(max(n.x, n.y), max(n.z, 1e-6));
    float3 weights = pow(n, sharpness);
    return weights / max(dot(weights, float3(1, 1, 1)), 1e-6);
}

float EscherTriplanarLines(float3 g, float3 weights, float linePixels, float minCellPixels)
{
    return dot(weights, float3(EscherStableLines(g.yz, linePixels, minCellPixels),
                               EscherStableLines(g.xz, linePixels, minCellPixels),
                               EscherStableLines(g.xy, linePixels, minCellPixels)));
}

// The plane whose grid is most visible here. Sparkling on the dominant plane only stops nodes
// tripling up along the blend seams.
float2 EscherDominantPlane(float3 g, float3 weights)
{
    return weights.x > weights.y && weights.x > weights.z ? g.yz
         : weights.y > weights.z ? g.xz : g.xy;
}

// ---- hierarchical lattice -------------------------------------------------
//
// Why a single fixed density had to go: a parametric manifold's UV->world Jacobian varies by orders
// of magnitude across one mesh — a Klein bottle's figure-8 pinch against its open sweep, a Dini
// spiral's tip against its base. One density is therefore wrong everywhere at once. It smears into
// fat blobs where UV compresses and drops under a pixel where UV stretches, which is exactly the
// "wires blow up and there is no detail" read: the two failures average into flat grey mush.
//
// So density is chosen per *pixel* rather than per material. The material's density stops being the
// level that must be drawn and becomes the finest level the surface is allowed to reach; the shader
// draws the finest division that still keeps a cell above targetPixels, then stacks coarser
// divisions on top of it as major lines and structural bands. That gives a real line hierarchy
// instead of one uniform screen of cells, and it cannot blow up, because the thing that decides the
// density is the on-screen cell size.

// One lattice level, pixel-measured so lines hold their thickness wherever the parameterisation
// stretches. Returned per axis (x from constant-u wires, y from constant-v wires) so callers can run
// travelling energy along the correct axis. Each wire's brightness is hashed from its own index:
// a perfectly even lattice is what makes these surfaces read as printed wallpaper rather than as
// drawn structure, and `variation` is how much of that evenness to break.
float2 EscherLatticeLevel(float2 g, float widthPixels, float variation)
{
    float2 raw = fwidth(g);
    float2 derivative = max(raw, 1e-5);
    float2 distancePixels = abs(frac(g + .5) - .5) / derivative;
    float halfWidth = widthPixels * .5;
    float2 mask = 1 - smoothstep(max(0, halfWidth - .5), halfWidth + .5, distancePixels);

    // Two conditions a level has to meet before it is allowed to draw. Both of them are what stops
    // a lattice turning into a solid mass instead of fading away, at opposite ends of the range:
    //
    //  room  — the cells have to be wider than the line is. Once a cell is only a couple of line
    //          widths across, every fragment in it is within half a width of an edge, so the level
    //          covers the surface rather than ruling it.
    //  alive — the coordinate has to actually vary across the pixel. A level coarsened past collapse
    //          degenerates to a constant, and a constant coordinate sits permanently *on* a line, so
    //          the mask returns 1 everywhere. That is a fill, and it is indistinguishable from a
    //          correct line until you notice it has no holes.
    float2 room = smoothstep(widthPixels * 2, widthPixels * 5, 1 / derivative);
    float2 alive = step(1e-6, raw);

    float2 vary = float2(EscherHash22(float2(round(g.x), 3.17)).x,
                         EscherHash22(float2(7.91, round(g.y))).y);
    return mask * room * alive * lerp(1, .4 + .6 * vary, variation);
}

// Energy running along the wires: a pulse on a constant-u wire travels in v and the reverse, each
// wire carrying its own hashed phase so the surface never pumps in unison. `spacing` is how many
// cells apart successive heads sit.
float2 EscherWirePulse(float2 g, float speed, float spacing, float sharpness, float time)
{
    float2 phase = float2(EscherHash22(float2(round(g.x), 5.13)).x,
                          EscherHash22(float2(2.71, round(g.y))).y);
    float2 travel = float2(g.y, g.x) / max(spacing, 1e-3) - time * speed + phase * 8;
    float2 head = .5 + .5 * sin(travel * ESCHER_TAU);
    return pow(head, sharpness);
}

struct EscherWireBundle
{
    float bands;     // coarsest division: the structural ribs of the surface
    float major;     // mid division
    float minor;     // finest division that is safe at this pixel size
    float micro;     // one step finer again, faded in only when the pixels are there
    float nodes;     // where major wires cross
    float pulse;     // travelling energy, already summed across both axes
    float2 majorG;   // the major level's grid coordinate, for sparkles that want to ride it
};

// `finest` is the material's density (lines around, lines along).
EscherWireBundle EscherHierarchicalWire(float2 uv, float2 finest, float widthPixels,
                                        float targetPixels, float variation,
                                        float pulseSpeed, float pulseSpacing, float flow, float time)
{
    float2 g = uv * finest + float2(0, time * flow * finest.y);

    // Pixels across one cell of the material's own density, in its worst direction.
    float cellPixels = 1 / max(length(fwidth(g)), 1e-6);
    // Halvings needed before a cell clears targetPixels; 0 once the full density already fits.
    float level = max(0, log2(max(targetPixels, 1e-4) / max(cellPixels, 1e-6)));

    // The climb has to be capped. Where UV compression is extreme — the Klein pinch, a Dini tip —
    // `level` runs away, and an uncapped `exp2(-steps)` takes the grid coordinate to a constant,
    // which fills the surface (see `alive` in EscherLatticeLevel). Below two lines per axis there is
    // no lattice left to read anyway, so that is the floor; past it the lattice fades out and leaves
    // the chart-independent mesh-edge wire carrying those regions, which is what it is there for.
    float maxSteps = max(0, log2(max(max(finest.x, finest.y), 2) * .5));
    float steps = min(ceil(level), maxSteps);
    float affordable = saturate(maxSteps + 1 - level);
    // 0 right as a level becomes affordable, approaching 1 as it gets cheaper. Fading the next finer
    // division in on this instead of snapping at integer boundaries is what stops detail popping as
    // the camera closes.
    float budget = saturate(ceil(level) - level);

    float2 minorG = g * exp2(-steps);

    EscherWireBundle o;
    o.majorG = minorG * .25;
    // Widths stay close together on purpose: hierarchy here is carried by *brightness*, not by
    // fattening the coarse lines. A wide coarse line is the fat-blob look this shader is replacing.
    float2 bands = EscherLatticeLevel(minorG * .0625, widthPixels * 1.35, variation * .5) * affordable;
    float2 major = EscherLatticeLevel(o.majorG,        widthPixels * 1.1, variation) * affordable;
    float2 minor = EscherLatticeLevel(minorG,          widthPixels * .85, variation) * affordable;
    // Only ever finer than the material asked for when a halving was actually spent getting here.
    float2 micro = EscherLatticeLevel(minorG * 2,      widthPixels * .6,  variation)
                 * smoothstep(0, .85, budget) * saturate(steps) * affordable;

    o.bands = max(bands.x, bands.y);
    o.major = max(major.x, major.y);
    o.minor = max(minor.x, minor.y);
    o.micro = max(micro.x, micro.y);
    // Crossings, not cells: the product is only lit where both families overlap.
    o.nodes = saturate(major.x * major.y + bands.x * bands.y);

    float2 run = EscherWirePulse(o.majorG, pulseSpeed, pulseSpacing, 12, time);
    o.pulse = run.x * major.x + run.y * major.y;
    return o;
}

// Geometric normal from the position derivatives. Marching-cubes and the unwelded manifold meshes
// either carry no normals or carry flat ones, so this is the only normal available to a fragment —
// and it is the one that makes a wireframe read as a solid object instead of as a flat pattern.
float3 EscherGeometricNormal(float3 positionWS)
{
    return normalize(cross(ddy(positionWS), ddx(positionWS)) + 1e-8);
}

#endif // ESCHER_WIRE_CORE_INCLUDED
