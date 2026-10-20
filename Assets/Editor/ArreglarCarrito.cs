using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Arregla el CARRITO (Pref_Trolley01_Sm) para que se vea y se agarre como un carrito
/// de verdad, no como una caja.
///
/// QUE ESTA MAL
/// ------------
/// El prefab del carrito tiene UN SOLO BoxCollider con el tamano del BOUNDING BOX de
/// toda la malla (0.65 x 1.09 x 1.13). Es decir, un cubo invisible que envuelve el
/// carrito entero. Por eso:
///
///   - el carrito "se ve como una caja" (el collider es una caja, y ademas el mesh
///     es una sola pieza, asi que no hay forma de distinguishing partes);
///   - el rayo del mando puede pegarle a una esquina vacia del cubo y agarrarlo "por
///     el aire";
///   - al agarrarlo y soltarlo, se queda con la rotacion de la caja.
///
/// QUE HACE ESTE
/// --------------
/// 1. Sustituye el BoxCollider gigante por un MeshCollider CONVEX que sigue la forma
///    real del carrito. Se puede editar desde el Inspector (convex = false no vale,
///    porque el carrito tiene Rigidbody).
///
/// 2. Ancla el carrito al SUELO: con un Rigidbody kinematic se queda quieto en el
///    sitio y no se va rodando ni se cae por los estantes, pero se puede agarrar y
///    mover. Es lo que hace un carrito de verdad en una tienda.
///
/// 3. Crea un "Punto de Agarre" en el ASA (barra superior trasera) para que se
///    agarre por donde debe.
///
/// 4. Ajusta el rigidbody y los ajustes de agarre (mismo criterio que los productos:
///    farAttach = Near, movement = Instantaneous, sin suavizado).
///
/// Uso: Tools/Minimarket/Agarrar/Arreglar Carrito (malla y anclaje al suelo)
/// </summary>
public static class ArreglarCarrito
{
    const string PrefabCarrito = "Assets/SuperMarket/Prefabs/Pref_Trolley01_Sm.prefab";
    const string Escena = "Assets/Scenes/MinimarketXR_Full.unity";
    const string NombrePunto = "Punto de Agarre";

    [MenuItem("Tools/Minimarket/Agarrar/Arreglar Carrito (malla y anclaje al suelo)")]
    public static void Ejecutar()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Arreglo del carrito ===");

        // ------------------------------------------------ 1. El PREFAB (collider)
        ArreglarPrefab(t);

        // ------------------------------------------------ 2. La ESCENA (rigidbody, punto)
        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include);
        int n = 0;

        foreach (var g in grabs)
        {
            if (!Ruta(g.transform).Contains("Trolley")) continue;

            ArreglarEscena(g, t);
            n++;
        }

        t.AppendLine("carritos en la escena corregidos: " + n);

        if (!EditorSceneManager.SaveScene(scene, Escena))
        {
            Debug.LogError("[Carrito] No se pudo guardar la escena.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log(t.ToString());
        Escribir(t);

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------ prefab
    static void ArreglarPrefab(System.Text.StringBuilder t)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCarrito);
        if (prefab == null)
        {
            t.AppendLine("prefab no encontrado: " + PrefabCarrito);
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabCarrito);
        try
        {
            // El collider viejo (caja gigante) se elimina.
            var viejos = root.GetComponents<BoxCollider>();
            foreach (var c in viejos) Object.DestroyImmediate(c);
            if (viejos.Length > 0) t.AppendLine("  BOXCOLLIDER eliminados: " + viejos.Length);

            // MeshCollider convexo que sigue la forma real del carrito.
            var mf = root.GetComponentInChildren<MeshFilter>(true);
            if (mf != null && mf.sharedMesh != null)
            {
                var mc = root.GetComponent<MeshCollider>();
                if (mc == null) mc = root.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;      // obligatorio: el carrito tiene Rigidbody
                mc.isTrigger = false;
                mc.cookingOptions = MeshColliderCookingOptions.None;
                t.AppendLine("  MeshCollider convexo creado sobre '" + mf.sharedMesh.name + "'");
            }
            else
            {
                t.AppendLine("  sin malla: se deja el collider como estaba");
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabCarrito);
            t.AppendLine("  prefab guardado: " + PrefabCarrito);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------ escena
    static void ArreglarEscena(XRGrabInteractable g, System.Text.StringBuilder t)
    {
        // Anclaje al suelo: kinematic, sin gravedad, con constraints.
        var rb = g.GetComponent<Rigidbody>();
        if (rb == null) rb = g.gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;         // no rueda, no se cae, se queda en su sitio
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.mass = 12f;

        // El punto de agarre va en el asa (barra superior trasera).
        var mf = g.GetComponentInChildren<MeshFilter>(true);
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 asa = CalcularAsa(mf.sharedMesh.vertices);

            // A espacio local del objeto.
            Vector3 local = mf.transform.InverseTransformPoint(mf.transform.TransformPoint(asa));

            var punto = CrearPunto(g.transform, local, new Vector3(0.14f, 0.1f, 0.14f), out BoxCollider col);
            g.colliders.Clear();
            g.colliders.Add(col);
            g.attachTransform = punto.transform;
            t.AppendLine("  '" + g.name + "': punto de agarre en el asa local=" + local);
        }

        // Mismos ajustes de agarre que los productos.
        g.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Near;
        g.movementType = XRBaseInteractable.MovementType.Instantaneous;
        g.smoothPosition = false;
        g.smoothRotation = false;
        g.trackPosition = true;
        g.trackRotation = true;
        g.throwOnDetach = false;
    }

    /// <summary>
    /// Localiza el asa del carrito: los vertices mas altos en el extremo trasero (Z neg).
    /// </summary>
    static Vector3 CalcularAsa(Vector3[] verts)
    {
        float maxY = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in verts)
        {
            if (v.y > maxY) maxY = v.y;
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        var altos = new List<Vector3>();
        foreach (var v in verts) if (v.y >= maxY - 0.01f) altos.Add(v);
        if (altos.Count == 0) foreach (var v in verts) if (v.y >= maxY - 0.05f) altos.Add(v);
        if (altos.Count == 0) return Vector3.zero;

        float corte = minZ + (maxZ - minZ) * 0.25f;
        var asa = new List<Vector3>();
        foreach (var v in altos) if (v.z <= corte) asa.Add(v);
        if (asa.Count == 0) asa = altos;

        Vector3 suma = Vector3.zero;
        foreach (var v in asa) suma += v;
        return suma / asa.Count;
    }

    static GameObject CrearPunto(Transform padre, Vector3 localPos, Vector3 tam, out BoxCollider col)
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
        col.size = tam;
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
        File.WriteAllText(@"C:\Users\abner\AppData\Local\Temp\opencode\carrito_fix.txt", t.ToString());
    }
}
