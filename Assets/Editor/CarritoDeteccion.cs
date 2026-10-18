using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CarritoDeteccion
{
    static readonly string[] Escenas =
    {
        "Assets/Scenes/MinimarketXR_Full.unity",
        "Assets/Scenes/MinimarketXR_Night.unity",
    };

    const string Prefijo = "Pref_Trolley01_Sm";
    const string Zona = "ZonaCarrito";

    [MenuItem("Tools/Minimarket/Carritos/Instalar Deteccion")]
    public static void Ejecutar()
    {
        int errores = 0;
        Debug.Log("[Deteccion] iniciando");

        foreach (var path in Escenas)
        {
            try { ProcesarEscena(path); }
            catch (System.Exception e) { Debug.LogError("[Deteccion] EX " + path + ": " + e); errores++; }
        }

        Debug.Log(errores == 0 ? "[Deteccion] TODO LISTO" : "[Deteccion] ERRORES " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static void ProcesarEscena(string path)
    {
        if (!System.IO.File.Exists(path)) { Debug.LogError("[Deteccion] falta " + path); return; }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Debug.Log("[Deteccion] abierta " + path);

        int puestas = 0, yaEstaba = 0;

        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(Prefijo)) continue;

            bool existe = false;
            foreach (Transform h in t)
                if (h.name == Zona) { existe = true; break; }
            if (existe) { yaEstaba++; continue; }

            var bc = t.GetComponent<BoxCollider>();
            if (bc == null)
            {
                Debug.LogWarning("[Deteccion] sin BoxCollider: " + t.name);
                continue;
            }

            var zona = new GameObject(Zona);
            zona.transform.SetParent(t, false);

            // Interior de la cesta: parte superior del volumen del carrito.
            var c = bc.center;
            c.y += bc.size.y * 0.16f;
            zona.transform.localPosition = c;

            var box = zona.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(
                bc.size.x * 0.70f,
                bc.size.y * 0.45f,
                bc.size.z * 0.70f);

            zona.AddComponent<DeteccionCarrito>();

            puestas++;
            Debug.Log($"[Deteccion] zona puesta en '{t.name}' localPos={c.ToString("F3")} size={box.size.ToString("F3")}");
        }

        if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path))
        {
            Debug.LogError("[Deteccion] fallo SaveScene " + path);
            return;
        }

        Debug.Log($"[Deteccion] {path}: zonas={puestas} yaExistian={yaEstaba}");
    }
}
