using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class CarritosMovibles
{
    static readonly string[] Escenas =
    {
        "Assets/Scenes/MinimarketXR_Full.unity",
        "Assets/Scenes/MinimarketXR_Night.unity",
    };

    const string Prefijo = "Pref_Trolley01_Sm";

    // Congela Y (no cae ni se levanta) y volteo en X/Z (no se voltea).
    const RigidbodyConstraints Restricciones =
        RigidbodyConstraints.FreezePositionY |
        RigidbodyConstraints.FreezeRotationX |
        RigidbodyConstraints.FreezeRotationZ;

    [MenuItem("Tools/Minimarket/Carritos/Hacer Movibles")]
    public static void Ejecutar()
    {
        Debug.Log("[Carritos] Iniciando...");
        int errores = 0;

        foreach (var path in Escenas)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("[Carritos] falta escena: " + path);
                errores++;
                continue;
            }

            try { ProcesarEscena(path); }
            catch (System.Exception e)
            {
                Debug.LogError("[Carritos] EX en " + path + ": " + e);
                errores++;
            }
        }

        Debug.Log(errores == 0 ? "[Carritos] TODO LISTO" : "[Carritos] CON ERRORES: " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static void ProcesarEscena(string path)
    {
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Debug.Log("[Carritos] abierta: " + path);

        var colliders = new List<Collider>();
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (c.gameObject.name.StartsWith(Prefijo))
                    colliders.Add(c);

        Debug.Log("[Carritos]   encontrados: " + colliders.Count);

        // Marcar como solapados los que chocan con otro carro.
        var solapados = new HashSet<Collider>();
        for (int a = 0; a < colliders.Count; a++)
            for (int b = a + 1; b < colliders.Count; b++)
                if (colliders[a].bounds.Intersects(colliders[b].bounds))
                {
                    solapados.Add(colliders[a]);
                    solapados.Add(colliders[b]);
                }

        int movidos = 0, pilas = 0, yaEstaba = 0;
        foreach (var c in colliders)
        {
            var go = c.gameObject;
            var path2 = Ruta(go);

            if (solapados.Contains(c))
            {
                pilas++;
                Debug.Log("[Carritos]   pila (estatico): " + path2);
                continue;
            }

            if (go.GetComponent<Rigidbody>() != null)
            {
                yaEstaba++;
                Debug.Log("[Carritos]   ya movible: " + path2);
                continue;
            }

            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 12f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.constraints = Restricciones;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.smoothPosition = true;
            grab.smoothRotation = true;
            grab.useDynamicAttach = false;

            movidos++;
            Debug.Log("[Carritos]   MOVIBLE: " + path2 + " (bounds " + c.bounds.size + ")");
        }

        if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path))
        {
            Debug.LogError("[Carritos] fallo SaveScene: " + path);
            return;
        }

        Debug.Log($"[Carritos] {path}: movibles={movidos} pila={pilas} yaEstaban={yaEstaba}");
    }

    static string Ruta(GameObject go)
    {
        string s = go.name;
        var t = go.transform;
        while (t.parent != null)
        {
            t = t.parent;
            s = t.name + "/" + s;
        }
        return s;
    }
}
