using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Tools/Minimarket/Productos/Llenar tienda (tamano real, todo agarrable)
///
/// Reemplaza los productos de los estantes (escenas de dia y noche) por una tienda LLENA:
///   - Cada producto con su TAMANO REAL (lata 12 cm, botella de vino 30 cm, cereal 30 cm...).
///   - Estantes llenos: filas del mismo producto, de adelante hacia atras.
///   - TODOS se pueden agarrar y escanear.
///   - Rendimiento: se dibujan por bloques (BloqueProductos) y estan quietos hasta
///     que los agarras (ProductoEstante). Se quitan los 642 "sockets" viejos, que
///     gastaban mucha CPU.
///
/// Escribe un resumen en Logs/llenar_tienda.txt. Se puede repetir cuando quieras.
/// </summary>
public static class ProductosLlenarTienda
{
    const string Fbx = "Assets/Productos/Models/Super market Models FBX File.fbx";
    static readonly string[] Escenas =
    {
        "Assets/Scenes/MinimarketXR_Full.unity",
        "Assets/Scenes/MinimarketXR_Night.unity",
    };

    // Estantes que se llenan (prefabs). La caja NO: ahi trabaja el cajero.
    static readonly Dictionary<string, string> Estantes = new Dictionary<string, string>
    {
        { "StandingRack01", "29ccca18fe20f1046b1caffc3c0ee1d3" },
        { "StandingRack02", "1af4fd586da010d4a8f8ad5acc6f242d" },
        { "WallRack01",     "f84f1190f7f24d14fb466863458dd85a" },
        { "WallRack02",     "07f507d2b0b6df544af029236fd8dc50" },
        { "WallRack03",     "8d362f2a5669e474d83aa927e71fa1ea" },
        { "CrateStand01",   "b91930e356b912e4dbf98a0ee09f7d33" },
        { "CrateStand_Load","56b2cf03f360f0f498da30f9f9b95258" },
        { "Freezer",        "4fc55f4386fce24448a635fded679593" },
    };

    // ------------------------------------------------------------------ ajustes
    const int MaxProductos = 7600;       // tope total por escena (rendimiento en Quest)
    const int MaxFilasFondo = 1;         // filas hacia el fondo (1 = la fila de adelante, la que se ve)
    const float Paso = 0.05f;            // resolucion del sondeo de baldas
    const float Hueco = 0.008f;          // separacion entre productos
    const float AlturaMaxima = 1.85f;    // no llenar baldas mas altas que esto (sobre el piso)
    const float TamBloque = 8f;          // tamano de los bloques de dibujo (m)

    /// <summary>Tamano REAL (lado mas largo, metros) por tipo de producto.</summary>
    static float TamanoReal(string n)
    {
        n = n.ToLowerInvariant();
        if (n.StartsWith("pringles 5") || n.StartsWith("pringles 6")) return 0.085f;
        if (n.StartsWith("alcohol")) return 0.30f;
        if (n.StartsWith("beer can") || n.StartsWith("soda can")) return 0.122f;
        if (n.StartsWith("biscuits")) return 0.21f;
        if (n.StartsWith("oreo")) return 0.20f;
        if (n.StartsWith("candy")) return 0.17f;
        if (n.StartsWith("cereal")) return 0.30f;
        if (n.StartsWith("chips")) return 0.27f;
        if (n.StartsWith("chocolate")) return 0.15f;
        if (n.StartsWith("fanta")) return 0.25f;
        if (n.StartsWith("water")) return 0.24f;
        if (n.StartsWith("jam")) return 0.11f;
        if (n.StartsWith("mayo")) return 0.15f;
        if (n.StartsWith("nutella")) return 0.13f;
        if (n.StartsWith("juice")) return 0.20f;
        if (n.StartsWith("milk")) return 0.21f;
        if (n.StartsWith("katchup")) return 0.20f;
        if (n.StartsWith("musturd")) return 0.18f;
        if (n.StartsWith("oil")) return 0.29f;
        if (n.StartsWith("pringles")) return 0.235f;
        if (n.StartsWith("tea")) return 0.13f;
        return 0.15f;
    }

