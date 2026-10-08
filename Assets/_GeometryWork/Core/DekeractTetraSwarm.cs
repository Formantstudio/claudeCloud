using UnityEngine;
using polyhedronGenerator.scripts.solids;

namespace PsychedelicLab.GeometryFX
{
    // A 10-cube has 1024 vertices and 5120 edges. Rotate in 10D, then project into 3D.
    // Uses the installed polyhedron mesh-particle prefab, not one GameObject per particle.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DekeractTetraSwarm : MonoBehaviour
    {
        public GameObject particlePrefab;
        public Material particleMaterial;
        [Range(1,6)] public int particlesPerEdge=2;
        [Range(0,1)] public float projectionMorph=.65f;
        [Range(0,1)] public float scatter;
        public bool animateProjection=true;
        public bool breatheSwarm=true;
        public bool assembleOnPlay=true;
        [Range(.1f,3)] public float radius=1.35f;
        [Range(0,2)] public float rotationSpeed=.25f;
        [Range(1,20)] public float regroupSpeed=5;
        [Range(.002f,.04f)] public float edgeParticleSize=.008f;
        [Range(.01f,.12f)] public float vertexParticleSize=.035f;
        public Color cyan=new Color(.12f,.85f,1,.5f);
        public Color gold=new Color(1,.55f,.13f,.8f);
        public bool previewInEditor=true;
        const int Dimensions=10, Vertices=1<<Dimensions, Edges=Vertices*Dimensions/2;
        ParticleSystem system;
        GameObject instance;
        Mesh tetra;
        ParticleSystem.Particle[] particles;
        Vector3[] projected=new Vector3[Vertices];
        float[] coordinate=new float[Dimensions], cosine=new float[Dimensions*2], sine=new float[Dimensions*2];
        int[] start,end;
        int builtSamples;
        double time;
        bool initialized;
        public int ParticleCount => particles==null?0:particles.Length;

        void OnEnable() { Build(); }
        void Build()
        {
            Release();
            if(!particlePrefab || !particleMaterial || (!Application.isPlaying && !previewInEditor)) return;
            instance=Instantiate(particlePrefab,transform);
            instance.name="Dekeract - tetrahedron swarm";
            instance.hideFlags=HideFlags.HideAndDontSave;
            instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;instance.transform.localScale=Vector3.one;
            system=instance.GetComponent<ParticleSystem>();
            if(!system) { Debug.LogError("Assign polyhedronGenerator's URP wireframeParticle prefab.",this); Release();return; }
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            builtSamples=Mathf.Clamp(particlesPerEdge,1,6);
            particles=new ParticleSystem.Particle[Vertices+Edges*builtSamples];
            var main=system.main;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.maxParticles=particles.Length;main.startSpeed=0;main.gravityModifier=0;
            main.startRotation3D=true;
            var emission=system.emission;emission.enabled=false;
            var shape=system.shape;shape.enabled=false;
            var velocity=system.velocityOverLifetime;velocity.enabled=false;
            var noise=system.noise;noise.enabled=false;
            var trails=system.trails;trails.enabled=false;
            var color=system.colorOverLifetime;color.enabled=false;
            var size=system.sizeOverLifetime;size.enabled=false;
            tetra=Tetrahedon.generate(1).build("Swarm tetrahedron");tetra.hideFlags=HideFlags.HideAndDontSave;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;
            renderer.mesh=tetra;renderer.sharedMaterial=particleMaterial;renderer.enableGPUInstancing=false;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*20);
            start=new int[Edges];end=new int[Edges];int e=0;
            for(int v=0;v<Vertices;v++) for(int d=0;d<Dimensions;d++) if((v&(1<<d))==0) {start[e]=v;end[e++]=v|(1<<d);}
            initialized=false;Render(0,1);system.Pause();
        }
        void Update()
        {
            if(!Application.isPlaying && !previewInEditor) {if(instance)Release();return;}
            if(!instance || builtSamples!=Mathf.Clamp(particlesPerEdge,1,6)) Build();
            if(!system)return;
            if(Application.isPlaying) time+=Time.deltaTime;
            Render((float)time,Application.isPlaying?1-Mathf.Exp(-regroupSpeed*Mathf.Min(Time.deltaTime,.1f)):1);
        }
        void Render(float t,float follow)
        {
            if(!system)return;
            float morph=animateProjection&&Application.isPlaying?Mathf.Lerp(.15f,.95f,.5f+.5f*Mathf.Sin(t*.16f)):projectionMorph;
            float release=Mathf.Clamp01(scatter+(breatheSwarm&&Application.isPlaying?.1f*Mathf.Pow(.5f+.5f*Mathf.Sin(t*.43f),6):0));
            if(assembleOnPlay && Application.isPlaying) release=Mathf.Max(release,Mathf.Exp(-t*1.1f));
            for(int r=0;r<Dimensions*2;r++) {float angle=.27f*(r+1)+t*rotationSpeed*(.3f+.07f*r);cosine[r]=Mathf.Cos(angle);sine[r]=Mathf.Sin(angle);}
            for(int v=0;v<Vertices;v++)
            {
                for(int d=0;d<Dimensions;d++)coordinate[d]=(v&(1<<d))==0?-.31622777f:.31622777f;
                for(int r=0;r<Dimensions*2;r++)
                {
                    int a=r%Dimensions,b=(a+3)%Dimensions;float x=coordinate[a],y=coordinate[b];
                    coordinate[a]=x*cosine[r]-y*sine[r];coordinate[b]=x*sine[r]+y*cosine[r];
                }
                Vector3 p=new Vector3(coordinate[0],coordinate[1],coordinate[2])*1.55f;
                Vector3 cube=new Vector3((v&1)==0?-.65f:.65f,(v&2)==0?-.65f:.65f,(v&4)==0?-.65f:.65f)+p*.18f;
                projected[v]=Vector3.Lerp(cube,p,morph)*radius;
                Set(v,projected[v],true,t,release,follow);
            }
            int index=Vertices;
            for(int e=0;e<Edges;e++)for(int j=0;j<builtSamples;j++)
            {
                float phase=Mathf.Repeat((j+.5f)/builtSamples+t*.09f,1);
                Set(index++,Vector3.Lerp(projected[start[e]],projected[end[e]],phase),false,t,release,follow);
            }
            system.SetParticles(particles,particles.Length);initialized=true;
        }
        void Set(int i,Vector3 target,bool node,float t,float release,float follow)
        {
            float h=Mathf.Repeat(i*.61803399f,1),azimuth=h*Mathf.PI*2+t*.24f;
            float y=Mathf.Repeat(i*.75487766f,1)*2-1,ring=Mathf.Sqrt(Mathf.Max(0,1-y*y));
            Vector3 free=new Vector3(Mathf.Cos(azimuth)*ring,y,Mathf.Sin(azimuth)*ring)*(radius*1.8f);
            target=Vector3.Lerp(target,free,release);
            ref var p=ref particles[i];p.position=initialized?Vector3.Lerp(p.position,target,follow):target;
            p.startLifetime=1000;p.remainingLifetime=1000;p.velocity=Vector3.zero;
            p.startSize=node?vertexParticleSize:edgeParticleSize;
            p.startColor=Color.Lerp(cyan,gold,node?.6f+.4f*h:h*.3f);
            p.rotation3D=new Vector3(h*360+t*13,h*150-t*19,h*270+t*7);
            p.randomSeed=(uint)i+1;
        }
        [ContextMenu("Scatter")]
        public void Scatter() { scatter=1; }
        [ContextMenu("Reassemble")]
        public void Reassemble() { scatter=0; }
        void OnDisable() {Release();}
        void Release() {Dispose(instance);Dispose(tetra);instance=null;system=null;tetra=null;particles=null;initialized=false;}
        static void Dispose(Object value) {if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
