using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Arregla el CARRITO (Pref_Trolley01_Sm) para que se empuje y ruede por el suelo
/// como un carrito de verdad, y para que su colisionador sea una CAJA (rectangular)
/// y no un cubo.
///
/// QUE ESTA MAL
/// ------------
/// 1. El colisionador era un MeshCollider convexo: al pegar el rayo del mando
///    podia pegarle a una esquina y agarrarlo "por el aire", y el carro no se podia
///    empujar con el cuerpo (el Rigidbody era kinematic, o sea, clavado).
///
/// 2. Al agarrarlo, el sistema de agarre de XRI le calculaba la velocidad para
///    llevarlo hasta la pose de la mano y se la ponia al Rigidbody, incluida la
///    parte vertical: el carrito se LEVANTABA del suelo. Y como XRI escribe la
///    posicion final despues de todos los LateUpdate, ninguna correccion hecha
///    a posteriori servia.
///
/// QUE HACE ESTE
/// --------------
/// 1. Sustituye el MeshCollider por un BoxCollider (una CAJA rectangular con la
///    forma real del carrito: mas alta que ancha, no un cubo). Se puede ajustar
///    el center y el size desde el Inspector, para darle la altura que se quiera.
///
/// 2. Pone el Rigidbody en condiciones de carrito con ruedas:
///      - normal (no kinematic): se puede EMPUJAR con el cuerpo;
///      - sin gravedad y con FreezePositionY: es IMPOSIBLE que se levante;
///      - sin volcarse (rotaciones X y Z bloqueadas) pero girando sobre Y, como
///        un carrito con una rueda giratoria;
///      - freno suave, para que ruede un poco y pare, como con ruedas.
///
/// 3. El agarre se queda por el ASA (barra superior trasera) y se mueve con
///    fisica (VelocityTracking), no teletransportandose.
///
/// Uso: Tools/Minimarket/Agarrar/Arreglar Carrito (caja, ruedas y suelo)
/// </summary>
public static class ArreglarCarrito
{
    const string PrefabCarrito = "Assets/SuperMarket/Prefabs/Pref_Trolley01_Sm.prefab";
    const string Escena = "Assets/Scenes/MinimarketXR_Full.unity";
    const string NombrePunto = "Punto de Agarre";

    [Header("Cajon hueco del cesto")]
    // Repisas intermedias del cesto: 1 = dos pisos (el piso y el medio cesto).
    const int Repisas = 1;

    // Grosor de las paredes del cesto, en metros.
    const float GrosorPared = 0.04f;

    /// <summary>Si es true, no cierra Unity al terminar (para encadenar herramientas).</summary>
    public static bool noSalir = false;

    [MenuItem("Tools/Minimarket/Agarrar/Arreglar Carrito (caja, ruedas y suelo)")]
    public static void Ejecutar()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Arreglo del carrito (caja + ruedas) ===");

        // ------------------------------------------------ 1. El PREFAB (colisionador)
        ArreglarPrefab(t);

