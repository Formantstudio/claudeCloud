// Stand-ins for the studio packages that are not in this repository (Curved World, the control
// rig, WorldGridScan, WireParticleSwarm, Camera). Only the members the compiled files touch.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace AmazingAssets.CurvedWorld {
  public enum BendType { ClassicRunner_X_Positive, LittlePlanet_Y, CylindricalTower_X, CylindricalRolloff_Z, TwistedSpiral_X_Positive, TwistedSpiral_Z_Positive }
  public class CurvedWorldController : MonoBehaviour {
    public enum AxisType { Custom }
    public int bendID; public bool manualUpdate; public BendType bendType; public AxisType bendRotationAxisType;
    public Vector3 bendRotationAxis, bendPivotPointPosition; public float bendCurvatureSize, bendHorizontalSize, bendVerticalSize, bendCurvatureOffset;
    public void ManualUpdate() { }
  }
}
namespace PsychedelicLab.Control {
  public class MasterClock : MonoBehaviour { public event Action<int> BeatCrossed, BarCrossed; }
  public class KickReactivity : MonoBehaviour { public float KickLevel; }
  public class WorldGridScan : MonoBehaviour { public bool guideLinesOn = true; }
  public class CurvedWorldBridge : MonoBehaviour {
    public struct Preset { public AmazingAssets.CurvedWorld.BendType type; public float curvature, horizontal, vertical, offsetCurvature; public Vector3 axis; }
    public static Preset[] Presets = new Preset[8];
    public bool customBend, pivotFollowsCamera; public AmazingAssets.CurvedWorld.BendType customShape; public float customCurvature, customHorizontal, customVertical, amount, intensity; public Vector3 customAxis; public int preset;
    public void AddSharedBend(List<Material> m) { } public void RemoveSharedBend(List<Material> m) { }
  }

}
namespace PsychedelicLab.GeometryFX {
  public class WireParticleSwarm : MonoBehaviour {
    public CurvedGeometryChamber chamber; public GameObject particlePrefab; public Material particleMaterial;
    public int nodesU, nodesV, fractalLevels, travellersPerLine, maxParticles; public float nodeSize; public bool assembleOnPlay;
  }
}
namespace UnityEngine {
  public class Camera : Behaviour { public static Camera main; public float fieldOfView = 60, orthographicSize = 5, aspect = 1.777f; public bool orthographic; }
  [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int o) { } }
}
