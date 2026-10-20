using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PuertaInstalar
{
    static readonly string[] Escenas =
    {
        "Assets/Scenes/MinimarketXR_Full.unity",
        "Assets/Scenes/MinimarketXR_Night.unity",
    };

    const string GlassPath = "Assets/SuperMarket/Materials/Glass_Mat_01.mat";
    const string Raiz = "PuertaAutomatica";
    const string Trigger = "DoorTrig_01";
    const string PuertaEstatica = "Pref_Market_EntDoor_Ani";

    const float AnchoPanel = 1.5f;
    const float AltoPanel = 2.2f;
    const float Grosor = 0.05f;

    [MenuItem("Tools/Minimarket/Puerta/Instalar Corrediza")]
    public static void Ejecutar()
    {
        int errores = 0;
        Debug.Log("[PuertaInst] iniciando");

        foreach (var path in Escenas)
        {
            try { ProcesarEscena(path); }
            catch (System.Exception e) { Debug.LogError("[PuertaInst] EX " + path + ": " + e); errores++; }
        }

        Debug.Log(errores == 0 ? "[PuertaInst] TODO LISTO" : "[PuertaInst] ERRORES " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static void ProcesarEscena(string path)
    {
        if (!System.IO.File.Exists(path)) { Debug.LogError("[PuertaInst] falta " + path); return; }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Debug.Log("[PuertaInst] abierta " + path);

        Transform trig = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var h = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in h)
                if (t.name == Trigger) { trig = t; break; }
            if (trig != null) break;
        }

        // Sondeo de la entrada (para validar colocacion).
        foreach (var root in scene.GetRootGameObjects())
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name == "Market_Ent")
                Debug.Log($"[PuertaInst]   Market_Ent bounds={r.bounds.center.ToString("F2")} size={r.bounds.size.ToString("F2")}");

        if (trig == null)
        {
            Debug.LogError("[PuertaInst] no encontro " + Trigger);
            return;
        }
        Debug.Log("[PuertaInst] trigger en " + trig.position.ToString("F3"));

        var estaticas = new System.Collections.Generic.List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.StartsWith(PuertaEstatica))
                    estaticas.Add(t);
        foreach (var t in estaticas)
        {
            if (t == null) continue;
            Debug.Log("[PuertaInst] quitando puerta estatica " + t.name);
            Object.DestroyImmediate(t.gameObject);
        }

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == Raiz)
                Object.DestroyImmediate(root.gameObject);

        var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
        if (glass == null) Debug.LogWarning("[PuertaInst] falta material glass: " + GlassPath);

        trig.position = new Vector3(trig.position.x, trig.position.y, 0f);
        var raiz = new GameObject(Raiz);
        raiz.transform.position = new Vector3(0f, 0f, 0f);

        float mitad = AnchoPanel * 0.5f;
        var izq = CrearPanel("PanelIzq", new Vector3(-mitad, AltoPanel * 0.5f, 0f), glass, raiz.transform);
        var der = CrearPanel("PanelDer", new Vector3(mitad, AltoPanel * 0.5f, 0f), glass, raiz.transform);

        var puerta = raiz.AddComponent<PuertaAutomatica>();
        puerta.panelIzq = izq;
        puerta.panelDer = der;
        puerta.radio = 2.6f;
        puerta.velocidad = 1.4f;
        puerta.apertura = AnchoPanel;

        if (!EditorSceneManager.SaveScene(scene, path))
        {
            Debug.LogError("[PuertaInst] fallo SaveScene " + path);
            return;
        }
        Debug.Log("[PuertaInst] instalada en " + path);
    }

    static Transform CrearPanel(string nombre, Vector3 localPos, Material glass, Transform padre)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(AnchoPanel, AltoPanel, Grosor);

        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        if (glass != null)
            go.GetComponent<MeshRenderer>().sharedMaterial = glass;

        return go.transform;
    }
}
