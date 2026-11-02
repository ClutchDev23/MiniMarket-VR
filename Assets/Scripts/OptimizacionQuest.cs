using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// OPTIMIZACION PARA QUEST (la tienda tiene ~1.6 M triangulos).
/// En el Quest (Android) y tambien en el editor si PlayerPrefs "optiPC" = 1:
///   1. Culling por distancia: los objetos chicos (productos, cajitas, decoracion)
///      se dejan de dibujar lejos del jugador; los muebles grandes, un poco mas lejos.
///      Se revisa por tandas (unos cientos por frame), asi no cuesta CPU.
///   2. Sin sombras en todo lo chico (las sombras duplican los triangulos dibujados).
///   3. Sombras solo cerca (8 m), sin sombras suaves, 72 Hz fijos.
///   4. Clientes: huesos 2 por vertice y no se animan si no se ven.
///   5. Foveated rendering alto (el Quest dibuja menos pixeles en los bordes del lente).
/// </summary>
[DefaultExecutionOrder(500)]
public class OptimizacionQuest : MonoBehaviour
{
    public float distanciaChicos = 7f;    // productos y objetos de menos de ~40 cm
    public float distanciaMedianos = 14f; // cajas, carteles, objetos de menos de ~1.5 m
    public int porFrame = 400;

    class Item { public Renderer r; public Vector3 c; public float d2; public bool puse = true; }
    readonly HashSet<Renderer> apagadosPorMi = new HashSet<Renderer>();
    readonly List<Item> items = new List<Item>();
    int cursor;
    Transform cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (e, m) => Asegurar(e);
        Asegurar(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void Asegurar(UnityEngine.SceneManagement.Scene e)
    {
        bool activo = Application.platform == RuntimePlatform.Android || PlayerPrefs.GetInt("optiPC", 0) == 1;
        if (!activo || !e.IsValid() || !e.name.StartsWith("MinimarketXR") || FindAnyObjectByType<OptimizacionQuest>() != null) return;
        var go = new GameObject("Optimizacion Quest");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, e);
        go.AddComponent<OptimizacionQuest>();
    }

    void Start()
    {
        Application.targetFrameRate = 72;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadowDistance = 8f;
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.lodBias = 0.6f;
        QualitySettings.skinWeights = SkinWeights.TwoBones;
        Foveado();
        Invoke(nameof(Recolectar), 1.5f);       // cuando ya se construyeron mostradores, productos, etc.
        InvokeRepeating(nameof(Recolectar), 20f, 20f); // por si aparecen cosas nuevas
    }

    static void Foveado()
    {
        var disp = new List<UnityEngine.XR.XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(disp);
        foreach (var d in disp)
        {
            d.foveatedRenderingLevel = 1f;   // alto
            d.foveatedRenderingFlags = UnityEngine.XR.XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed;
        }
    }

    void Recolectar()
    {
        items.Clear();
        int chicos = 0, medianos = 0;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled && !apagadosPorMi.Contains(r)) continue;   // lo escondio el juego: no tocar
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;
            if (r.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) continue;   // manos, mandos
            if (r is SkinnedMeshRenderer smr)
            {
                smr.updateWhenOffscreen = false;
                smr.quality = SkinQuality.Bone2;
                continue;
            }
            var b = r.bounds;
            float tam = b.size.magnitude;
            float dist;
            if (tam < 0.6f) { dist = distanciaChicos; chicos++; r.shadowCastingMode = ShadowCastingMode.Off; }
            else if (tam < 2.2f) { dist = distanciaMedianos; medianos++; }
            else continue;   // paredes, piso, muebles grandes: siempre visibles
            items.Add(new Item { r = r, c = b.center, d2 = dist * dist, puse = r.enabled });
        }
        Debug.Log("[Optimizacion] culling por distancia: " + chicos + " chicos (" + distanciaChicos + " m), " + medianos + " medianos (" + distanciaMedianos + " m).");
    }

    void Update()
    {
        if (cam == null) { var c = Camera.main; if (c == null) return; cam = c.transform; }
        if (items.Count == 0) return;
        Vector3 p = cam.position;
        int n = Mathf.Min(porFrame, items.Count);
        for (int i = 0; i < n; i++)
        {
            if (cursor >= items.Count) cursor = 0;
            var it = items[cursor++];
            if (it.r == null) continue;
            if (it.r.enabled != it.puse) { apagadosPorMi.Remove(it.r); it.d2 = float.MaxValue; it.puse = it.r.enabled; continue; } // otro script lo cambio
            // Los objetos que se mueven (productos agarrados, en la faja) usan su posicion actual.
            Vector3 c = it.r.bounds.center;
            if (it.d2 == float.MaxValue) continue;
            bool ver = (c - p).sqrMagnitude < it.d2;
            if (ver == it.puse) continue;
            it.r.enabled = ver; it.puse = ver;
            if (ver) apagadosPorMi.Remove(it.r); else apagadosPorMi.Add(it.r);
        }
    }
}
