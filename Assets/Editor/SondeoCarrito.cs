using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SondeoCarrito
{
    [MenuItem("Tools/Minimarket/Sondeo Carrito")]
    public static void Ejecutar()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MinimarketXR_Full.unity", OpenSceneMode.Single);

        int encontrados = 0;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Pref_Trolley") || t.name == "Trolley")
                {
                    if (t.parent != null && (t.parent.name.StartsWith("Pref_Trolley") || t.parent.name == "Trolley")) continue;
                    encontrados++;
                    Dump(t, 0);
                }

        Debug.Log("[CarritoSondeo] carritos=" + encontrados);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Dump(Transform t, int nivel)
    {
        var cols = t.GetComponents<Collider>();
        var mrs = t.GetComponents<MeshRenderer>();
        var mf = t.GetComponent<MeshFilter>();
        string colInfo = "";
        foreach (var c in cols)
            colInfo += $" [box size={c.bounds.size.ToString("F2")} center={c.bounds.center.ToString("F2")} trig={c.isTrigger}]";

        string meshInfo = "";
        if (mf != null && mf.sharedMesh != null)
            meshInfo = $" mesh={mf.sharedMesh.bounds.size.ToString("F2")}";

        Debug.Log($"[CarritoSondeo] {new string(' ', nivel * 2)}'{t.name}' pos={t.position.ToString("F2")} scale={t.localScale.ToString("F2")} mrs={mrs.Length}{meshInfo}{colInfo}");
        foreach (Transform h in t) Dump(h, nivel + 1);
    }
}