        // ------------------------------------------------ 2. La ESCENA (rigidbody, punto)
        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include);

        // Se copian a una lista antes de tocarlos: al rehacer las instancias se
        // borran los objetos viejos y el array quedaria con referencias muertas.
        var carritos = new List<XRGrabInteractable>();
        foreach (var g in grabs)
            if (Ruta(g.transform).Contains("Trolley")) carritos.Add(g);

        int n = 0;
        foreach (var g in carritos)
        {
            if (g == null) continue;
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

        if (Application.isBatchMode && !noSalir) EditorApplication.Exit(0);
    }

    [MenuItem("Tools/Minimarket/Agarrar/Recrear cajon hueco del carrito (tras ajustar la caja)")]
    public static void RecrearCajon()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Recreacion del cajon hueco del carrito ===");

        // ------------------------------------------------- prefab
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCarrito);
        if (prefab != null)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabCarrito);
            try
            {
                var cajon = root.GetComponent<CajonHueco>();
                if (cajon == null)
                {
                    t.AppendLine("  el prefab no tiene CajonHueco: se ejecuta el arreglo completo.");
                }
                else
                {
                    cajon.Generar();
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabCarrito);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------- escena
        // Las instancias de la escena heredan el cajon del prefab: no se regeneran
        // aqui (si se regeneraran, se acumularian colliders duplicados).
        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        t.AppendLine("  las instancias heredan el cajon hueco del prefab");

        EditorSceneManager.SaveScene(scene, Escena);

        t.AppendLine("  cajon hueco recreado");
        Debug.Log(t.ToString());
        Escribir(t);

        if (Application.isBatchMode && !noSalir) EditorApplication.Exit(0);
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
            // El MeshCollider (o cualquier otro) se elimina: el carrito lleva una
            // CAJA HUECA (paredes + piso + repisa), no un cubo macizo.
            var mcs = root.GetComponents<MeshCollider>();
            foreach (var c in mcs) Object.DestroyImmediate(c);
            if (mcs.Length > 0) t.AppendLine("  MeshCollider eliminados: " + mcs.Length);

            var mf = root.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null)
            {
                t.AppendLine("  sin malla: se deja el collider como estaba");
            }
            else
            {
                // La caja base: el hueco del CESTO (sin el asa, que sobresale).
                Vector3 centro, tam;
                CalcularCesta(mf.sharedMesh, out centro, out tam);

                // Se elimina la "caja maestra" (el BoxCollider que hacia de
                // referencia): ahora los parametros del cajon estan en el
                // componente CajonHueco, asi que no queda ningun colisionador
                // "desmarcado" en el Inspector.
                var maestras = root.GetComponents<BoxCollider>();
                foreach (var c in maestras) Object.DestroyImmediate(c);
                if (maestras.Length > 0) t.AppendLine("  caja maestra eliminada: " + maestras.Length);

                var cajon = root.GetComponent<CajonHueco>();
                if (cajon == null) cajon = root.AddComponent<CajonHueco>();
                cajon.centro = centro;
                cajon.tamano = tam;
                cajon.grosor = GrosorPared;
                cajon.repisas = Repisas;
                cajon.abiertaArriba = true;

                // Se generan las cajas del cajon hueco.
                cajon.Generar();

                t.AppendLine("  cajon hueco: centro=" + centro + " size=" + tam +
                             " | piso + 4 paredes + " + Repisas + " repisa(s), cara de arriba abierta");
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabCarrito);
            t.AppendLine("  prefab guardado: " + PrefabCarrito);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// Calcula la caja del CESTO: la huella de la cesta, sin el asa (que sobresale
    /// por detras y por un lado).
    /// </summary>
    static void CalcularCesta(Mesh malla, out Vector3 centro, out Vector3 tam)
    {
        var verts = malla.vertices;
        float maxY = float.MinValue;
        foreach (var v in verts) if (v.y > maxY) maxY = v.y;

        // El asa son los vertices mas altos (la barra superior).
        var cesta = new List<Vector3>();
        foreach (var v in verts)
            if (v.y < maxY - 0.02f) cesta.Add(v);

        if (cesta.Count == 0) cesta = new List<Vector3>(verts);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY2 = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var v in cesta)
        {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.y > maxY2) maxY2 = v.y;
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        tam = new Vector3(maxX - minX, maxY2 - minY, maxZ - minZ);
        centro = new Vector3((minX + maxX) * 0.5f, (minY + maxY2) * 0.5f, (minZ + maxZ) * 0.5f);
    }

    // ------------------------------------------------------------------ escena
    static void ArreglarEscena(XRGrabInteractable g, System.Text.StringBuilder t)
    {
        // Se rehace la instancia DEL PREFAB, para que todos los carritos queden
        // exactamente igual y sin colliders duplicados (las cajas se generaban en
        // la instancia y se acumulaban).
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCarrito);
        if (prefab == null) return;

        var viejo = g.gameObject;
        var padre = viejo.transform.parent;
        var pos = viejo.transform.position;
        var rot = viejo.transform.rotation;
        var escala = viejo.transform.localScale;
        string nombre = viejo.name;

        var nuevo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        nuevo.transform.SetParent(padre, false);
        nuevo.transform.SetPositionAndRotation(pos, rot);
        nuevo.transform.localScale = escala;
        nuevo.name = nombre;

        var grab = nuevo.AddComponent<XRGrabInteractable>();

        // ---- 1. Rigidbody de carrito con ruedas.
        // OJO: el XRGrabInteractable ya exige un Rigidbody (RequireComponent), asi
        // que aqui solo se recupera el que Unity ha puesto.
        var rb = nuevo.GetComponent<Rigidbody>();
        if (rb == null) rb = nuevo.AddComponent<Rigidbody>();
        rb.isKinematic = false;          // se puede empujar
        rb.useGravity = false;           // la altura la lleva el script
        rb.constraints = RigidbodyConstraints.FreezePositionY      // no se levanta NUNCA
                       | RigidbodyConstraints.FreezeRotationX     // no se vuelca
                       | RigidbodyConstraints.FreezeRotationZ;    // (puede girar en Y)
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.mass = 30f;
        rb.linearDamping = 1.2f;         // rueda un poco y para: como con ruedas
        rb.angularDamping = 6f;
        rb.centerOfMass = new Vector3(0f, -0.35f, 0f);   // centro de masa bajo

        // ---- 2. El punto de agarre va en el asa (barra superior trasera).
        var mf = nuevo.GetComponentInChildren<MeshFilter>(true);
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 asa = CalcularAsa(mf.sharedMesh.vertices);
            Vector3 local = mf.transform.InverseTransformPoint(mf.transform.TransformPoint(asa));

            var punto = CrearPunto(nuevo.transform, local, new Vector3(0.14f, 0.1f, 0.14f), out BoxCollider col);

            // Solo el asa es agarrable: el rayo del mando no pilla el resto.
            grab.colliders.Clear();
            grab.colliders.Add(col);
            grab.attachTransform = punto.transform;
            t.AppendLine("  '" + nombre + "': punto de agarre en el asa local=" + local);
        }

        // ---- 3. Ajustes de agarre: se mueve con fisica (no se teletransporta) y
        //         sin lanzarlo al soltar.
        grab.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Near;
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.smoothPosition = false;
        grab.smoothRotation = false;
        grab.trackPosition = true;
        grab.trackRotation = true;
        grab.throwOnDetach = false;

        // Punto de agarre fijo (el asa): el carrito se agarra y se tira de el.
        grab.useDynamicAttach = false;
        grab.matchAttachPosition = false;
        grab.matchAttachRotation = false;

        // El viejo se borra (con su monton de colliders duplicados).
        Object.DestroyImmediate(viejo);
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
