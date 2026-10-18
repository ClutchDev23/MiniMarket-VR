using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class ProductosRellenar
{
    const string Fbx = "Assets/Productos/Models/Super market Models FBX File.fbx";
    const string PadreProd = "Productos";
    const string PadreSock = "Sockets";

    const int MaxPorEstante = 3;
    const int MaxCeldas = 6;
    const int MaxTotal = 320;
    const float ObjetoMax = 0.12f;
    const float EscalaMin = 2.0f;
    const float EscalaMax = 8.0f;
    const float PasoGrid = 0.15f;
    const float SepCelda = 0.24f;
    const float Elevacion = 0.004f;

    static int LogEjemplos = 0;

    static readonly HashSet<string> Excluir = new HashSet<string> { "rack", "Cylinder" };

    static readonly string[] Escenas =
    {
        "Assets/Scenes/MinimarketXR_Full.unity",
        "Assets/Scenes/MinimarketXR_Night.unity",
    };

    static readonly Dictionary<string, string> Estantes = new Dictionary<string, string>
    {
        { "StandingRack01", "29ccca18fe20f1046b1caffc3c0ee1d3" },
        { "StandingRack02", "1af4fd586da010d4a8f8ad5acc6f242d" },
        { "WallRack01",     "f84f1190f7f24d14fb466863458dd85a" },
        { "WallRack02",     "07f507d2b0b6df544af029236fd8dc50" },
        { "WallRack03",     "8d362f2a5669e474d83aa927e71fa1ea" },
        { "CrateStand01",   "b91930e356b912e4dbf98a0ee09f7d33" },
        { "CrateStand_Load","56b2cf03f360f0f498da30f9f9b95258" },
        { "CashCounter",    "76296b567362f34458915f7f26c7b861" },
        { "Freezer",        "4fc55f4386fce24448a635fded679593" },
    };

    class Producto
    {
        public string Nombre;
        public Mesh Malla;
        public Material[] Mats;
        public Quaternion RotLocal;
        public Vector3 EscalaLocal;
    }

    [MenuItem("Tools/Minimarket/Productos/Rellenar Estantes + Sockets")]
    public static void Ejecutar()
    {
        int errores = 0;
        Debug.Log("[Rellenar] iniciando");

        var catalogo = CargarCatalogo();
        if (catalogo.Count == 0)
        {
            Debug.LogError("[Rellenar] catalogo vacio");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        Debug.Log("[Rellenar] catalogo: " + catalogo.Count + " productos");

        foreach (var path in Escenas)
        {
            try { ProcesarEscena(path, catalogo); }
            catch (System.Exception e) { Debug.LogError("[Rellenar] EX " + path + ": " + e); errores++; }
        }

        Debug.Log(errores == 0 ? "[Rellenar] TODO LISTO" : "[Rellenar] ERRORES " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static List<Producto> CargarCatalogo()
    {
        var lista = new List<Producto>();
        var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (raiz == null) return lista;

        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t == raiz.transform) continue;
            if (Excluir.Contains(t.name)) continue;

            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null || mr == null) continue;

            lista.Add(new Producto
            {
                Nombre = t.name,
                Malla = mf.sharedMesh,
                Mats = mr.sharedMaterials,
                RotLocal = t.localRotation,
                EscalaLocal = t.localScale,
            });
        }

        lista.Sort((a, b) => string.CompareOrdinal(a.Nombre, b.Nombre));
        return lista;
    }

    static void ProcesarEscena(string path, List<Producto> catalogo)
    {
        if (!System.IO.File.Exists(path)) { Debug.LogError("[Rellenar] falta escena " + path); return; }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Debug.Log("[Rellenar] abierta " + path);

        // Limpiar corridas anteriores ANTES de recolectar.
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == PadreProd || root.name == PadreSock)
                Object.DestroyImmediate(root.gameObject);

        var todo = new List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
            todo.AddRange(root.GetComponentsInChildren<Transform>(true));

        var padreProd = new GameObject(PadreProd);
        var padreSock = new GameObject(PadreSock);

        var estantes = new List<(Transform t, Collider[] cols)>();
        foreach (var t in todo)
        {
            var instPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t);
            if (string.IsNullOrEmpty(instPath)) continue;
            var g = AssetDatabase.AssetPathToGUID(instPath);
            if (!Estantes.ContainsValue(g)) continue;

            var cols = t.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0) continue;
            estantes.Add((t, cols));
        }
        estantes = estantes
            .OrderBy(e => e.t.name)
            .ThenBy(e => e.t.position.x)
            .ThenBy(e => e.t.position.z)
            .ThenBy(e => e.t.position.y)
            .ToList();
        Debug.Log("[Rellenar] estantes: " + estantes.Count);

        Physics.SyncTransforms();

        var porEstante = new List<(Transform t, Collider[] cols, List<Vector3> cand)>();
        foreach (var e in estantes)
            porEstante.Add((e.t, e.cols, Recolectar(e.cols)));

        int totalCand = porEstante.Sum(p => p.cand.Count);
        int conCand = porEstante.Count(p => p.cand.Count > 0);
        Debug.Log($"[Rellenar] candidatas={totalCand} en {conCand}/{estantes.Count} estantes");

        var rnd = new System.Random(12345);
        var usadas = new List<Vector3>();
        int creados = 0, sockets = 0, idxCat = 0;

        foreach (var p in porEstante)
        {
            if (creados >= MaxTotal) break;
            if (p.cand.Count == 0) continue;

            var celdas = ElegirCeldas(p.cand, MaxCeldas, rnd, usadas);
            int conProd = 0;

            foreach (var celda in celdas)
            {
                bool hacerProducto = conProd < MaxPorEstante && creados < MaxTotal;
                Producto prod = null;
                if (hacerProducto)
                {
                    prod = catalogo[idxCat % catalogo.Count];
                    idxCat++;
                    conProd++;
                }

                CrearSocket(celda, prod, padreSock.transform, padreProd.transform, rnd, usadas);
                sockets++;
                if (hacerProducto) creados++;
            }
        }

        Debug.Log($"[Rellenar] {path}: sockets={sockets} productos={creados}");

        if (!EditorSceneManager.SaveScene(scene, path))
            Debug.LogError("[Rellenar] fallo SaveScene " + path);
    }

    static List<Vector3> ElegirCeldas(List<Vector3> cand, int max, System.Random rnd, List<Vector3> usadas)
    {
        var orden = Enumerable.Range(0, cand.Count).ToList();
        for (int i = orden.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(0, i + 1);
            int tmp = orden[i]; orden[i] = orden[j]; orden[j] = tmp;
        }

        var elegidas = new List<Vector3>();
        foreach (var ci in orden)
        {
            if (elegidas.Count >= max) break;
            var pos = cand[ci];

            bool choca = false;
            foreach (var u in usadas)
                if ((u - pos).sqrMagnitude < SepCelda * SepCelda) { choca = true; break; }
            if (choca) continue;

            foreach (var e in elegidas)
                if ((e - pos).sqrMagnitude < SepCelda * SepCelda) { choca = true; break; }
            if (choca) continue;

            elegidas.Add(pos);
            usadas.Add(pos);
        }
        return elegidas;
    }

    static List<Vector3> Recolectar(Collider[] cols)
    {
        var res = new List<Vector3>();
        var b = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
        if (b.size.x < 0.05f || b.size.z < 0.05f) return res;

        float inset = 0.07f;
        int nx = Mathf.Max(2, Mathf.FloorToInt((b.size.x - inset * 2f) / PasoGrid) + 1);
        int nz = Mathf.Max(2, Mathf.FloorToInt((b.size.z - inset * 2f) / PasoGrid) + 1);
        float dx = (b.size.x - inset * 2f) / Mathf.Max(1, nx - 1);
        float dz = (b.size.z - inset * 2f) / Mathf.Max(1, nz - 1);

        float yTop = b.max.y + 0.25f;
        float yBot = b.min.y - 0.05f;

        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            float px = b.min.x + inset + dx * ix;
            float pz = b.min.z + inset + dz * iz;
            float yCur = yTop;

            while (yCur > yBot)
            {
                var origen = new Vector3(px, yCur, pz);
                if (!Physics.Raycast(origen, Vector3.down, out var hit, yCur - yBot,
                        ~0, QueryTriggerInteraction.Ignore))
                    break;

                bool propio = false;
                for (int c = 0; c < cols.Length; c++)
                    if (cols[c] == hit.collider) { propio = true; break; }

                if (propio && hit.normal.y > 0.7f && hit.point.y > b.min.y + 0.02f)
                    res.Add(new Vector3(px, hit.point.y, pz));

                yCur = hit.point.y - 0.03f;
            }
        }
        return res;
    }

    static void CrearSocket(Vector3 celda, Producto prod, Transform padreSock, Transform padreProd,
        System.Random rnd, List<Vector3> usadas)
    {
        var sock = new GameObject("Socket");
        sock.transform.SetParent(padreSock, false);

        var trig = sock.AddComponent<BoxCollider>();
        trig.isTrigger = true;

        if (prod == null)
        {
            // Hueco libre: trigger generico justo sobre la balda.
            sock.transform.position = celda + Vector3.up * 0.055f;
            trig.center = Vector3.zero;
            trig.size = new Vector3(0.26f, 0.16f, 0.26f);
        }
        else
        {
            var go = new GameObject(prod.Nombre);
            go.transform.SetParent(padreProd, false);

            // Tamaño real del nodo (bounds x escala del FBX) para no multiplicar dos veces.
            var ls = prod.EscalaLocal;
            float maxReal = Mathf.Max(
                Mathf.Abs(ls.x) * prod.Malla.bounds.size.x,
                Mathf.Abs(ls.y) * prod.Malla.bounds.size.y,
                Mathf.Abs(ls.z) * prod.Malla.bounds.size.z);
            if (maxReal <= 1e-9f) maxReal = 1f;

            float factor = ObjetoMax / maxReal;
            float azimut = rnd.Next(0, 360);

            go.transform.localRotation = Quaternion.Euler(0f, azimut, 0f) * prod.RotLocal;
            go.transform.localScale = ls * factor;

            if (LogEjemplos < 6)
            {
                LogEjemplos++;
                var fin = Vector3.Scale(Vector3.Scale(prod.Malla.bounds.size, ls), Vector3.one * factor);
                Debug.Log($"[Rellenar] EJEMPLO {prod.Nombre}: escalaFBX={ls} maxReal={maxReal:F3} factor={factor:F4} tamFinal={fin.ToString("F4")}");
            }

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = prod.Malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = prod.Mats;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;

            var box = go.AddComponent<BoxCollider>();
            box.size = prod.Malla.bounds.size;
            box.center = prod.Malla.bounds.center;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.smoothPosition = true;
            grab.smoothRotation = true;
            grab.useDynamicAttach = false;

            // Asentar (bounds.min.y sobre la balda).
            var wb = MundoBounds(go.transform, prod.Malla);
            var p = go.transform.position;
            p.x = celda.x;
            p.z = celda.z;
            p.y += (celda.y + Elevacion) - wb.min.y;
            go.transform.position = p;

            // El socket vive EXACTAMENTE donde esta el producto (attach = su origen).
            sock.transform.position = go.transform.position;
            trig.center = wb.center - go.transform.position;
            trig.size = Vector3.Scale(wb.size, new Vector3(1.35f, 1.35f, 1.35f));
        }

        var si = sock.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        si.socketActive = true;
        si.recycleDelayTime = 0f;
        si.interactableHoverScale = 1f;
        si.showInteractableHoverMeshes = false;
        si.hoverSocketSnapping = false;
        si.socketSnappingRadius = 0.2f;
        usadas.Add(sock.transform.position);
    }

    static Bounds MundoBounds(Transform t, Mesh m)
    {
        var lb = m.bounds;
        var mtx = t.localToWorldMatrix;
        var res = new Bounds(mtx.MultiplyPoint3x4(lb.min), Vector3.zero);
        foreach (var c in new[]
        {
            new Vector3(lb.min.x, lb.min.y, lb.min.z), new Vector3(lb.max.x, lb.min.y, lb.min.z),
            new Vector3(lb.min.x, lb.max.y, lb.min.z), new Vector3(lb.min.x, lb.min.y, lb.max.z),
            new Vector3(lb.max.x, lb.max.y, lb.min.z), new Vector3(lb.max.x, lb.min.y, lb.max.z),
            new Vector3(lb.min.x, lb.max.y, lb.max.z), new Vector3(lb.max.x, lb.max.y, lb.max.z),
        })
            res.Encapsulate(mtx.MultiplyPoint3x4(c));
        return res;
    }
}
