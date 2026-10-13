using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HornearLighting
{
    const string ScenePath = "Assets/Scenes/MinimarketXR_Full.unity";

    [MenuItem("Tools/Minimarket/Marcar Statics y Hornear")]
    public static void Ejecutar()
    {
        Debug.Log("[HornearLighting] Iniciando...");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int marcados = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                var nombre = go.name;

                // No marcar objetos que no deben ser static
                if (nombre.Contains("Audio_") ||
                    nombre.Contains("DoorTrig") ||
                    nombre.Contains("Global Volume") ||
                    nombre.Contains("Reflection Probe") ||
                    nombre.Contains("Lighting") ||
                    nombre.Contains("XR Origin") ||
                    nombre.Contains("XR Device") ||
                    nombre.Contains("Directional Light") ||
                    nombre.Contains("Area Light") ||
                    nombre.Contains("Camera") ||
                    nombre.Contains("Volume"))
                    continue;

                // No marcar luces, audio, ni volúmenes
                if (go.GetComponent<Light>() != null ||
                    go.GetComponent<AudioSource>() != null ||
                    go.GetComponent<Camera>() != null)
                    continue;

                // Marcar como Static + Contribute GI (sin filtrar por MeshRenderer,
                // porque los meshes estan dentro de prefab instances)
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                bool yaStatic = (flags & StaticEditorFlags.ContributeGI) != 0;

                GameObjectUtility.SetStaticEditorFlags(go,
                    flags |
                    StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.OccluderStatic);

                if (!yaStatic)
                {
                    marcados++;
                    Debug.Log("[HornearLighting] static: " + nombre);
                }
            }
        }

        Debug.Log("[HornearLighting] " + marcados + " objetos marcados como Static");

        // Guardar la escena con los flags nuevos
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath))
        {
            Debug.LogError("[HornearLighting] Falló SaveScene");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        // Hornear
        Debug.Log("[HornearLighting] Iniciando hornada...");
        Lightmapping.Clear();

        bool ok = Lightmapping.Bake();

        if (ok)
        {
            Debug.Log("[HornearLighting] OK: hornada completada exitosamente");
        }
        else
        {
            Debug.LogError("[HornearLighting] Falló la hornada");
        }

        // Guardar de nuevo con los lightmaps
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);

        Debug.Log("[HornearLighting] Fin. Escena guardada.");

        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }
}
