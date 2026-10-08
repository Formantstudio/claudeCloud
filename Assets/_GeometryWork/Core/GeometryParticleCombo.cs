using UnityEngine;
using UnityEngine.VFX;
using polyhedronGenerator.scripts;
using polyhedronGenerator.scripts.solids;

namespace PsychedelicLab.GeometryFX
{
    [DisallowMultipleComponent]
    public sealed class GeometryParticleCombo : MonoBehaviour
    {
        public enum Form { Tetrahedron, StarTetrahedron, Dodecahedron, Sierpinski, TrinityRings, Octahedron, TetraCube }
        public enum Detail { Preview = 64, Standard = 96, High = 128 }
        public ComputeShader fieldShader;
        public VisualEffectAsset particleGraph;
        public Form from = Form.Tetrahedron, to = Form.StarTetrahedron;
        [Range(0,1)] public float morph;
        public bool autoCycle = true;
        [Min(1)] public float secondsPerForm = 10;
        [Range(0.1f,0.9f)] public float transitionFraction = .45f;
        public AnimationCurve transition = AnimationCurve.EaseInOut(0,0,1,1);
        public Detail detail = Detail.Standard;
        [Range(1,5)] public int fractalDepth = 3;
        [Range(-2,2)] public float twist = .12f;
        [Range(1000,80000)] public float particlesPerSecond = 24000;
        [Range(.002f,.05f)] public float surfaceThickness = .007f;
        public Gradient palette;
        [Header("Particle look")]
        [Tooltip("Full control over the sparkle cloud, including the size the stock SDF graph had no handle for. Duplicate SdfSparkleParticles.vfx to branch a variant and drop it in here.")]
        public SdfParticleSettings particleSettings = new SdfParticleSettings();
        [Tooltip("Applies a named starting point to the settings above, then returns to Custom.")]
        public SdfParticleSettings.Preset applyPreset = SdfParticleSettings.Preset.Custom;
        public bool showControls = true;
        public GameObject flowpathLayer, parallaxLayer, symbolLayer;
        public DekeractTetraSwarm tetraSwarm;
        public bool showFlowpath = true, showParallax = false, showSymbols = true;
        public string Status { get; private set; } = "Play to preview the particle morph";
        readonly SdfParticleRig rig = new SdfParticleRig();
        RenderTexture volume;
        ComputeShader compute;
        int kernel, resolution;
        double elapsed;
        const float HalfExtent = 1.35f;

        /// <summary>Folds the older flat fields into the settings block the first time this runs.</summary>
        void MigrateLegacyFields()
        {
            if (particleSettings == null) particleSettings = new SdfParticleSettings();
            if (particleSettings.graph) return;
            particleSettings.graph = particleGraph;
            particleSettings.spawnRate = Mathf.Max(particleSettings.spawnRate, particlesPerSecond);
            particleSettings.stickDistance = surfaceThickness;
            if (palette != null) particleSettings.palette = palette;
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supports3DRenderTextures ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
            { Status = "This GPU cannot render the 3D particle field."; Debug.LogWarning(Status, this); return; }
            MigrateLegacyFields();
            if (!fieldShader || !particleSettings.graph) { Status = "Assign the field shader and particle graph."; Debug.LogWarning(Status, this); return; }
            compute = Instantiate(fieldShader);
            kernel = compute.FindKernel("Bake");
            var planes = new Vector4[32];
            FillPlanes(Tetrahedon.generate(1), planes, 0);
            FillPlanes(Dodecahedron.generate(1), planes, 4);
            FillPlanes(Octahedron.generate(1), planes, 16);
            compute.SetVectorArray("_Planes", planes);
            rig.Build(transform, "Morphing SDF particles", particleSettings.graph, Vector3.zero, HalfExtent * 2);
            BuildVolume();
            elapsed = 0;
        }

