using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HerramientasEscena
{
    const string ScenePath = "Assets/Scenes/MinimarketXR_Full.unity";
    const string SimuladorPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.4.1/XR Device Simulator/XR Device Simulator.prefab";

    static readonly string[] Estantes =
    {
        "Assets/SuperMarket/Prefabs/Pref_StandingRack01_Sm.prefab",
        "Assets/SuperMarket/Prefabs/Pref_StandingRack02_Sm.prefab",
        "Assets/SuperMarket/Prefabs/Pref_WallRack01_Sm.prefab",
        "Assets/SuperMarket/Prefabs/Pref_WallRack02_Sm.prefab",
        "Assets/SuperMarket/Prefabs/Pref_WallRack03_Sm.prefab",
        "Assets/SuperMarket/Prefabs/Pref_CrateStand_Load_Sm.prefab",
        "Assets/SuperMarket/Prefabs/Pref_CrateStand01_Sm.prefab",
        "Assets/SuperMarket/Prefabs/Pref_CashCounter_Sm.prefab",
        "Assets/SuperMarket/Prefabs/Pref_Freezer01_Sm.prefab",
    };

    [MenuItem("Tools/Minimarket/Herramientas Escena (simulador + estantes)")]
    public static void Todo()
    {
        Debug.Log("[HERR] Iniciando...");
        int errores = 0;

        try { RestaurarSimulador(); }
        catch (System.Exception e) { Debug.LogError("[HERR] simulador EX: " + e); errores++; }

        try { ArreglarEstantes(); }
        catch (System.Exception e) { Debug.LogError("[HERR] estantes EX: " + e); errores++; }

        Debug.Log(errores == 0 ? "[HERR] TODO LISTO" : "[HERR] TERMINO CON ERRORES: " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static void RestaurarSimulador()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "XR Device Simulator")
            {
                Debug.Log("[HERR] simulador ya existe, nada que hacer");
                return;
            }
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimuladorPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[HERR] prefab simulador no encontrado: " + SimuladorPrefabPath);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Debug.Log("[HERR] simulador restaurado como root: " + go.name);

        if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath))
        {
            Debug.LogError("[HERR] fallo SaveScene del simulador");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    static void ArreglarEstantes()
    {
        foreach (var path in Estantes)
        {
            if (!File.Exists(path))
            {
                Debug.LogError("[HERR] falta prefab: " + path);
                continue;
            }

            var root = PrefabUtility.LoadPrefabContents(path);

            int boxesAntes = root.GetComponentsInChildren<BoxCollider>(true).Length;
            int meshAntes = root.GetComponentsInChildren<MeshCollider>(true).Length;
            int boxQuitados = 0;
            int boxConservados = 0;

            foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                bool propia = box.GetComponent<MeshFilter>() != null;
                bool enHijos = box.GetComponentsInChildren<MeshFilter>(true).Length > 0;
                if (propia || enHijos)
                {
                    Object.DestroyImmediate(box);
                    boxQuitados++;
                }
                else
                {
                    boxConservados++;
                    Debug.Log("[HERR]   box conservado (sin malla): " + HierarchyPath(box.transform));
                }
            }

            int meshAgregados = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                    continue;
                if (mf.GetComponent<Collider>() != null)
                    continue;

                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                mc.isTrigger = false;
                meshAgregados++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            int boxesDespues = CountInAsset(path, "BoxCollider:");
            int meshDespues = CountInAsset(path, "MeshCollider:");
            Debug.Log($"[HERR] {Path.GetFileName(path)}: box {boxesAntes}->{boxesDespues} (-{boxQuitados}, conservados {boxConservados}), meshcolliders {meshAntes}->{meshDespues}");
        }

        AssetDatabase.SaveAssets();
    }

    static int CountInAsset(string path, string marca)
    {
        int n = 0;
        foreach (var l in File.ReadAllLines(path))
            if (l.TrimStart().StartsWith(marca))
                n++;
        return n;
    }

    static string HierarchyPath(Transform t)
    {
        string s = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            s = t.name + "/" + s;
        }
        return s;
    }
}
