using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReemplazarRigXR
{
    const string ScenePath = "Assets/Scenes/MinimarketXR_Full.unity";
    const string XrOriginPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    static readonly Vector3 RigPosition = new Vector3(-3.55f, 0f, -4.4f);
    static readonly float RigYaw = 0f;

    [MenuItem("Tools/Minimarket/Reemplazar Rig XR (Meta -> XRI)")]
    public static void Reemplazar()
    {
        Debug.Log("[ReemplazarRigXR] Iniciando...");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[ReemplazarRigXR] No se encontro el prefab XR Origin en: " + XrOriginPrefabPath);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int eliminados = 0;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            string n = root.name;
            if (n.Contains("Camera Rig") || n.Contains("Hand Tracking") || n.Contains("HandGrabInstallation"))
            {
                Debug.Log("[ReemplazarRigXR] eliminando root: " + n);
                Object.DestroyImmediate(root);
                eliminados++;
            }
        }

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        rig.transform.position = RigPosition;
        rig.transform.rotation = Quaternion.Euler(0f, RigYaw, 0f);
        Debug.Log("[ReemplazarRigXR] XR Origin creado en " + RigPosition);

        if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath))
        {
            Debug.LogError("[ReemplazarRigXR] Falló SaveScene");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[ReemplazarRigXR] OK. Eliminados=" + eliminados + ", XR Origin puesto en " + RigPosition);
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }
}
