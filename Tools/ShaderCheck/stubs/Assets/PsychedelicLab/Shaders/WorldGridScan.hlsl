// Stand-in for the studio's WorldGridScan.hlsl (not in this repository): the Shader Graph custom
// function's signature, returning no highlight.
#ifndef STUB_WORLD_GRID_SCAN
#define STUB_WORLD_GRID_SCAN
void WorldGridHighlighting_float(float3 positionWS, float time, out float scan) { scan = 0.0; }
#endif
