#!/usr/bin/env python3
"""Compiles every HLSL program in the project's .shader files with glslang.

Unity is not available here, so this is a type check, not a render: each pass's HLSLINCLUDE and
HLSLPROGRAM text is compiled as HLSL for its vertex and fragment entry points, once with no keywords
and once per keyword its multi_compile / shader_feature lines declare. URP's core library, Curved
World and the studio's WorldGridScan are not in this repository; `stubs/` declares the few symbols
the shaders use from them, with their real signatures.

    apt install glslang-tools
    python3 Tools/ShaderCheck/check.py            # all shaders
    python3 Tools/ShaderCheck/check.py -v file    # one shader, printing every variant

Also compiles Rooms/EscherField4D.hlsl through a small raymarch harness.
"""
import os, re, subprocess, sys, tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STUBS = os.path.join(ROOT, "Tools", "ShaderCheck", "stubs")
GLSLANG = "glslangValidator"

def blocks(text, start):
    return [m.group(1) for m in re.finditer(start + r"(.*?)ENDHLSL", text, re.S)]

def keywords(program):
    sets = []
    for line in program.splitlines():
        m = re.match(r"\s*#pragma\s+(multi_compile|shader_feature)(_local)?(_vertex|_fragment)?\s+(.*)", line)
        if m:
            words = [w for w in m.group(4).split() if w != "_" and not w.startswith("__")]
            if words: sets.append(words)
    return sets

def compile_one(source, stage, entry, defines, verbose):
    with tempfile.TemporaryDirectory() as tmp:
        path = os.path.join(tmp, "pass.hlsl")
        with open(path, "w") as f:
            f.write("".join("#define %s 1\n" % d for d in defines) + source)
        args = [GLSLANG, "-D", "-S", stage, "-e", entry, "-V", "-I" + STUBS, "-I" + ROOT, path, "-o", os.path.join(tmp, "out.spv")]
        r = subprocess.run(args, capture_output=True, text=True)
        out = (r.stdout + r.stderr).replace(path, "pass.hlsl")
        if verbose: print("   ", stage, entry, defines or "(no keywords)", "ok" if r.returncode == 0 else "FAILED")
        return r.returncode == 0, out

def check_shader(path, verbose):
    text = open(path, encoding="utf-8-sig").read()
    shared = "\n".join(blocks(text, "HLSLINCLUDE"))
    failures = 0
    for i, program in enumerate(blocks(text, "HLSLPROGRAM")):
        vert = re.search(r"#pragma\s+vertex\s+(\w+)", program)
        frag = re.search(r"#pragma\s+fragment\s+(\w+)", program)
        variants = [[]] + [[w] for ws in keywords(shared + program) for w in ws]
        for stage, m in (("vert", vert), ("frag", frag)):
            if not m: continue
            for defines in variants:
                ok, out = compile_one(shared + "\n" + program, stage, m.group(1), defines, verbose)
                if not ok:
                    failures += 1
                    print("FAIL %s pass %d %s %s %s\n%s" % (os.path.relpath(path, ROOT), i, stage, m.group(1), defines, out))
                    break
    return failures

def check_escher_field(verbose):
    harness = ('#include "Assets/_EscherWorldManagement/Rooms/EscherField4D.hlsl"\n'
               'float4 main(float4 pos : SV_Position) : SV_Target {\n'
               '  float3 hit; float hitW; float t;\n'
               '  bool ok = EscherMarch(float3(0, 0, -3), normalize(float3(pos.xy * 0.001, 1)), 0.0, 0.1, hit, hitW, t);\n'
               '  return ok ? float4(EscherNormal(hit, hitW, 0.001) * 0.5 + 0.5, 1) : float4(0, 0, 0, 1);\n}\n')
    ok, out = compile_one(harness, "frag", "main", [], verbose)
    if not ok: print("FAIL EscherField4D.hlsl\n" + out)
    return 0 if ok else 1

def main():
    verbose = "-v" in sys.argv
    files = [a for a in sys.argv[1:] if a != "-v"]
    if not files:
        files = []
        for d, _, names in os.walk(os.path.join(ROOT, "Assets")):
            files += [os.path.join(d, n) for n in names if n.endswith(".shader")]
    failures = 0
    for f in sorted(files):
        if verbose: print(os.path.relpath(f, ROOT))
        failures += check_shader(f, verbose)
    failures += check_escher_field(verbose)
    print("%d shader(s) checked, %d failure(s)" % (len(files), failures))
    sys.exit(1 if failures else 0)

if __name__ == "__main__":
    main()
