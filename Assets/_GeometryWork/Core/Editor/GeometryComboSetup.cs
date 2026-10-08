using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using AmazingAssets.CurvedWorld;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX.Editor
{
    // This requested upgrade is scoped to the existing demo. Never opens or replaces a scene.
    [InitializeOnLoad]
    public static class GeometryComboSetup
    {
        const string ScenePath="Assets/GeometryFXParticles/DemoScene.unity";
        const string ProceduralTunnelScenePath="Assets/Scenes/ProceduralTunnelTester.unity";
        const string Folder="Assets/_GeometryWork/Core";
        const string RootName="Geometry Particle Combo";
        static GeometryComboSetup()
        {
            EditorApplication.delayCall += TryInstall;
            EditorApplication.playModeStateChanged += state => { if(state==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall+=TryInstall; };
        }
        static void TryInstall()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall+=TryInstall; return; }
            if(EditorApplication.isPlayingOrWillChangePlaymode || !SupportedScene(SceneManager.GetActiveScene().path))
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText("Temp/GeometryComboPending.txt",$"ActiveScene={SceneManager.GetActiveScene().path}\nPlaying={EditorApplication.isPlayingOrWillChangePlaymode}\n");
                return;
            }
            var existing=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x=>x.name==RootName);
            if(existing) { UpgradeDekeract(existing); return; }
            Install();
        }
        [MenuItem("Tools/Geometry FX/Upgrade Current Demo")]
        public static void Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(!SupportedScene(scene.path) || EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Open GeometryFXParticles/DemoScene or ProceduralTunnelTester outside Play mode first."); return; }
            var existing=scene.GetRootGameObjects().FirstOrDefault(x=>x.name==RootName);
            if(existing) { UpgradeDekeract(existing); return; }
            var compute=AssetDatabase.LoadAssetAtPath<ComputeShader>(Folder+"/GeometryMorph.compute");
            var graph=AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/PsychedelicLab/Stage4D/FractalSdfParticles.vfx");
            var additive=Shader.Find("PsychedelicLab/Geometry Additive");
            if(!compute || !graph || !additive) { Debug.LogError("Geometry combo dependencies have not imported yet. Run Tools/Geometry FX/Upgrade Current Demo after import."); return; }
            Undo.IncrementCurrentGroup(); int undo=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Upgrade Geometry FX demo");
            RepairMaterials(additive);
            var root=new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root,"Create geometry combo");
            var combo=root.AddComponent<GeometryParticleCombo>(); combo.fieldShader=compute; combo.particleGraph=graph;
            combo.palette=new Gradient();
            combo.palette.SetKeys(new[]{new GradientColorKey(new Color(.18f,.85f,.9f),0),new GradientColorKey(new Color(.8f,.65f,.3f),.55f),new GradientColorKey(new Color(.28f,.3f,.65f),1)},new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(.7f,1)});
            // Keep the original library intact and available as a separate scene root.
            foreach(var viewer in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<AssetsViewer>(true)))
            { Undo.RecordObject(viewer.gameObject,"Keep original particle library"); viewer.gameObject.SetActive(false); }
            foreach(var ground in scene.GetRootGameObjects().Where(x=>x.name=="Ground"))
            { Undo.RecordObject(ground,"Hide demo floor"); ground.SetActive(false); }
            var camera=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if(camera)
            {
                Undo.RecordObject(camera,"Frame particle core"); camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.006f,.009f,.018f); camera.allowHDR=true;
                var data=camera.GetUniversalAdditionalCameraData(); Undo.RecordObject(data,"Enable bloom"); data.renderPostProcessing=true;
                // Keep the user's existing camera transform/orbit controller.
            }
            AddVolume(root);
            var chamberShader=Shader.Find("PsychedelicLab/Curved Geometry Chamber");
            if(chamberShader)
            {
                var chamber=root.AddComponent<CurvedGeometryChamber>();
                chamber.chamberMaterial=MaterialAsset("CurvedChamber",chamberShader,Color.white);
                var scan=root.AddComponent<PsychedelicLab.Control.WorldGridScan>();
                scan.preset=PsychedelicLab.Control.WorldGridScan.Preset.Weave;
                scan.ApplyPreset(scan.preset); scan.glitch=.08f; scan.reactToShapes=false; scan.leaveGuidesLayer=false;
                chamber.worldGridScan=scan;
                EditorUtility.SetDirty(chamber);
            }
            combo.flowpathLayer=AddFlowpath(root,additive);
            combo.parallaxLayer=AddParallax(root);
            combo.symbolLayer=AddSymbols(root);
            AddDekeract(root);
            EnsureCurvedController(root);
            EditorUtility.SetDirty(combo);
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved=EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/GeometryComboSetup.txt",$"Saved={saved}\nScene={scene.path}\nGraph={AssetDatabase.GetAssetPath(graph)}\nFlowpath={combo.flowpathLayer!=null}\nParallax={combo.parallaxLayer!=null}\nSymbols={combo.symbolLayer!=null}\n");
            Selection.activeGameObject=root;
            Debug.Log("Geometry FX combo installed in the existing DemoScene. Play to morph; select Geometry Particle Combo for detail, shapes and layer controls.");
        }
        static bool SupportedScene(string path) => path==ScenePath || path==ProceduralTunnelScenePath;
        static void UpgradeDekeract(GameObject root)
        {
            bool changed=AddDekeract(root);
            var chamber=root.GetComponent<CurvedGeometryChamber>();
            if(chamber && !chamber.worldGridScan)
            {
                var scan=root.GetComponent<PsychedelicLab.Control.WorldGridScan>();
                if(!scan)
                {
                    scan=Undo.AddComponent<PsychedelicLab.Control.WorldGridScan>(root);
                    scan.preset=PsychedelicLab.Control.WorldGridScan.Preset.Weave;
                    scan.ApplyPreset(scan.preset);scan.glitch=.08f;scan.reactToShapes=false;scan.leaveGuidesLayer=false;
                }
                Undo.RecordObject(chamber,"Connect WorldGridScan");chamber.worldGridScan=scan;EditorUtility.SetDirty(chamber);changed=true;
            }
            changed=EnsureCurvedController(root)||changed;
            if(!changed) return;
            EditorSceneManager.MarkSceneDirty(root.scene);
            bool saved=EditorSceneManager.SaveScene(root.scene);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/DekeractSetup.txt",$"Saved={saved}\nScene={root.scene.path}\nVertices=1024\nEdges=5120\nDefaultMeshParticles=11264\n");
            File.WriteAllText("Temp/GeometryCurvedSetup.txt",$"Saved={saved}\nScene={root.scene.path}\nSharedBridge={chamber && chamber.bridge}\nFlowpath={chamber && chamber.flowpathRoot}\n");
            Debug.Log("Dekeract tetrahedron swarm added to the existing Geometry FX demo.");
        }
        static bool EnsureCurvedController(GameObject root)
        {
            var scene=root.scene;
            var holder=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Curved Controller");
            bool changed=false;
            if(!holder)
            {
                holder=new GameObject("Curved Controller");
                Undo.RegisterCreatedObjectUndo(holder,"Create Curved Controller");changed=true;
            }
            var bridge=holder.GetComponent<CurvedWorldBridge>();
            if(!bridge)
            {
                bridge=Undo.AddComponent<CurvedWorldBridge>(holder);
                bridge.amount=1;bridge.intensity=1;bridge.preset=3;bridge.customBend=true;
                bridge.customShape=BendType.TwistedSpiral_Z_Positive;
                bridge.customCurvature=3;bridge.customHorizontal=.12f;bridge.customVertical=.08f;
                bridge.customAxis=Vector3.forward;bridge.pivotFollowsCamera=true;changed=true;
            }
            var manager=holder.GetComponent<UserCurvedControllerManager>();
            if(!manager) {manager=Undo.AddComponent<UserCurvedControllerManager>(holder);changed=true;}
            if(manager.slots.Count==0)
            {
                Undo.RecordObject(manager,"Set curved bend slots");
                manager.startingSlot=1;manager.transitionSeconds=1.5f;
                manager.slots.Add(new UserCurvedControllerManager.Slot {label="Off",preset=0,amount=0});
                manager.slots.Add(Spiral("Gentle Chamber Twist",3,.12f,.08f));
                manager.slots.Add(new UserCurvedControllerManager.Slot {label="Cylinder Rolloff",preset=3,amount=.35f});
                manager.slots.Add(new UserCurvedControllerManager.Slot {label="Cylinder Tower",preset=2,amount=.3f});
                manager.slots.Add(new UserCurvedControllerManager.Slot {label="Classic Runner",preset=4,amount=.25f});
                manager.slots.Add(Spiral("Big Tetrahedral Twist",20,3f,1.8f));
                manager.slots.Add(new UserCurvedControllerManager.Slot {label="Little Planet",preset=1,amount=.12f});
                EditorUtility.SetDirty(manager);changed=true;
            }
            var automation=holder.GetComponent<CurvedWorldAutomation>();
            if(!automation)
            {
                automation=Undo.AddComponent<CurvedWorldAutomation>(holder);
                automation.fallbackBpm=120;
                automation.motions.Clear();
                automation.motions.Add(new CurvedWorldAutomation.Motion {enabled=true,momentOnly=false,wave=CurvedWorldAutomation.Wave.Sine,period=CurvedWorldAutomation.Period.FourBars,parameter=CurvedWorldAutomation.Parameter.Curvature,low=2,high=4,strength=.15f});
                automation.motions.Add(new CurvedWorldAutomation.Motion {enabled=true,momentOnly=true,wave=CurvedWorldAutomation.Wave.Sine,period=CurvedWorldAutomation.Period.OneBar,parameter=CurvedWorldAutomation.Parameter.Curvature,low=3,high=24,strength=1});
                EditorUtility.SetDirty(automation);changed=true;
            }
            var chamber=root.GetComponent<CurvedGeometryChamber>();
            var combo=root.GetComponent<GeometryParticleCombo>();
            if(chamber)
            {
                bool assign=chamber.bridge!=bridge || chamber.flowpathRoot==null && combo && combo.flowpathLayer;
                if(assign)
                {
                    Undo.RecordObject(chamber,"Connect shared Curved World bridge");
                    chamber.bridge=bridge;
                    if(!chamber.flowpathRoot && combo) chamber.flowpathRoot=combo.flowpathLayer;
                    EditorUtility.SetDirty(chamber);changed=true;
                }
            }
            return changed;
        }
        static UserCurvedControllerManager.Slot Spiral(string label,float curvature,float horizontal,float vertical)
        {
            return new UserCurvedControllerManager.Slot {label=label,preset=3,amount=1,intensity=1,customBend=true,
                customShape=BendType.TwistedSpiral_Z_Positive,customCurvature=curvature,
                customHorizontal=horizontal,customVertical=vertical,customAxis=Vector3.forward,pivotFollowsCamera=true};
        }
        static bool AddDekeract(GameObject root)
        {
            if(root.GetComponentInChildren<DekeractTetraSwarm>(true)) return false;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/polyhedronGenerator/prefabs/urp/wireframeParticle.prefab");
            var shader=Shader.Find("PsychedelicLab/Geometry Additive");
            if(!prefab || !shader) { Debug.LogError("Dekeract requires the installed URP wireframeParticle prefab and Geometry Additive shader.");return false; }
            var go=new GameObject("Dekeract Tetrahedron Swarm");go.transform.SetParent(root.transform,false);
            Undo.RegisterCreatedObjectUndo(go,"Add dekeract tetrahedron swarm");
            var swarm=go.AddComponent<DekeractTetraSwarm>();
            swarm.particlePrefab=prefab;
            swarm.particleMaterial=MaterialAsset("Dekeract_Particles",shader,new Color(1.5f,1.5f,1.5f,.5f));
            EditorUtility.SetDirty(swarm);
            var combo=root.GetComponent<GeometryParticleCombo>();
            if(combo)
            {
                Undo.RecordObject(combo,"Set tetra cube particle core");
                combo.from=GeometryParticleCombo.Form.TetraCube;combo.to=GeometryParticleCombo.Form.StarTetrahedron;
                combo.autoCycle=false;combo.morph=0;combo.particlesPerSecond=10000;combo.twist=.06f;
                combo.tetraSwarm=swarm;
                EditorUtility.SetDirty(combo);
            }
            return true;
        }
        static void RepairMaterials(Shader additive)
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/GeometryFXParticles"}))
            {
                var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(!mat || (mat.shader && mat.shader.name.StartsWith("beffio/"))) continue;
                string shaderName=mat.shader?mat.shader.name:"";
                if(shaderName.Contains("Particles") || !mat.shader || shaderName.Contains("InternalError"))
                {
                    Undo.RecordObject(mat,"Convert legacy geometry particles");
                    // Existing built-in additive variants retain texture and tint property names.
                    mat.shader=shaderName.IndexOf("Add",StringComparison.OrdinalIgnoreCase)>=0 ? additive : Shader.Find("beffio/ParticleSimpleAlphaBlend");
                    EditorUtility.SetDirty(mat);
                }
                else if(shaderName=="Standard" || shaderName.StartsWith("Legacy Shaders/") || shaderName=="Unlit/Color" || shaderName=="Unlit/Texture")
                {
                    Undo.RecordObject(mat,"Convert geometry environment to URP");
                    Texture texture=mat.HasProperty("_MainTex")?mat.GetTexture("_MainTex"):null;
                    Color color=mat.HasProperty("_Color")?mat.GetColor("_Color"):Color.white;
                    mat.shader=Shader.Find("Universal Render Pipeline/Unlit"); mat.SetColor("_BaseColor",color);
                    if(texture) mat.SetTexture("_BaseMap",texture);
                    EditorUtility.SetDirty(mat);
                }
            }
            AssetDatabase.SaveAssets();
        }
        static Material MaterialAsset(string name,Shader shader,Color tint)
        {
            string path=Folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material) return material;
            material=new Material(shader){name=name};
            if(material.HasProperty("_TintColor")) material.SetColor("_TintColor",tint);
            AssetDatabase.CreateAsset(material,path); return material;
        }
        static void AddVolume(GameObject root)
        {
            string path=Folder+"/GeometryAtmosphere.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,path);
                var bloom=profile.Add<Bloom>(true); bloom.intensity.Override(.35f); bloom.threshold.Override(1f); bloom.scatter.Override(.55f);
                AssetDatabase.AddObjectToAsset(bloom,profile); EditorUtility.SetDirty(profile);
            }
            var go=new GameObject("Particle glow"); go.transform.SetParent(root.transform,false);
            var volume=go.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=5; volume.sharedProfile=profile;
        }
        static GameObject AddFlowpath(GameObject root,Shader shader)
        {
            var meshes=AssetDatabase.LoadAllAssetsAtPath("Assets/Tazlel/Shapes FlowPath/Models/Tunnels_Assets.fbx").OfType<Mesh>().Where(x=>x.vertexCount>0).OrderBy(x=>x.name).ToArray();
            if(meshes.Length==0) return null;
            var mesh=meshes.FirstOrDefault(x=>x.name.Contains("001"))??meshes[0];
            var holder=new GameObject("Flowpath - geometric filigree"); holder.transform.SetParent(root.transform,false);
            var curved=Shader.Find("PsychedelicLab/Curved Geometry Accent");
            var material=MaterialAsset("Flowpath_Cyan",curved?curved:shader,new Color(.06f,.23f,.25f,.35f));
            for(int i=0;i<2;i++)
            {
                var ring=new GameObject("Flowpath shell "+(i+1)); ring.transform.SetParent(holder.transform,false);
                ring.transform.localRotation=Quaternion.Euler(i*90,0,i*30);
                var content=new GameObject(mesh.name); content.transform.SetParent(ring.transform,false);
                float scale=3.8f/Mathf.Max(.001f,mesh.bounds.size.magnitude);
                content.transform.localScale=Vector3.one*scale; content.transform.localPosition=-mesh.bounds.center*scale;
                content.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=content.AddComponent<MeshRenderer>(); renderer.sharedMaterials=Enumerable.Repeat(material,mesh.subMeshCount).ToArray();
                renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
                renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*200/scale);
            }
            return holder;
        }
        static GameObject AddParallax(GameObject root)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameNote/Parallax 3D Free/Samples/Prefabs/ParallaxRoom.prefab");
            if(!prefab) return null;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform); go.name="Parallax depth panel (optional)";
            go.transform.localPosition=new Vector3(0,0,2.5f); go.transform.localScale=new Vector3(6,4,3);
            go.SetActive(false); return go;
        }
        static GameObject AddSymbols(GameObject root)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeometryFXParticles/Prefabs/SimpleParticles/Symbols/SymbolPrefab_1.prefab");
            if(!prefab) return null;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform); go.name="Geometry symbol accents";
            go.transform.localPosition=Vector3.zero; go.transform.localScale=Vector3.one*.4f;
            foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main=ps.main; main.maxParticles=96; main.startColor=new Color(.2f,.6f,.7f,.35f);
                var emission=ps.emission; emission.rateOverTime=6;
            }
            return go;
        }
    }
}
