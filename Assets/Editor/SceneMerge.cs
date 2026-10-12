using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneMerge
{
    const string MarketScene = "Assets/SuperMarket/Scenes/SuperMarket_Day_01.unity";
    const string XrScene = "Assets/Scenes/MinimarketXR.unity";
    const string OutputScene = "Assets/Scenes/MinimarketXR_Full.unity";

    static readonly string[] MoveRoots =
    {
        "[BuildingBlock] Camera Rig",
        "Global Volume"
    };

    static readonly string[] SkipRoots =
    {
        "Plane",
        "Directional Light",
        "Cube",
        "[BuildingBlock] Cube"
    };

    static readonly string[] RemoveInMarket =
    {
        "Main Camera"
    };

    // Detras del mostrador central: clientes vienen de +z (entrada), cajero en -z.
    static readonly Vector3 RigPosition = new Vector3(-3.55f, 0f, -4.4f);
    static readonly float RigYaw = 0f;

    [MenuItem("Tools/Minimarket/Merge Escenas")]
    public static void Merge()
    {
        Debug.Log("[SceneMerge] Iniciando fusion...");

        if (System.IO.File.Exists(OutputScene))
            AssetDatabase.DeleteAsset(OutputScene);

        var market = EditorSceneManager.OpenScene(MarketScene, OpenSceneMode.Single);
        var xr = EditorSceneManager.OpenScene(XrScene, OpenSceneMode.Additive);

        var marketNames = market.GetRootGameObjects().Select(g => g.name).ToArray();
        var xrNames = xr.GetRootGameObjects().Select(g => g.name).ToArray();
        Debug.Log("[SceneMerge] roots mercado: " + string.Join(", ", marketNames));
        Debug.Log("[SceneMerge] roots xr: " + string.Join(", ", xrNames));

        int moved = 0;
        foreach (var root in xr.GetRootGameObjects().ToArray())
        {
            string n = root.name;
            bool doMove = MoveRoots.Contains(n) ||
                          (n.Contains("Hand Tracking") || n.Contains("HandGrab") || n.Contains("Camera Rig"));
            if (!doMove)
            {
                Debug.Log("[SceneMerge] skip root: " + n + (SkipRoots.Contains(n) ? " (placeholder)" : ""));
                continue;
            }

            if (market.GetRootGameObjects().Any(g => g.name == n))
            {
                Debug.Log("[SceneMerge] ya existe en mercado, skip: " + n);
                continue;
            }

            SceneManager.MoveGameObjectToScene(root, market);
            moved++;
            Debug.Log("[SceneMerge] movido: " + n);
        }

        foreach (var name in RemoveInMarket)
        {
            foreach (var go in market.GetRootGameObjects().Where(g => g.name == name).ToArray())
            {
                Debug.Log("[SceneMerge] eliminando Main Camera duplicada");
                Object.DestroyImmediate(go);
            }
        }

        // Colocar el rig detras del mostrador central.
        var rig = market.GetRootGameObjects().FirstOrDefault(g => g.name.Contains("Camera Rig"));
        if (rig != null)
        {
            rig.transform.position = RigPosition;
            rig.transform.rotation = Quaternion.Euler(0f, RigYaw, 0f);
            Debug.Log("[SceneMerge] rig posicionado en " + RigPosition);
        }
        else
        {
            Debug.LogError("[SceneMerge] NO se encontro Camera Rig en la escena resultante");
        }

        var vol = market.GetRootGameObjects().FirstOrDefault(g => g.name == "Global Volume");
        Debug.Log("[SceneMerge] Global Volume presente: " + (vol != null));

        LogHierarchy(market);

        if (!EditorSceneManager.SaveScene(market, OutputScene))
        {
            Debug.LogError("[SceneMerge] Falló SaveScene");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        // Restaurar la escena original del mercado sin cambios.
        EditorSceneManager.OpenScene(MarketScene, OpenSceneMode.Single);

        Debug.Log("[SceneMerge] OK guardado en " + OutputScene + " movidos=" + moved);
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    static void LogHierarchy(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = root.transform;
            string kids = string.Join(", ", t.Cast<Transform>().Select(c => c.name));
            Debug.Log("[SceneMerge] ROOT: " + t.position + " | " + root.name + " -> [" + kids + "]");
        }
    }
}
