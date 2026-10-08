using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PsychedelicLab.GeometryFX;

namespace PsychedelicLab.GeometryFX.EditorTools
{
    /// <summary>
    /// Combines the two installed libraries that were sitting unused next to each other:
    /// **polyhedronGenerator** supplies the particle meshes (5 Platonics, prisms, antiprisms and
    /// all 92 Johnson solids) and **GeometryFXParticles** supplies the materials (170 of them, in
    /// seven groups). A swarm role takes one of each, so the pairing is the palette.
    ///
    ///   Circles 24 · Grids 1 · IrregularShapes 31 · Lines&amp;Dots 54 · Polygons 13 ·
    ///   Symbols 31 · Triangles 16
    ///
    /// That is 170 materials against ~97 meshes: more pairings than could be tried by hand, which
    /// is the point of rolling them rather than picking them.
    /// </summary>
    static class ParticleLibraryTools
    {
        const string Library = "Assets/GeometryFXParticles/Materials";
        const string Prefab = "Assets/polyhedronGenerator/prefabs/urp/wireframeParticle.prefab";

        static readonly string[] Groups =
            { "Circles", "Grids", "IrregularShapes", "Lines&Dots", "Polygons", "Symbols", "Triangles" };

        /// <summary>Every material in the library, or just one group.</summary>
        public static List<Material> Load(string group = null)
        {
            var found = new List<Material>();
            string root = string.IsNullOrEmpty(group) ? Library : Library + "/" + group;
            if (!Directory.Exists(root)) return found;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat) found.Add(mat);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return found;
        }

        // ---- rolling ---------------------------------------------------------

        [MenuItem("Tools/Geometry FX/Particle library/Roll materials on selection", false, 120)]
        static void RollSelection() => Roll(null, false);

        [MenuItem("Tools/Geometry FX/Particle library/Roll materials + meshes on selection", false, 121)]
        static void RollBoth() => Roll(null, true);

        [MenuItem("Tools/Geometry FX/Particle library/Roll from Circles", false, 140)]
        static void RollCircles() => Roll("Circles", false);

        [MenuItem("Tools/Geometry FX/Particle library/Roll from Lines and Dots", false, 141)]
        static void RollLines() => Roll("Lines&Dots", false);

        [MenuItem("Tools/Geometry FX/Particle library/Roll from Symbols", false, 142)]
        static void RollSymbols() => Roll("Symbols", false);

        [MenuItem("Tools/Geometry FX/Particle library/Roll from Triangles", false, 143)]
        static void RollTriangles() => Roll("Triangles", false);

        [MenuItem("Tools/Geometry FX/Particle library/Roll from Irregular Shapes", false, 144)]
        static void RollIrregular() => Roll("IrregularShapes", false);

        [MenuItem("Tools/Geometry FX/Particle library/Roll from Polygons", false, 145)]
        static void RollPolygons() => Roll("Polygons", false);

        /// <summary>
        /// Gives every role on the selected objects a different material from the library, and
        /// optionally a different polyhedron to draw with.
        /// </summary>
        static void Roll(string group, bool rollMeshes)
        {
            var library = Load(group);
            if (library.Count == 0)
            {
                Debug.LogError("Particle library: no materials found under " + Library +
                               (group == null ? "" : "/" + group));
                return;
            }

            var engines = Selected<GeometricParticleSwarmEngine>();
            if (engines.Count == 0)
            {
                Debug.LogWarning("Select one or more objects with a GeometricParticleSwarmEngine.");
                return;
            }

            // Every mesh the polyhedron library can draw, Johnson solids included.
            var meshes = new List<(SwarmMesh mesh, int johnson)>();
            foreach (SwarmMesh m in System.Enum.GetValues(typeof(SwarmMesh)))
                if (m != SwarmMesh.Custom) meshes.Add((m, 0));

            int rolled = 0;
            foreach (var engine in engines)
            {
                Undo.RecordObject(engine, "Roll particle library");
                for (int i = 0; i < engine.roles.Count; i++)
                {
                    var role = engine.roles[i];
                    if (role == null) continue;
                    role.material = library[Random.Range(0, library.Count)];
                    if (rollMeshes)
                    {
                        var pick = meshes[Random.Range(0, meshes.Count)];
                        role.mesh = pick.mesh;
                    }
                    rolled++;
                }
                EditorUtility.SetDirty(engine);
            }

            Debug.Log(string.Format("Rolled {0} role(s) across {1} engine(s) from {2} material(s){3}.",
                rolled, engines.Count, library.Count,
                group == null ? " in the whole library" : " in " + group));
        }

