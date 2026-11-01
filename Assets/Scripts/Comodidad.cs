using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// COMODIDAD (para que no maree).
///   1. VIÑETA: al caminar o girar con el stick, los bordes de la vista se oscurecen
///      (el cerebro ve menos movimiento "falso"). Quieto, desaparece.
///   2. GIRO POR SALTOS (snap turn) de 45° con el stick derecho, en vez de girar suave.
///   3. TELETRANSPORTE: stick derecho hacia adelante, apuntas al piso, sueltas y apareces ahi.
/// Se instala solo en las escenas del minimarket. Ajustes en PlayerPrefs:
/// "vineta" (1), "giroSaltos" (1), "teleport" (1).
/// </summary>
public class Comodidad : MonoBehaviour
{
    Transform cam, origen;
    Vector3 posAnterior; float yawAnterior;
    Material mat; float alfa;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (e, m) => Asegurar(e);
        Asegurar(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void Asegurar(UnityEngine.SceneManagement.Scene e)
    {
        if (!e.IsValid() || !e.name.StartsWith("MinimarketXR") || FindAnyObjectByType<Comodidad>() != null) return;
        var go = new GameObject("Comodidad (anti mareo)");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, e);
        go.AddComponent<Comodidad>();
    }

    void Start()
    {
        var o = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        origen = o != null ? o.transform : null;
        cam = Camera.main != null ? Camera.main.transform : null;
        if (PlayerPrefs.GetInt("vineta", 1) == 1) CrearVineta();
        if (PlayerPrefs.GetInt("giroSaltos", 1) == 1) GiroPorSaltos();
        if (PlayerPrefs.GetInt("teleport", 1) == 1) Teleport();
        if (origen != null) { posAnterior = origen.position; yawAnterior = origen.eulerAngles.y; }
    }

    // ------------------------------------------------------------------ viñeta
    void CrearVineta()
    {
        if (cam == null) return;
        int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            t.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, (r - 0.42f) / 0.38f)));
        }
        t.Apply();
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = "Vineta";
        q.transform.SetParent(cam, false);
        q.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        q.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
        mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = t, renderQueue = 4000 };
        mat.color = new Color(1f, 1f, 1f, 0f);
        var mr = q.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void LateUpdate()
    {
        if (mat == null || origen == null) return;
        // Movimiento "artificial": el del XR Origin (stick), no el de tu cabeza.
        float v = Time.deltaTime > 0f ? Vector3.Distance(origen.position, posAnterior) / Time.deltaTime : 0f;
        float w = Time.deltaTime > 0f ? Mathf.Abs(Mathf.DeltaAngle(yawAnterior, origen.eulerAngles.y)) / Time.deltaTime : 0f;
        posAnterior = origen.position; yawAnterior = origen.eulerAngles.y;
        float quiero = Mathf.Clamp01(Mathf.Max(v / 1.2f, w / 90f)) * 0.85f;
        alfa = Mathf.MoveTowards(alfa, quiero, Time.deltaTime * (quiero > alfa ? 5f : 2f));
        mat.color = new Color(1f, 1f, 1f, alfa);
    }

    // ------------------------------------------------------------------ giro
    void GiroPorSaltos()
    {
        if (origen == null) return;
        bool haySaltos = false;
        foreach (var b in origen.GetComponentsInChildren<MonoBehaviour>(true))
            if (b.GetType().Name == "SnapTurnProvider") { haySaltos = true; b.gameObject.SetActive(true); b.enabled = true; }
        if (!haySaltos) { Debug.Log("[Comodidad] El rig no tiene giro por saltos: se deja el giro suave."); return; }
        foreach (var b in origen.GetComponentsInChildren<MonoBehaviour>(true))
            if (b.GetType().Name == "ContinuousTurnProvider") b.enabled = false;
        Debug.Log("[Comodidad] Giro por saltos activado.");
    }

    // ------------------------------------------------------------------ teleport
    void Teleport()
    {
        if (origen == null) return;
        int n = 0;
        foreach (var t in origen.GetComponentsInChildren<Transform>(true))
            if (t.name == "Teleport Interactor") { t.gameObject.SetActive(true); n++; }
        var tipo = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea, Unity.XR.Interaction.Toolkit");
        int pisos = 0;
        if (tipo != null)
            foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c.isTrigger || !c.name.ToLowerInvariant().Contains("floor")) continue;
                if (c.GetComponent(tipo) == null) { c.gameObject.AddComponent(tipo); pisos++; }
            }
        Debug.Log("[Comodidad] Teleport: " + n + " interactores y " + pisos + " pisos.");
    }
}
