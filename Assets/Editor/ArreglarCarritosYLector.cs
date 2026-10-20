using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Arregla el agarre de los CARRITOS y del LECTOR DE BARRAS ("base") en la escena
/// MinimarketXR_Full.
///
/// QUE ESTA MAL
/// -------------
/// 1. Los carritos (Pref_Trolley01_Sm) tienen un unico BoxCollider del TAMANO DEL
///    BOUNDING BOX de toda la malla (0.65 x 1.09 x 1.13). Eso es un cubo vacio que
///    envuelve el carrito: el rayo del mando puede pegarle a una esquina invisible
///    (encima de la cesta o al lado) y el carrito se agarra "por el aire".
///
/// 2. El origen del objeto esta en el SUELO (y=0) mientras que la malla esta
///    centrada en y=0.55. Al agarrarlo, el punto de agarre quedaba a 55 cm del centro
///    visual del carrito: se colgaba de las ruedas en vez de del asa.
///
/// 3. El lector de barras ("base") tiene un BoxCollider de 1x1x1 CENTIMETROS, y
///    ademas esta en el origen del objeto, que esta 7 cm por debajo del centro visual
///    del lector. Por eso "no se agarra de donde quiero": el collider esta suelto,
///    flotando lejos de la maquina.
///
/// QUE HACE ESTO
/// -------------
/// Para cada carrito y para el lector:
///   - Crea un hijo "Punto de Agarre" con un collider pequeno que representa la zona
///     por la que se agarra (el asa del carrito / el cuerpo del lector).
///   - Ese collider es el UNICO que se puede agarrar (XRGrabInteractable.colliders),
///     de forma que el rayo solo pilla la parte de verdad, no el cubo invisible.
///   - El attach transform apunta a ese punto, asi el objeto se sostiene por ahi.
///   - El collider raro de antes se recoloca para que no quede flotando lejos del mesh.
///
/// Es una herramienta de editor: se ejecuta una vez desde el menu y guarda la escena.
/// </summary>
public static class ArreglarCarritosYLector
{
    const string Escena = "Assets/Scenes/MinimarketXR_Full.unity";
    const string NombrePunto = "Punto de Agarre";