        static void FillPlanes(MeshBuilder shape, Vector4[] target, int offset)
        {
            for (int i=0; i<shape.faces.Count; i++)
            {
                var f = shape.faces[i];
                Vector3 a = shape.vectorList[f[0]], b = shape.vectorList[f[1]], c = shape.vectorList[f[2]];
                Vector3 normal = Vector3.Cross(b-a,c-a).normalized;
                if (Vector3.Dot(normal,a) < 0) normal = -normal;
                target[offset+i] = new Vector4(normal.x,normal.y,normal.z,Vector3.Dot(normal,a));
            }
        }
        void BuildVolume()
        {
            if (volume) { volume.Release(); Destroy(volume); }
            resolution = (int)detail;
            volume = new RenderTexture(resolution,resolution,0,RenderTextureFormat.RHalf) {
                name="Geometry morph field", dimension=UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth=resolution, enableRandomWrite=true, wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Bilinear, useMipMap=false
            };
            volume.Create();
            rig.SetField(volume);
            Bake(0);
            rig.Reinit();
        }
        void Update()
        {
            SetLayer(flowpathLayer,showFlowpath); SetLayer(parallaxLayer,showParallax); SetLayer(symbolLayer,showSymbols);
            if (applyPreset != SdfParticleSettings.Preset.Custom)
            {
                particleSettings.Apply(applyPreset);
                applyPreset = SdfParticleSettings.Preset.Custom;
            }
            if (!compute || rig.Effect == null) return;
            if (rig.NeedsRebuild(particleSettings.graph))
            {
                rig.Build(transform,"Morphing SDF particles",particleSettings.graph,Vector3.zero,HalfExtent*2);
                BuildVolume();
                if (rig.Effect == null) return;
            }
            if (resolution != (int)detail) BuildVolume();
            elapsed += Time.deltaTime;
            if (autoCycle)
            {
                double cycle = elapsed / Mathf.Max(1,secondsPerForm);
                int index = (int)(cycle % 7);
                from=(Form)index; to=(Form)((index+1)%7);
                float phase=(float)(cycle-System.Math.Floor(cycle));
                morph = transition.Evaluate(Mathf.InverseLerp(1-transitionFraction,1,phase));
            }
            Bake((float)elapsed);
            // Keep the two legacy sliders live: they still drive the settings block.
            particleSettings.spawnRate = particlesPerSecond;
            particleSettings.stickDistance = surfaceThickness;
            if (palette != null) particleSettings.palette = palette;
            rig.Push(particleSettings);
            Status = rig.HasSizeControl
                ? from + " → " + to + "  ·  sparkle " + rig.WorldParticleSize(particleSettings).ToString("0.####") + " units"
                : from + " → " + to + "  ·  graph has no ParticleSize parameter";
        }
        void Bake(float time)
        {
            compute.SetTexture(kernel,"_Out",volume);
            compute.SetVector("_Config",new Vector4(resolution,HalfExtent,Mathf.Clamp01(morph),twist));
            compute.SetInt("_From",(int)from); compute.SetInt("_To",(int)to);
            compute.SetInt("_Depth",Mathf.Clamp(fractalDepth,1,5)); compute.SetFloat("_TimeValue",time);
            compute.Dispatch(kernel,(resolution+7)/8,(resolution+7)/8,(resolution+7)/8);
        }
        static void SetLayer(GameObject go,bool visible) { if(go && go.activeSelf!=visible) go.SetActive(visible); }
        public void SetMorph(float value) { autoCycle=false; morph=Mathf.Clamp01(value); }
        public void NextForm() { autoCycle=false; from=to; to=(Form)(((int)to+1)%7); morph=0; }
        void OnDisable()
        {
            rig.Release();
            if(volume) { volume.Release(); Destroy(volume); }
            if(compute) Destroy(compute);
            volume=null; compute=null;
        }
        void OnGUI()
        {
            if(!showControls) return;
            GUILayout.BeginArea(new Rect(16,16,280,tetraSwarm?385:300),GUI.skin.box);
            GUILayout.Label("GEOMETRY / PARTICLE COMBO"); GUILayout.Label(Status);
            autoCycle=GUILayout.Toggle(autoCycle,"Automatic morph sequence");
            if(!autoCycle) { if(GUILayout.Button("Next form pair")) NextForm(); morph=GUILayout.HorizontalSlider(morph,0,1); }
            showFlowpath=GUILayout.Toggle(showFlowpath,"Flowpath geometry");
            showParallax=GUILayout.Toggle(showParallax,"Parallax depth panel");
            showSymbols=GUILayout.Toggle(showSymbols,"Geometry symbols");
            GUILayout.Label("Density"); particlesPerSecond=GUILayout.HorizontalSlider(particlesPerSecond,1000,80000);
            GUILayout.Label("Particle size "+particleSettings.sizeScale.ToString("0.00"));
            particleSettings.sizeScale=GUILayout.HorizontalSlider(particleSettings.sizeScale,.02f,8f);
            if(tetraSwarm)
            {
                GUILayout.Label("Tetrahedron swarm / reassemble");
                tetraSwarm.scatter=GUILayout.HorizontalSlider(tetraSwarm.scatter,0,1);
                tetraSwarm.animateProjection=GUILayout.Toggle(tetraSwarm.animateProjection,"Morph cube into dekeract");
                if(!tetraSwarm.animateProjection) tetraSwarm.projectionMorph=GUILayout.HorizontalSlider(tetraSwarm.projectionMorph,0,1);
            }
            GUILayout.EndArea();
        }
        void OnDrawGizmosSelected() { Gizmos.matrix=transform.localToWorldMatrix; Gizmos.color=Color.cyan; Gizmos.DrawWireCube(Vector3.zero,Vector3.one*HalfExtent*2); }
    }
}
