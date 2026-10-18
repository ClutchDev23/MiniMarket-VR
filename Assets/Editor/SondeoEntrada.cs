using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SondeoEntrada
{
    [MenuItem("Tools/Minimarket/Sondeo Entrada")]
    public static void Ejecutar()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MinimarketXR_Full.unity", OpenSceneMode.Single);

        Transform super = null;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "SuperMarket") super = root.transform;

        if (super == null)
        {
            Debug.LogError("[Entrada] no hay SuperMarket");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[Entrada] hijos de SuperMarket: " + super.childCount);
        foreach (Transform h in super)
        {
            var cols = h.GetComponentsInChildren<Collider>(true).Length;
            var mrs = h.GetComponentsInChildren<MeshRenderer>(true).Length;
            Debug.Log($"[Entrada]  '{h.name}' pos={h.position.ToString("F3")} hijos={h.childCount} cols={cols} mrs={mrs} prefab={PrefabUtility.IsPartOfPrefabInstance(h)}");
        }

        // Objetos cerca del trigger de la puerta (0, 0.88, 1.79).
        var centro = new Vector3(0f, 0.9f, 1.79f);
        Debug.Log("[Entrada] objetos cerca de la puerta (radio 2.5m):");
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Vector3.Distance(t.position, centro) > 2.5f) continue;
            if (t.parent != null && Vector3.Distance(t.parent.position, centro) <= 2.5f &&
                t.GetComponentsInChildren<MeshRenderer>(true).Length == 0) continue;
            Debug.Log($"[Entrada]  '{Ruta(t)}' pos={t.position.ToString("F3")} scale={t.localScale.ToString("F3")} mrs={t.GetComponentsInChildren<MeshRenderer>(true).Length}");
        }

        Debug.Log("[Entrada] FIN");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static string Ruta(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
