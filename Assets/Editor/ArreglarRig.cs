using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.XR.CoreUtils;

public static class ArreglarRig
{
    static readonly string[] Escenas =
    {
        "Assets/Scenes/MinimarketXR_Full.unity",
        "Assets/Scenes/MinimarketXR_Night.unity",
    };

    [MenuItem("Tools/Minimarket/Arreglar Rig (camara en suelo)")]
    public static void Ejecutar()
    {
        int errores = 0;
        Debug.Log("[ArreglarRig] iniciando");

        foreach (var path in Escenas)
        {
            try { ProcesarEscena(path); }
            catch (System.Exception e) { Debug.LogError("[ArreglarRig] EX " + path + ": " + e); errores++; }
        }

        Debug.Log(errores == 0 ? "[ArreglarRig] TODO LISTO" : "[ArreglarRig] ERRORES " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static void ProcesarEscena(string path)
    {
        if (!System.IO.File.Exists(path)) { Debug.LogError("[ArreglarRig] falta " + path); return; }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        XROrigin rig = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            rig = root.GetComponentInChildren<XROrigin>(true);
            if (rig != null) break;
        }

        if (rig == null)
        {
            Debug.LogError("[ArreglarRig] no hay XROrigin en " + path);
            return;
        }

        var t = rig.transform;
        Debug.Log($"[ArreglarRig] {path}: antes pos={t.position.ToString("F3")} localY={t.localPosition.y:F3} modo={rig.RequestedTrackingOriginMode}");

        // 1) Suelo del rig en y=0 (mantener x/z y rotacion).
        if (Mathf.Abs(t.localPosition.y) > 0.0001f)
        {
            var lp = t.localPosition;
            lp.y = 0f;
            t.localPosition = lp;
            Debug.Log("[ArreglarRig]   localPosition.y -0.303 -> 0");
        }

        // 2) Tracking en modo Floor (siempre a altura real de suelo).
        if (rig.RequestedTrackingOriginMode != XROrigin.TrackingOriginMode.Floor)
        {
            rig.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            Debug.Log("[ArreglarRig]   modo tracking -> Floor");
        }

        // 3) Camera Offset a 0.
        var offset = t.Find("Camera Offset");
        if (offset != null && offset.localPosition != Vector3.zero)
        {
            Debug.Log("[ArreglarRig]   Camera Offset localY=" + offset.localPosition.y + " -> 0");
            offset.localPosition = Vector3.zero;
        }
        else if (offset == null)
        {
            Debug.LogWarning("[ArreglarRig]   no encontro 'Camera Offset'");
        }

        Physics.SyncTransforms();
        Debug.Log($"[ArreglarRig] despues pos={t.position.ToString("F3")} modo={rig.RequestedTrackingOriginMode}");

        if (!EditorSceneManager.SaveScene(scene, path))
        {
            Debug.LogError("[ArreglarRig] fallo SaveScene " + path);
            return;
        }
        Debug.Log("[ArreglarRig] guardada " + path);
    }
}
