using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Arregla el LECTOR DE BARRAS ("base") para que se agarre SIEMPRE por el mango (arriba)
/// y no por cualquier sitio.
///
/// QUE ESTA MAL
/// ------------
/// El lector ("base.fbx") es un FBX con escala 100x y la malla en unidades
/// diminutas (0.01), y con el eje Y deformado. El BoxCollider que se creo en su
/// momento quedo pegado al ORIGEN del objeto, que en este FBX no coincide con el
/// centro visual de la maquina: por eso el lector se agarraba "en el aire" y la
/// zona de lectura no estaba encima de la maquina.
///
/// QUE HACE ESTE
/// --------------
/// 1. Recalcula el collider a partir de los bounds REALES del renderer (en mundo), y
///    no de los vertices del mesh (que estan en unas unidades absurdas). Asi el
///    collider va exactamente sobre la maquina.
///
/// 2. Crea un "Punto de Agarre" en la parte de ARRIBA del lector (el mango), que es
///    donde se coge en la vida real. Ese punto es el UNICO collider agarrable, asi
///    que el rayo del mando solo pilla el mango.
///
/// 3. Mismos ajustes de agarre que los productos: el objeto viene a la mano, sin
///    retardo de fisica.
///
/// Uso: Tools/Minimarket/Agarrar/Arreglar Lector (mango siempre)
/// </summary>
public static class ArreglarLector
{
    const string Escena = "Assets/Scenes/MinimarketXR_Full.unity";
    const string NombrePunto = "Punto de Agarre";

    [MenuItem("Tools/Minimarket/Agarrar/Arreglar Lector (mango siempre)")]
    public static void Ejecutar()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Arreglo del lector ===");

        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include);
        int n = 0;

        foreach (var g in grabs)
        {
            string ruta = Ruta(g.transform);
            if (ruta != "base" && !ruta.EndsWith("/base")) continue;

            Arreglar(g, t);
            n++;
        }

        t.AppendLine("lectores corregidos: " + n);

        if (!EditorSceneManager.SaveScene(scene, Escena))
        {
            Debug.LogError("[Lector] No se pudo guardar la escena.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log(t.ToString());
        Escribir(t);

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Arreglar(XRGrabInteractable g, System.Text.StringBuilder t)
    {
        // ---- 1. Collider principal sobre los bounds reales del renderer (en mundo).
        var renderer = g.GetComponentInChildren<Renderer>(true);
        Bounds mundo = renderer != null ? renderer.bounds : new Bounds(g.transform.position, Vector3.one * 0.2f);

        var box = g.GetComponent<BoxCollider>();
        if (box == null) box = g.gameObject.AddComponent<BoxCollider>();

        // El collider es hijo del objeto, asi que hay que pasar de mundo a local.
        Vector3 tamLocal = DividirPorEscala(mundo.size, g.transform.lossyScale);
        Vector3 centroLocal = g.transform.InverseTransformPoint(mundo.center);

        box.size = tamLocal;
        box.center = centroLocal;
        box.isTrigger = false;
        t.AppendLine("  collider: center(local)=" + centroLocal + " size(local)=" + tamLocal +
                     " | mundo size=" + mundo.size);

        // ---- 2. Punto de agarre en el MANGO (parte de arriba de la maquina).
        // El mango es la zona superior del renderer, un poco hacia delante (Z negativo
        // es la parte de arriba segun la rotacion del objeto, rot=270 en X).
        Vector3 centroMango = CalcularMango(renderer, mundo, g.transform);

        var punto = CrearPunto(g.transform, g.transform.InverseTransformPoint(centroMango),
                                DividirPorEscala(new Vector3(0.09f, 0.09f, 0.09f), g.transform.lossyScale),
                                out BoxCollider col);

        g.colliders.Clear();
        g.colliders.Add(col);
        g.attachTransform = punto.transform;
        t.AppendLine("  punto de agarre (mango) en mundo=" + centroMango);

        // ---- 3. Ajustes de agarre.
        g.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Near;
        g.movementType = XRBaseInteractable.MovementType.Instantaneous;
        g.smoothPosition = false;
        g.smoothRotation = false;
        g.trackPosition = true;
        g.trackRotation = true;

        // El rigidbody: que no ruede solo, pero se pueda agarrar y mover.
        var rb = g.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    /// <summary>
    /// Calcula donde esta el mango: la parte de arriba de la maquina, inclinada hacia
    /// delante un poco (los lectores de barras se cogen por la parte alta y frontal).
    /// </summary>
    static Vector3 CalcularMango(Renderer renderer, Bounds mundo, Transform obj)
    {
        if (renderer == null) return mundo.center + Vector3.up * mundo.extents.y;

        // Arriba de la maquina, un 20% hacia abajo desde el tope (el mango no esta en
        // el extremo mas alto, sino en el tercio superior) y desplazado hacia el frente.
        float altura = mundo.extents.y * 0.62f;
        float frente = mundo.extents.z * 0.18f;

        Vector3 pos = mundo.center + Vector3.up * altura;

        // El frente del lector es hacia -Z en su espacio local (rotacion 270 en X).
        Vector3 dirFrenteLocal = Vector3.back;
        Vector3 dirFrente = obj.TransformDirection(dirFrenteLocal);
        dirFrente.y = 0f;
        if (dirFrente.sqrMagnitude < 0.0001f) dirFrente = obj.forward;
        dirFrente.Normalize();

        return pos + dirFrente * frente;
    }

    static Vector3 DividirPorEscala(Vector3 v, Vector3 escala)
    {
        return new Vector3(
            v.x / Mathf.Max(0.0001f, Mathf.Abs(escala.x)),
            v.y / Mathf.Max(0.0001f, Mathf.Abs(escala.y)),
            v.z / Mathf.Max(0.0001f, Mathf.Abs(escala.z)));
    }

    static GameObject CrearPunto(Transform padre, Vector3 localPos, Vector3 tamLocal, out BoxCollider col)
    {
        var previos = new List<GameObject>();
        foreach (Transform h in padre.GetComponentsInChildren<Transform>(true))
            if (h.name == NombrePunto && h.gameObject != null) previos.Add(h.gameObject);
        for (int i = 0; i < previos.Count; i++)
            if (previos[i] != null) Object.DestroyImmediate(previos[i]);

        var go = new GameObject(NombrePunto);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        col = go.AddComponent<BoxCollider>();
        col.size = tamLocal;
        col.center = Vector3.zero;
        col.isTrigger = false;
        return go;
    }

    static string Ruta(Transform tr)
    {
        var parts = new List<string>();
        var cur = tr;
        int guard = 0;
        while (cur != null && guard < 30) { parts.Insert(0, cur.name); cur = cur.parent; guard++; }
        return string.Join("/", parts);
    }

    static void Escribir(System.Text.StringBuilder t)
    {
        Directory.CreateDirectory(@"C:\Users\abner\AppData\Local\Temp\opencode");
        File.WriteAllText(@"C:\Users\abner\AppData\Local\Temp\opencode\lector_fix.txt", t.ToString());
    }
}