    static bool EsBebida(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("can") || n.Contains("bottle") || n.Contains("juice") || n.Contains("milk");
    }

    class Prod
    {
        public string nombre;
        public Mesh malla;
        public Material[] mats;
        public Quaternion rot;        // rotacion base del fbx (de pie)
        public Vector3 escala;        // escala final (tamano real)
        public Vector3 tam;           // ancho (x), alto (y), fondo (z) ya girado
        public float yawExtra;        // 0 o 90: la cara ancha mira al pasillo
        public float tilt;            // -90 = se para (bolsas y chocolates que venian echados)
        public float peso;            // probabilidad (menos para los de muchos triangulos)
        public int tris;
        public bool bebida;
    }

    class Lugar
    {
        public Prod prod;
        public Vector3 centroBase;    // centro de la base, en el mundo
        public Quaternion giroEstante;
        public int fila;
    }

    // ================================================================== menu

    [MenuItem("Tools/Minimarket/Productos/Llenar tienda (tamano real, todo agarrable)")]
    public static void Ejecutar()
    {
        if (!EditorUtility.DisplayDialog("Llenar tienda",
                "Esto REEMPLAZA los productos de los estantes de las escenas de dia y noche por la tienda llena " +
                "(tamano real, todo agarrable) y guarda las escenas.\n\n¿Seguir?", "Si, llenar", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Las mallas tienen que poder leerse en el juego (para dibujarlas por bloques).
        var mi = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        if (mi != null && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); }

        var catalogo = Catalogo();
        var log = new StringBuilder();
        log.AppendLine("Catalogo: " + catalogo.Count + " productos");
        foreach (var p in catalogo)
            log.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "  {0}: {1:0.000} x {2:0.000} x {3:0.000} m (tris {4})", p.nombre, p.tam.x, p.tam.y, p.tam.z, p.tris));