        // ---- stepping, for walking the library one at a time -----------------

        [MenuItem("Tools/Geometry FX/Particle library/Next material on selection", false, 160)]
        static void Next() => Step(1);

        [MenuItem("Tools/Geometry FX/Particle library/Previous material on selection", false, 161)]
        static void Previous() => Step(-1);

        static void Step(int delta)
        {
            var library = Load();
            if (library.Count == 0) { Debug.LogError("Particle library: nothing found."); return; }

            var engines = Selected<GeometricParticleSwarmEngine>();
            if (engines.Count == 0) { Debug.LogWarning("Select a GeometricParticleSwarmEngine."); return; }

            foreach (var engine in engines)
            {
                Undo.RecordObject(engine, "Step particle library");
                foreach (var role in engine.roles)
                {
                    if (role == null) continue;
                    int at = role.material ? library.IndexOf(role.material) : -1;
                    int next = at < 0 ? 0 : ((at + delta) % library.Count + library.Count) % library.Count;
                    role.material = library[next];
                }
                EditorUtility.SetDirty(engine);
            }
            var first = engines[0].roles.Count > 0 && engines[0].roles[0] != null
                ? engines[0].roles[0].material : null;
            Debug.Log("Stepped the library" + (first ? " to " + first.name : "") +
                      " (" + library.Count + " materials).");
        }

        // ---- a combo object, ready to roll -----------------------------------

        [MenuItem("Tools/Geometry FX/Particle library/Add a polyhedron x library combo", false, 180)]
        static void AddCombo()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (!prefab)
            {
                Debug.LogError("Missing " + Prefab + " — the swarm needs a prefab with a ParticleSystem.");
                return;
            }
            var library = Load();
            if (library.Count == 0) { Debug.LogError("Particle library: nothing found."); return; }

            var host = new GameObject("Polyhedron x Library Combo");
            Undo.RegisterCreatedObjectUndo(host, "Add polyhedron x library combo");
            var engine = Undo.AddComponent<GeometricParticleSwarmEngine>(host);
            Undo.RecordObject(engine, "Add polyhedron x library combo");

            engine.sources.Clear();
            engine.roles.Clear();

            // A Johnson solid as the structure, since that is the richest thing the polyhedron
            // library offers and it was never used for anything.
            engine.sources.Add(new ShapeSource
            {
                label = "Johnson solid",
                kind = ShapeSourceKind.Polyhedron,
                solid = PolyhedronSwarm.Solid.Johnson,
                shellRadiusSquared = 16,   // doubles as the Johnson index on this source
                maxEdges = 600
            });

            // Three roles, three different meshes, three different library materials.
            engine.roles.Add(new SwarmRole
            {
                label = "Girders", mesh = SwarmMesh.Tetrahedron, particlePrefab = prefab,
                material = library[Random.Range(0, library.Count)],
                orient = SwarmOrient.AlongEdge, segments = 10, thickness = .018f, fill = 1.15f
            });
            engine.roles.Add(new SwarmRole
            {
                label = "Joints", mesh = SwarmMesh.Icosahedron, particlePrefab = prefab,
                material = library[Random.Range(0, library.Count)],
                orient = SwarmOrient.Tumble, size = .08f, segments = 0, minEdgeLength = 999f
            });
            engine.roles.Add(new SwarmRole
            {
                label = "Sparks", mesh = SwarmMesh.Octahedron, particlePrefab = prefab,
                material = library[Random.Range(0, library.Count)],
                orient = SwarmOrient.Tumble, size = .012f, segments = 3
            });
            engine.applyFrame = GeometricParticleSwarmEngine.Frame.Custom;
            EditorUtility.SetDirty(engine);

            Selection.activeGameObject = host;
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Combo added: a Johnson solid framed by three polyhedron meshes with three " +
                      "GeometryFXParticles materials. Roll the materials from this same menu.");
        }

        static List<T> Selected<T>() where T : Component
        {
            var list = new List<T>();
            foreach (var go in Selection.gameObjects)
            {
                var c = go.GetComponent<T>();
                if (c) list.Add(c);
            }
            return list;
        }
    }
}