    [MenuItem("Tools/Minimarket/Agarrar/Arreglar Carritos y Lector de Barras")]
    public static void Ejecutar()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Arreglo de carritos y lector ===");

        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include);

        int carritos = 0, lectores = 0;

        foreach (var g in grabs)
        {
            string path = Ruta(g.transform);

            bool esCarrito = path.Contains("Trolley");
            bool esLector = path == "base" || path.EndsWith("/base");

            if (!esCarrito && !esLector) continue;

            t.AppendLine("--- " + path);

            if (esCarrito) ArreglarCarrito(g, t); else ArreglarLector(g, t);

            if (esCarrito) carritos++; else lectores++;
        }

        t.AppendLine("carritos arreglados: " + carritos);
        t.AppendLine("lectores arreglados: " + lectores);

        if (!EditorSceneManager.SaveScene(scene, Escena))
        {
            Debug.LogError("[Carritos] No se pudo guardar la escena.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log(t.ToString());
        Directory.CreateDirectory("Temp/opencode");
        File.WriteAllText("Temp/opencode/carritos_fix.txt", t.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------ carrito
    static void ArreglarCarrito(XRGrabInteractable g, System.Text.StringBuilder t)
    {
        var mf = g.GetComponentInChildren<MeshFilter>(true);
        if (mf == null || mf.sharedMesh == null)
        {
            t.AppendLine("    sin mesh: se deja como esta");
            return;
        }

        var mesh = mf.sharedMesh;
        Vector3 asa = CalcularAsaCarrito(mesh.vertices, out string como);

        t.AppendLine("    mesh=" + mesh.name + " verts=" + mesh.vertexCount);
        t.AppendLine("    asa calculada (" + como + ") local=" + asa);

        // El punto de agarre va en el espacio local del OBJETO (no de la malla).
        Transform mallaTf = mf.transform;
        Vector3 puntoLocalEnObjeto = mallaTf.InverseTransformPoint(mallaTf.TransformPoint(asa));

        // Collider pequeno y facil de apuntar, centrado en la barra del asa.
        Vector3 tam = new Vector3(0.12f, 0.08f, 0.12f);

        var punto = CrearPuntoDeAgarre(g.transform, puntoLocalEnObjeto, tam, out BoxCollider col);

        // Solo el asa es agarrable: el cubo grande queda solo para la fisica.
        g.colliders.Clear();
        g.colliders.Add(col);

        g.attachTransform = punto.transform;
        g.useDynamicAttach = false;
        g.trackPosition = true;
        g.trackRotation = true;
        g.smoothPosition = false;
        g.smoothRotation = false;
        g.matchAttachPosition = true;
        g.matchAttachRotation = true;

        t.AppendLine("    punto de agarre: '" + punto.name + "' collider " + col.size + " en local " + puntoLocalEnObjeto);
        t.AppendLine("    colliders agarrables: " + g.colliders.Count);
    }

    /// <summary>
    /// Localiza el asa del carrito: es la barra superior trasera.
    ///
    /// En la malla Trolley_01 la barra del asa son los vertices mas altos
    /// (y ~1.09) y estan en el extremo de Z mas negativo (~-0.65), en el centro
    /// de X. Los demas vertices altos son el borde de la cesta.
    /// </summary>
    static Vector3 CalcularAsaCarrito(Vector3[] verts, out string como)
    {
        como = "por defecto";
        if (verts == null || verts.Length == 0) return Vector3.zero;

        float maxY = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in verts)
        {
            if (v.y > maxY) maxY = v.y;
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        // Los vertices mas altos (la barra del asa) estan en el extremo de Z.
        var altos = new List<Vector3>();
        foreach (var v in verts) if (v.y >= maxY - 0.01f) altos.Add(v);

        if (altos.Count == 0)
        {
            // Si no hay nada tan alto, se usa la franja superior.
            foreach (var v in verts) if (v.y >= maxY - 0.05f) altos.Add(v);
        }

        if (altos.Count == 0) return new Vector3(0f, maxY, 0f);

        // Grupo del extremo trasero (Z negativo).
        float corte = minZ + (maxZ - minZ) * 0.25f;
        var asa = new List<Vector3>();
        foreach (var v in altos) if (v.z <= corte) asa.Add(v);

        if (asa.Count == 0)
        {
            como = "sin grupo trasero claro: se usa el centro de la banda";
            Vector3 c = Vector3.zero;
            foreach (var v in altos) c += v;
            c /= altos.Count;
            return c;
        }

        Vector3 suma = Vector3.zero;
        foreach (var v in asa) suma += v;
        suma /= asa.Count;

        como = "barra superior trasera (" + asa.Count + " vertices)";
        return suma;
    }

    // ------------------------------------------------------------------ lector
    static void ArreglarLector(XRGrabInteractable g, System.Text.StringBuilder t)
    {
        var mf = g.GetComponentInChildren<MeshFilter>(true);
        if (mf == null || mf.sharedMesh == null)
        {
            t.AppendLine("    sin mesh: se deja como esta");
            return;
        }

        var mesh = mf.sharedMesh;
        Bounds b = mesh.bounds;

        // OJO: la malla del lector es diminuta en local (0.01 x 0.01 x 0.02) porque el
        // objeto tiene una escala grande (~9x). Por eso el BoxCollider viejo (de 1 cm)
        // quedaba en el aire: hay que trabajar con el tamaño en espacio MUNDO.
        Vector3 escala = mf.transform.lossyScale;
        Vector3 tamMundo = new Vector3(
            b.size.x * Mathf.Abs(escala.x),
            b.size.y * Mathf.Abs(escala.y),
            b.size.z * Mathf.Abs(escala.z));

        // El collider viejo (1x1x1 cm) estaba en el origen, lejos del mesh.
        // Se recoloca para que cubra la maquina de verdad.
        var viejos = g.GetComponents<BoxCollider>();
        Vector3 centroMundo = mf.transform.TransformPoint(b.center);
        Vector3 centroLocal = g.transform.InverseTransformPoint(centroMundo);
        Vector3 tamLocal = new Vector3(
            tamMundo.x / Mathf.Max(0.0001f, Mathf.Abs(escala.x)),
            tamMundo.y / Mathf.Max(0.0001f, Mathf.Abs(escala.y)),
            tamMundo.z / Mathf.Max(0.0001f, Mathf.Abs(escala.z)));

        foreach (var c in viejos)
        {
            c.size = tamLocal;
            c.center = centroLocal;
        }
        t.AppendLine("    collider del lector: size(local)=" + tamLocal + " center(local)=" + centroLocal +
                      " | mundo=" + tamMundo + " (antes 0.01 cubo)");

        // Punto de agarre: donde se sostiene el lector (centro de la maquina, un poco
        // mas abajo que el centro visual para que se agarre por el cuerpo).
        Vector3 puntoLocal = centroLocal + new Vector3(0f, -tamLocal.y * 0.15f, 0f);
        Vector3 tam = new Vector3(
            Mathf.Max(0.02f, tamLocal.x * 1.1f),
            Mathf.Max(0.02f, tamLocal.y * 0.8f),
            Mathf.Max(0.02f, tamLocal.z * 1.1f));

        var punto = CrearPuntoDeAgarre(g.transform, puntoLocal, tam, out BoxCollider col);

        g.colliders.Clear();
        g.colliders.Add(col);

        g.attachTransform = punto.transform;
        g.useDynamicAttach = false;
        g.trackPosition = true;
        g.trackRotation = true;
        g.smoothPosition = false;
        g.smoothRotation = false;

        t.AppendLine("    punto de agarre: local=" + puntoLocal + " collider " + col.size);
    }

    // ------------------------------------------------------------------ comun
    static GameObject CrearPuntoDeAgarre(Transform padre, Vector3 localPos, Vector3 tam, out BoxCollider col)
    {
        // Limpia TODOS los puntos de agarre previos, a cualquier nivel (puede haber
        // varios si la herramienta se ha ejecutado mas de una vez).
        var previos = new List<GameObject>();
        foreach (Transform h in padre.GetComponentsInChildren<Transform>(true))
            if (h.name == NombrePunto && h.gameObject != null)
                previos.Add(h.gameObject);
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
}