        try
        {
            for (int i = 0; i < Escenas.Length; i++)
            {
                if (!File.Exists(Escenas[i])) continue;
                EditorUtility.DisplayProgressBar("Llenar tienda", "Escena " + Path.GetFileNameWithoutExtension(Escenas[i]), (i + 0.5f) / Escenas.Length);
                Procesar(Escenas[i], catalogo, log);
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        EditorSceneManager.OpenScene(Escenas[0], OpenSceneMode.Single);
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Logs", "llenar_tienda.txt"), log.ToString());
        Debug.Log("[Llenar] " + log);
        EditorUtility.DisplayDialog("Llenar tienda", ResumenCorto(log.ToString()), "OK");
    }

    /// <summary>
    /// Llena UN mueble (por ejemplo el estante del cuarto del menu) con productos de
    /// tamano real, agarrables. Devuelve cuantos puso.
    /// </summary>
    public static int LlenarMueble(Transform mueble, Transform padre, float pisoY, int semilla)
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        if (mi != null && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); }
        var catalogo = Catalogo();
        if (catalogo.Count == 0) return 0;
        Physics.SyncTransforms();
        var lugares = new List<Lugar>();
        LlenarEstante(mueble, catalogo, pisoY, new System.Random(semilla), lugares);
        var bloque = new GameObject("Productos " + mueble.name);
        bloque.transform.SetParent(padre, false);
        bloque.AddComponent<BloqueProductos>();
        foreach (var l in lugares) Crear(l, bloque.transform);
        return lugares.Count;
    }

    static string ResumenCorto(string log)
    {
        var lineas = log.Split('\n').Where(l => l.StartsWith("ESCENA")).ToArray();
        return lineas.Length > 0 ? string.Join("\n", lineas) : "Listo.";
    }

    // ================================================================== catalogo

    static List<Prod> Catalogo()
    {
        var lista = new List<Prod>();
        var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (raiz == null) return lista;

        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t == raiz.transform || t.name == "rack" || t.name == "Cylinder") continue;
            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null || mr == null) continue;
            if (BaseDatosProductos.Buscar(t.name) == null) continue;   // sin precio no se vende

            var m = mf.sharedMesh;
            // Tamano en el mundo con la rotacion y escala del fbx.
            var s0 = TamGirado(m.bounds, t.localRotation, t.localScale);
            float factor = TamanoReal(t.name) / Mathf.Max(s0.x, Mathf.Max(s0.y, s0.z));
            var tam = s0 * factor;
            // Bolsas de caramelos, chocolates, galletas planas: venian ECHADOS y en el
            // estante casi no se veian. Se paran (como en una tienda de verdad).
            float tilt = 0f;
            if (tam.y < 0.5f * Mathf.Min(tam.x, tam.z)) { tilt = -90f; tam = new Vector3(tam.x, tam.z, tam.y); }
            float yaw = 0f;
            if (tam.z > tam.x) { yaw = 90f; tam = new Vector3(tam.z, tam.y, tam.x); }   // cara ancha al frente

            int tris = m.triangles.Length / 3;
            lista.Add(new Prod
            {
                nombre = t.name,
                malla = m,
                mats = mr.sharedMaterials,
                rot = t.localRotation,
                escala = t.localScale * factor,
                tam = tam,
                yawExtra = yaw,
                tilt = tilt,
                tris = tris,
                peso = tris > 1000 ? 0.35f : tris > 500 ? 0.7f : 1f,
                bebida = EsBebida(t.name),
            });
        }
        return lista.OrderBy(p => p.nombre).ToList();
    }

    static Vector3 TamGirado(Bounds b, Quaternion r, Vector3 s)
    {
        var m = Matrix4x4.TRS(Vector3.zero, r, s);
        var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            var w = m.MultiplyPoint3x4(c);
            min = Vector3.Min(min, w); max = Vector3.Max(max, w);
        }
        return max - min;
    }

    // ================================================================== escena

    static void Procesar(string ruta, List<Prod> catalogo, StringBuilder log)
    {
        var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);

        // Fuera lo anterior (productos y sockets viejos).
        foreach (var r in escena.GetRootGameObjects())
            if (r.name == "Productos" || r.name == "Sockets")
                Object.DestroyImmediate(r);
        Physics.SyncTransforms();

        var piso = GameObject.Find("Floor");
        float pisoY = 0.58f;
        if (piso != null)
        {
            var rs = piso.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0) { var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); pisoY = b.max.y; }
        }

        // Estantes
        var estantes = new List<Transform>();
        foreach (var r in escena.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                var ruta2 = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t);
                if (string.IsNullOrEmpty(ruta2)) continue;
                if (Estantes.ContainsValue(AssetDatabase.AssetPathToGUID(ruta2))) estantes.Add(t);
            }
        estantes = estantes.OrderBy(e => e.position.x).ThenBy(e => e.position.z).ToList();

        var rnd = new System.Random(2026);
        var lugares = new List<Lugar>();
        int baldas = 0;
        foreach (var e in estantes)
            baldas += LlenarEstante(e, catalogo, pisoY, rnd, lugares);

        // Tope de rendimiento: primero se quitan filas de atras.
        int antes = lugares.Count;
        for (int fila = MaxFilasFondo - 1; fila >= 1 && lugares.Count > MaxProductos; fila--)
        {
            var atras = lugares.Where(l => l.fila == fila).OrderBy(_ => rnd.Next()).ToList();
            int quitar = Mathf.Min(atras.Count, lugares.Count - MaxProductos);
            var fuera = new HashSet<Lugar>(atras.Take(quitar));
            lugares.RemoveAll(l => fuera.Contains(l));
        }
        if (lugares.Count > MaxProductos)
            lugares = lugares.OrderBy(_ => rnd.Next()).Take(MaxProductos).ToList();

        // Crear
        var raiz = new GameObject("Productos");
        var bloques = new Dictionary<Vector2Int, Transform>();
        int tris = 0;
        foreach (var l in lugares)
        {
            var k = new Vector2Int(Mathf.FloorToInt(l.centroBase.x / TamBloque), Mathf.FloorToInt(l.centroBase.z / TamBloque));
            if (!bloques.TryGetValue(k, out var bloque))
            {
                var go = new GameObject("Bloque " + k.x + "," + k.y);
                go.transform.SetParent(raiz.transform, false);
                go.AddComponent<BloqueProductos>();
                bloque = go.transform;
                bloques[k] = bloque;
            }
            Crear(l, bloque);
            tris += l.prod.tris;
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        var porTipo = lugares.GroupBy(l => l.prod.nombre).OrderByDescending(g => g.Count()).Take(8)
                             .Select(g => g.Key + "=" + g.Count());
        log.AppendLine("ESCENA " + Path.GetFileNameWithoutExtension(ruta) + ": " + lugares.Count + " productos (" +
                       (antes > lugares.Count ? "de " + antes + " posibles, " : "") + estantes.Count + " estantes, " +
                       baldas + " baldas, " + bloques.Count + " bloques, " + tris + " triangulos)");
        log.AppendLine("  mas usados: " + string.Join(", ", porTipo));
    }

    // ================================================================== llenar un estante

    static int LlenarEstante(Transform est, List<Prod> catalogo, float pisoY, System.Random rnd, List<Lugar> salida)
    {
        var originales = new HashSet<Collider>(est.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.enabled));

        // Se sondea con la FORMA VISIBLE del mueble: colisionadores temporales de malla.
        // (Algunos muebles, como las refrigeradoras, tienen una caja maciza de colision
        // y sus baldas de adentro no se detectaban.)
        var temporales = new List<MeshCollider>();
        foreach (var mf in est.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            temporales.Add(mc);
        }
        Physics.SyncTransforms();
        try { return LlenarEstante(est, catalogo, pisoY, rnd, salida, originales, temporales); }
        finally { foreach (var mc in temporales) Object.DestroyImmediate(mc); Physics.SyncTransforms(); }
    }

    static int LlenarEstante(Transform est, List<Prod> catalogo, float pisoY, System.Random rnd, List<Lugar> salida,
                             HashSet<Collider> originales, List<MeshCollider> temporales)
    {
        var propios = new HashSet<Collider>(originales);
        foreach (var t in temporales) propios.Add(t);
        if (propios.Count == 0) return 0;

        var rends = est.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return 0;
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);

        // Rotacion del estante (solo el giro horizontal).
        var fwd = est.forward; fwd.y = 0;
        var giro = fwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fwd.normalized) : Quaternion.identity;
        var inv = Quaternion.Inverse(giro);
        bool soloBebidas = est.name.Contains("Freezer");

        // 1. Sondeo: puntos de apoyo (baldas) del propio estante.
        var puntos = new List<Vector3>();
        for (float x = b.min.x + 0.02f; x <= b.max.x - 0.02f; x += Paso)
        for (float z = b.min.z + 0.02f; z <= b.max.z - 0.02f; z += Paso)
        {
            float y = b.max.y + 0.2f;
            while (y > b.min.y)
            {
                if (!Physics.Raycast(new Vector3(x, y, z), Vector3.down, out var hit, y - b.min.y + 0.05f, ~0, QueryTriggerInteraction.Ignore)) break;
                if (propios.Contains(hit.collider) && hit.normal.y > 0.8f &&
                    hit.point.y > pisoY + 0.006f && hit.point.y < pisoY + AlturaMaxima)
                    puntos.Add(hit.point);
                y = hit.point.y - 0.02f;
            }
        }
        if (puntos.Count == 0) return 0;

        // 2. Agrupar por altura (cada grupo = una balda).
        var niveles = new List<List<Vector3>>();
        foreach (var p in puntos.OrderBy(p => p.y))
        {
            if (niveles.Count > 0 && Mathf.Abs(niveles[niveles.Count - 1][0].y - p.y) < 0.03f) niveles[niveles.Count - 1].Add(p);
            else niveles.Add(new List<Vector3> { p });
        }
        niveles = niveles.Where(n => n.Count >= 3).ToList();

        int usadas = 0;
        for (int iv = 0; iv < niveles.Count; iv++)
        {
            var pts = niveles[iv];
            float y = pts.Average(p => p.y);
            float libre = iv + 1 < niveles.Count
                ? niveles[iv + 1].Average(p => p.y) - y - 0.03f
                : Mathf.Min(0.40f, pisoY + 2.0f - y);
            if (libre < 0.08f) continue;

            // Rectangulo de la balda, en coordenadas del estante.
            var loc = pts.Select(p => inv * (p - est.position)).ToList();
            float minX = loc.Min(p => p.x) - 0.02f, maxX = loc.Max(p => p.x) + 0.02f;
            float minZ = loc.Min(p => p.z) - 0.02f, maxZ = loc.Max(p => p.z) + 0.02f;
            bool largoEnX = (maxX - minX) >= (maxZ - minZ);
            float uMin = largoEnX ? minX : minZ, uMax = largoEnX ? maxX : maxZ;
            float vMin = largoEnX ? minZ : minX, vMax = largoEnX ? maxZ : maxX;
            float fondo = vMax - vMin;

            var aptos = catalogo.Where(p => p.tam.y <= libre - 0.01f && p.tam.z <= fondo &&
                                            (!soloBebidas || p.bebida)).ToList();
            if (aptos.Count == 0) continue;
            usadas++;

            var ocupado = new List<Vector2>();   // tramos [u0,u1] ya usados en esta balda
            float u = uMin + 0.01f;
            while (u < uMax - 0.03f)
            {
                var prod = Elegir(aptos, rnd);
                float ancho = prod.tam.x + Hueco, prof = prod.tam.z + Hueco;
                float seccion = 0.25f + (float)rnd.NextDouble() * 0.35f;
                float inicio = u;
                bool alguno = false;
                while (u + ancho <= uMax + 0.005f && (u - inicio) < seccion)
                {
                    for (int fila = 0; fila < MaxFilasFondo; fila++)
                    {
                        // De adelante hacia atras. Si choca con la barandita del frente
                        // de la balda, se prueba un poco mas atras.
                        foreach (float atras in new[] { 0f, 0.025f, 0.05f, 0.08f })
                        {
                            float v = vMax - 0.01f - atras - (fila + 0.5f) * prof;
                            if (v - prof * 0.5f < vMin) break;
                            float cu = u + ancho * 0.5f;
                            var local = largoEnX ? new Vector3(cu, 0, v) : new Vector3(v, 0, cu);
                            var mundo = est.position + giro * local;
                            mundo.y = y;
                            var rotProd = giro * Quaternion.Euler(0, largoEnX ? 0 : 90, 0);
                            if (!Cabe(mundo, prod, rotProd, propios, originales, y)) continue;
                            salida.Add(new Lugar { prod = prod, centroBase = mundo, giroEstante = rotProd, fila = fila });
                            if (fila == 0) ocupado.Add(new Vector2(u, u + ancho));
                            alguno = true;
                            break;
                        }
                    }
                    u += ancho;
                }
                if (!alguno) u += 0.04f;   // hueco (parante, etc.): seguir mas alla
            }

            // 2a PASADA: los huecos que quedaron (entre parantes, en los cubiculos chicos)
            // se rellenan con lo que quepa, de a poquito. Antes quedaban cubiculos vacios.
            var chicos = aptos.OrderBy(p => p.tam.x).ToList();
            u = uMin + 0.005f;
            while (u < uMax - 0.03f)
            {
                var tramo = ocupado.FirstOrDefault(o => u >= o.x - 0.001f && u < o.y);
                if (tramo != default) { u = tramo.y + 0.001f; continue; }
                float siguiente = ocupado.Where(o => o.x >= u).Select(o => o.x).DefaultIfEmpty(uMax).Min();
                bool puso = false;
                // Prueba unos cuantos al azar entre los que caben en el hueco.
                var candidatos = chicos.Where(p => p.tam.x + Hueco <= siguiente - u + 0.002f).ToList();
                for (int intento = 0; intento < 4 && candidatos.Count > 0 && !puso; intento++)
                {
                    var prod = Elegir(candidatos, rnd);
                    float ancho = prod.tam.x + Hueco, prof = prod.tam.z + Hueco;
                    foreach (float atras in new[] { 0f, 0.025f, 0.05f, 0.08f })
                    {
                        float v = vMax - 0.01f - atras - 0.5f * prof;
                        if (v - prof * 0.5f < vMin) break;
                        float cu = u + ancho * 0.5f;
                        var local = largoEnX ? new Vector3(cu, 0, v) : new Vector3(v, 0, cu);
                        var mundo = est.position + giro * local;
                        mundo.y = y;
                        var rotProd = giro * Quaternion.Euler(0, largoEnX ? 0 : 90, 0);
                        if (!Cabe(mundo, prod, rotProd, propios, originales, y)) continue;
                        salida.Add(new Lugar { prod = prod, centroBase = mundo, giroEstante = rotProd, fila = 0 });
                        ocupado.Add(new Vector2(u, u + ancho));
                        u += ancho;
                        puso = true;
                        break;
                    }
                    if (!puso) candidatos.Remove(prod);
                }
                if (!puso) u += 0.02f;
            }
        }
        return usadas;
    }

    static Prod Elegir(List<Prod> lista, System.Random rnd)
    {
        float total = lista.Sum(p => p.peso);
        float r = (float)rnd.NextDouble() * total;
        foreach (var p in lista) { r -= p.peso; if (r <= 0) return p; }
        return lista[lista.Count - 1];
    }

    /// <summary>Hay balda debajo y nada estorbando (parantes, barandita, la balda de arriba...).</summary>
    static bool Cabe(Vector3 baseMundo, Prod p, Quaternion rot, HashSet<Collider> propios, HashSet<Collider> originales, float y)
    {
        // Apoyo: algo del propio mueble justo debajo (ignorando cajas macizas).
        bool apoyo = false;
        foreach (var h in Physics.RaycastAll(baseMundo + Vector3.up * 0.06f, Vector3.down, 0.1f, ~0, QueryTriggerInteraction.Ignore))
            if (propios.Contains(h.collider) && Mathf.Abs(h.point.y - y) <= 0.02f) { apoyo = true; break; }
        if (!apoyo) return false;

        // tam ya esta girado (yawExtra / tilt), asi que la caja va con el giro del estante.
        var medio = new Vector3(p.tam.x * 0.46f, p.tam.y * 0.48f, p.tam.z * 0.46f);
        var centro = baseMundo + Vector3.up * (p.tam.y * 0.5f + 0.006f);
        foreach (var h in Physics.OverlapBox(centro, medio, rot, ~0, QueryTriggerInteraction.Ignore))
        {
            // La caja maciza de colision del propio mueble (refrigeradora, etc.) no cuenta.
            if (originales.Contains(h) && !(h is MeshCollider) && (h.ClosestPoint(centro) - centro).sqrMagnitude < 1e-6f) continue;
            return false;
        }
        return true;
    }

    // ================================================================== crear producto

    static void Crear(Lugar l, Transform padre)
    {
        var p = l.prod;
        var go = new GameObject(p.nombre);
        go.transform.SetParent(padre, false);
        go.transform.rotation = l.giroEstante * Quaternion.Euler(0, p.yawExtra, 0) * Quaternion.Euler(p.tilt, 0, 0) * p.rot;
        go.transform.localScale = p.escala;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = p.malla;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = p.mats;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;

        // Apoyar la base sobre la balda.
        go.transform.position = Vector3.zero;
        var wb = BoundsMundo(go.transform, p.malla.bounds);
        go.transform.position = new Vector3(l.centroBase.x - wb.center.x, l.centroBase.y + 0.002f - wb.min.y, l.centroBase.z - wb.center.z);

        var box = go.AddComponent<BoxCollider>();
        box.center = p.malla.bounds.center;
        box.size = p.malla.bounds.size;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.isKinematic = true;          // quieto en el estante hasta que lo agarres
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        var grab = go.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = true;
        grab.smoothPosition = true;
        grab.smoothRotation = true;
        grab.useDynamicAttach = true;   // se agarra de donde lo tocas

        go.AddComponent<ProductoEstante>();
        go.AddComponent<CalibrarTamanoProducto>();
    }

    static Bounds BoundsMundo(Transform t, Bounds lb)
    {
        var m = t.localToWorldMatrix;
        var b = new Bounds(m.MultiplyPoint3x4(lb.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(m.MultiplyPoint3x4(new Vector3(
                (i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z)));
        return b;
    }
}
