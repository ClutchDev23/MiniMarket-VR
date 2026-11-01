using UnityEngine;

/// <summary>
/// MOSTRADOR NUEVO (se arma en el juego, encima del modelo viejo que se esconde).
///
/// Forma en "L", igual que el original (medida con SondeoMostrador):
///   A) Mueble largo del lado del cliente con FAJA de goma (se mueve cuando hay cliente),
///      rieles de acero, rodillo, separador "SIGUIENTE CLIENTE" y luz LED bajo el canto.
///   B) Escritorio del cajero: madera nogal, tablero de cuarzo blanco, zocalo de acero.
///   - ESCANER MANOS LIBRES (tipo 3nStar): solo acercas el producto con la mano.
///   - MONITOR de 27": la pantalla de la caja va DENTRO del monitor.
///   - Torre de luz "CAJA 1".
/// Los colliders nuevos reemplazan al del modelo viejo (los productos, el cajon, el POS y
/// la impresora se apoyan en el tablero nuevo).
/// </summary>
public static class MostradorNuevo
{
    // Medidas (local del mostrador, metros). Ver Logs del sondeo.
    static readonly Rect LargoXZ = new Rect(0.25f, -0.86f, 0.63f, 1.80f);    // x 0.25..0.88, z -0.86..0.94
    static readonly Rect EscritorioXZ = new Rect(-0.76f, -0.86f, 1.01f, 0.58f); // x -0.76..0.25, z -0.86..-0.28
    static readonly Rect FajaXZ = new Rect(0.33f, 0.15f, 0.42f, 0.75f);      // x 0.33..0.75, z 0.15..0.90
    public static Vector3 EscanerLocal = new Vector3(0.40f, 0f, -0.08f);
    public static Vector3 MonitorLocal = new Vector3(-0.47f, 0f, -0.74f);
    public static Vector3 PuntoCajero = new Vector3(-0.30f, 0f, 0.35f);

    static Material madera, cuarzo, acero, negro, goma, led, aceroOscuro;
    public static Transform Faja;
    static string numeroCaja = "CAJA 1";

    /// <summary>Las demas cajas de la tienda: mismo mostrador, pero cerradas (sin lector).</summary>
    public static void ConstruirOtras(Transform principal)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t != principal && t.name.StartsWith("Pref_CashCounter_Sm")) Debug.Log("[Mostrador] otra caja " + t.name + " escala=" + t.lossyScale.ToString("F2") + " pos=" + t.position.ToString("F1"));
        int n = 2;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t == principal || !t.name.StartsWith("Pref_CashCounter_Sm")) continue;
            if (t.parent != null && t.parent.name.StartsWith("Pref_CashCounter")) continue;
            numeroCaja = "CAJA " + n++;
            Construir(null, t);
        }
        numeroCaja = "CAJA 1";
    }

    public static void Construir(CajaRegistradora caja, Transform m)
    {
        if (m == null || m.Find("Mostrador nuevo") != null) return;
        // Alturas reales del modelo viejo, antes de esconderlo.
        float h = Altura(m, new Vector3(-0.1f, 0f, -0.45f), 0.90f);
        float hFaja = Altura(m, new Vector3(0.52f, 0f, 0.55f), h - 0.04f);
        if (hFaja > h - 0.01f) hFaja = h - 0.035f;

        foreach (var r in m.GetComponents<Renderer>()) r.enabled = false;
        foreach (var c in m.GetComponents<Collider>()) c.enabled = false;

        CrearMateriales();
        var raiz = new GameObject("Mostrador nuevo").transform;
        raiz.SetParent(m, false);

        // ---------------- B) escritorio del cajero
        Mueble(raiz, EscritorioXZ, h, true);
        // ---------------- A) mueble largo con faja
        Mueble(raiz, LargoXZ, hFaja - 0.006f + 0.04f, false);   // el cuerpo llega justo debajo de la faja
        // Tablero del mueble largo alrededor de la faja (la faja va hundida).
        float x0 = LargoXZ.xMin, x1 = LargoXZ.xMax, z0 = LargoXZ.yMin, z1 = LargoXZ.yMax;
        Tablero(raiz, new Rect(x0, z0, x1 - x0, FajaXZ.yMin - z0), h);                       // zona del escaner
        Tablero(raiz, new Rect(x0, FajaXZ.yMin, FajaXZ.xMin - x0, z1 - FajaXZ.yMin), h);       // canto cajero
        Tablero(raiz, new Rect(FajaXZ.xMax, FajaXZ.yMin, x1 - FajaXZ.xMax, z1 - FajaXZ.yMin), h); // canto cliente
        // Collider de la faja (mas bajo) y de los tableros.
        Caja(raiz, "Faja collider", new Vector3((FajaXZ.xMin + FajaXZ.xMax) / 2f, hFaja / 2f, (FajaXZ.yMin + FajaXZ.yMax) / 2f),
             new Vector3(FajaXZ.width, hFaja, FajaXZ.height), null, true);

        // Faja de goma con textura que corre.
        Faja = Caja(raiz, "Faja", new Vector3((FajaXZ.xMin + FajaXZ.xMax) / 2f, hFaja - 0.004f, (FajaXZ.yMin + FajaXZ.yMax) / 2f),
                    new Vector3(FajaXZ.width - 0.01f, 0.008f, FajaXZ.height - 0.02f), goma, false);
        var fm = Faja.gameObject.AddComponent<FajaMovil>();
        fm.mostrador = m; fm.zona = FajaXZ; fm.altura = hFaja; fm.activa = caja != null;
        // Rieles de acero a los lados de la faja y rodillo al final.
        foreach (var x in new[] { FajaXZ.xMin + 0.004f, FajaXZ.xMax - 0.004f })
            Caja(raiz, "Riel", new Vector3(x, h + 0.008f, (FajaXZ.yMin + FajaXZ.yMax) / 2f), new Vector3(0.012f, 0.03f, FajaXZ.height), acero, false);
        var rodillo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(rodillo.GetComponent<Collider>());
        rodillo.name = "Rodillo"; rodillo.transform.SetParent(raiz, false);
        rodillo.transform.localPosition = new Vector3((FajaXZ.xMin + FajaXZ.xMax) / 2f, hFaja - 0.006f, FajaXZ.yMin + 0.012f);
        rodillo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        rodillo.transform.localScale = new Vector3(0.025f, FajaXZ.width / 2f - 0.01f, 0.025f);
        rodillo.GetComponent<MeshRenderer>().sharedMaterial = acero;
        // Separador de clientes sobre el canto.
        Caja(raiz, "Separador", new Vector3(FajaXZ.xMax + 0.06f, h + 0.02f, 0.62f), new Vector3(0.04f, 0.035f, 0.38f), acero, false);
        if (caja != null) ObjetosCaja.Texto(raiz, "SIGUIENTE CLIENTE", new Vector3(FajaXZ.xMax + 0.039f, h + 0.02f, 0.62f), Quaternion.Euler(0f, -90f, 0f), new Vector2(360, 30), 0.001f, 24, new Color(0.1f, 0.1f, 0.12f));
        // Luz LED calida bajo el canto del lado del cliente.
        Caja(raiz, "LED", new Vector3(x1 + 0.012f, h - 0.05f, (z0 + z1) / 2f), new Vector3(0.006f, 0.008f, z1 - z0 - 0.06f), led, false);

        // ---------------- torre "CAJA 1"
        var poste = Caja(raiz, "Poste", new Vector3(x1 - 0.06f, h + 0.45f, z0 + 0.06f), new Vector3(0.035f, 0.9f, 0.035f), aceroOscuro, false);
        Caja(raiz, "Cartel CAJA 1", new Vector3(x1 - 0.06f, h + 0.98f, z0 + 0.06f), new Vector3(0.09f, 0.16f, 0.30f), led, false);
        if (m.lossyScale.x > 0f) foreach (var lado in new[] { 1f, -1f })
            ObjetosCaja.Texto(raiz, numeroCaja, new Vector3(x1 - 0.06f + 0.047f * lado, h + 0.98f, z0 + 0.06f), Quaternion.Euler(0f, lado > 0 ? -90f : 90f, 0f),
                new Vector2(280, 120), 0.001f, 72, new Color(0.15f, 0.08f, 0.02f));

        // ---------------- escaner manos libres
        Escaner(caja, m, raiz, h);
        // ---------------- monitor con la pantalla de la caja adentro
        Monitor(caja, m, raiz, h);

        if (caja == null) { Debug.Log("[Mostrador] " + numeroCaja + " (cerrada) lista."); return; }
        // La pistola (ya no hace falta, pero queda a mano) sobre el escritorio.
        foreach (var l in Object.FindObjectsByType<LectorCodigos>(FindObjectsSortMode.None))
            if (!l.manosLibres && l.grab != null)
            {
                var g = l.grab.transform;
                g.position = m.TransformPoint(new Vector3(0.16f, h + 0.06f, -0.60f));
                g.rotation = m.rotation * Quaternion.Euler(0f, 90f, 90f);
                var rb = g.GetComponent<Rigidbody>();
                if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }

        Debug.Log("[Mostrador] Nuevo mostrador listo (tablero a " + h.ToString("0.00") + " m, faja a " + hFaja.ToString("0.00") + " m).");
    }

    static float Altura(Transform m, Vector3 local, float porDefecto)
    {
        var p = m.TransformPoint(local);
        float mejor = -1f;
        foreach (var hit in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.transform.IsChildOf(m)) mejor = Mathf.Max(mejor, hit.point.y - m.position.y);
        return mejor > 0.3f && mejor < 1.2f ? mejor : porDefecto;
    }

    // Mueble: cuerpo de madera, zocalo de acero hundido y (si se pide) tablero completo.
    static void Mueble(Transform raiz, Rect r, float h, bool conTablero)
    {
        Vector3 c = new Vector3(r.center.x, 0f, r.center.y);
        Caja(raiz, "Zocalo", c + Vector3.up * 0.05f, new Vector3(r.width - 0.06f, 0.10f, r.height - 0.06f), aceroOscuro, false);
        Caja(raiz, "Cuerpo", c + Vector3.up * (0.10f + (h - 0.14f) / 2f), new Vector3(r.width - 0.01f, h - 0.14f, r.height - 0.01f), madera, false);
        // Collider de todo el mueble.
        Caja(raiz, "Collider", c + Vector3.up * (h / 2f), new Vector3(r.width, h, r.height), null, true);
        if (conTablero) Tablero(raiz, r, h);
    }

    // Tablero de cuarzo con canto de acero y un poco de vuelo.
    static void Tablero(Transform raiz, Rect r, float h)
    {
        if (r.width <= 0.005f || r.height <= 0.005f) return;
        Vector3 c = new Vector3(r.center.x, 0f, r.center.y);
        // Relleno de madera desde el cuerpo del mueble hasta el tablero (en el mueble largo).
        Caja(raiz, "Relleno", c + Vector3.up * (h - 0.10f), new Vector3(r.width - 0.01f, 0.13f, r.height - 0.01f), madera, false);
        Caja(raiz, "Canto", c + Vector3.up * (h - 0.032f), new Vector3(r.width + 0.03f, 0.012f, r.height + 0.03f), acero, false);
        Caja(raiz, "Tablero", c + Vector3.up * (h - 0.013f), new Vector3(r.width + 0.03f, 0.026f, r.height + 0.03f), cuarzo, false);
        Caja(raiz, "Tablero collider", c + Vector3.up * (h - 0.013f), new Vector3(r.width, 0.026f, r.height), null, true);
    }

    static Transform Caja(Transform padre, string n, Vector3 pos, Vector3 tam, Material mat, bool soloCollider)
    {
        var go = soloCollider ? new GameObject(n) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        if (soloCollider) { var bc = go.AddComponent<BoxCollider>(); bc.size = tam; }
        else
        {
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.localScale = tam;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
        }
        return go.transform;
    }

    // ------------------------------------------------------------------ escaner
    static void Escaner(CajaRegistradora caja, Transform m, Transform raiz, float h)
    {
        var e = new GameObject("Escaner manos libres").transform;
        e.SetParent(raiz, false);
        e.localPosition = EscanerLocal + Vector3.up * h;
        var hacia = PuntoCajero - EscanerLocal; hacia.y = 0f;
        e.localRotation = Quaternion.LookRotation(hacia.normalized, Vector3.up);   // +Z mira al cajero

        var cuerpoMat = ObjetosCaja.Mat(new Color(0.22f, 0.23f, 0.25f), 0.45f);
        var vidrio = ObjetosCaja.Mat(new Color(0.02f, 0.02f, 0.03f), 0.97f);
        var amarillo = ObjetosCaja.Mat(new Color(0.98f, 0.82f, 0.10f), 0.3f);
        var blancoLuz = ObjetosCaja.Mat(new Color(1f, 1f, 1f), 0.5f, true);
        var rojo = ObjetosCaja.Mat(new Color(1f, 0.1f, 0.05f), 0.5f, true);

        Caja(e, "Base", new Vector3(0f, 0.006f, -0.01f), new Vector3(0.11f, 0.012f, 0.10f), cuerpoMat, false);
        var cuerpo = new GameObject("Cabeza").transform;
        cuerpo.SetParent(e, false);
        cuerpo.localPosition = new Vector3(0f, 0.085f, 0f);
        cuerpo.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        Caja(cuerpo, "Carcasa", Vector3.zero, new Vector3(0.135f, 0.145f, 0.075f), cuerpoMat, false);
        Caja(cuerpo, "Vidrio", new Vector3(0f, 0.004f, 0.038f), new Vector3(0.115f, 0.12f, 0.004f), vidrio, false);
        for (int i = 0; i < 4; i++)
            Caja(cuerpo, "LED", new Vector3(-0.036f + i * 0.024f, 0.05f, 0.041f), new Vector3(0.012f, 0.006f, 0.002f), blancoLuz, false);
        Caja(cuerpo, "Sensor", new Vector3(0f, 0.005f, 0.04f), new Vector3(0.012f, 0.012f, 0.002f), cuerpoMat, false);
        Caja(cuerpo, "Etiqueta", new Vector3(0f, -0.043f, 0.041f), new Vector3(0.10f, 0.016f, 0.002f), amarillo, false);
        if (m.lossyScale.x > 0f) ObjetosCaja.Texto(cuerpo, "WARNING  LED 1M", new Vector3(0f, -0.043f, 0.0425f), Quaternion.Euler(0f, 180f, 0f), new Vector2(200, 30), 0.0005f, 18, Color.black);
        var luzRoja = Caja(cuerpo, "Luz de lectura", new Vector3(0f, 0.004f, 0.0415f), new Vector3(0.09f, 0.003f, 0.001f), rojo, false);
        luzRoja.gameObject.SetActive(false);

        // Zona de lectura: delante del vidrio.
        var zona = new GameObject("Zona de lectura").transform;
        zona.SetParent(cuerpo, false);
        zona.localPosition = new Vector3(0f, 0f, 0.11f);
        var bc = zona.gameObject.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.16f, 0.16f, 0.14f);

        var lector = zona.gameObject.AddComponent<LectorCodigos>();
        lector.manosLibres = true;
        lector.distanciaLectura = 0.01f;
        lector.lectorCollider = bc;
        lector.audioLector = zona.gameObject.AddComponent<AudioSource>();
        lector.audioLector.spatialBlend = 1f; lector.audioLector.playOnAwake = false;
        foreach (var l in Object.FindObjectsByType<LectorCodigos>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l != lector && l.clipBip != null) { lector.clipBip = l.clipBip; break; }
        zona.gameObject.AddComponent<DestelloEscaner>().luz = luzRoja.gameObject;
        if (caja != null) caja.AgregarLector(lector); else lector.enabled = false;
    }

    // ------------------------------------------------------------------ monitor
    static void Monitor(CajaRegistradora caja, Transform m, Transform raiz, float h)
    {
        var mo = new GameObject("Monitor 27").transform;
        mo.SetParent(raiz, false);
        mo.localPosition = MonitorLocal + Vector3.up * h;
        var hacia = PuntoCajero - MonitorLocal; hacia.y = 0f;
        mo.localRotation = Quaternion.LookRotation(-hacia.normalized, Vector3.up);   // +Z = la espalda del monitor

        var plast = ObjetosCaja.Mat(new Color(0.07f, 0.07f, 0.08f), 0.55f);
        var pantallaNegra = ObjetosCaja.Mat(new Color(0.01f, 0.01f, 0.012f), 0.95f);
        const float ancho = 0.62f, alto = 0.37f, centroY = 0.33f;

        var pie = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(pie.GetComponent<Collider>());
        pie.name = "Pie"; pie.transform.SetParent(mo, false);
        pie.transform.localPosition = new Vector3(0f, 0.005f, 0.06f);
        pie.transform.localScale = new Vector3(0.22f, 0.005f, 0.17f);
        pie.GetComponent<MeshRenderer>().sharedMaterial = acero;
        Caja(mo, "Brazo", new Vector3(0f, 0.15f, 0.08f), new Vector3(0.05f, 0.30f, 0.025f), acero, false);
        Caja(mo, "Espalda", new Vector3(0f, centroY, 0.035f), new Vector3(ancho - 0.06f, alto - 0.06f, 0.04f), plast, false);
        Caja(mo, "Marco", new Vector3(0f, centroY, 0f), new Vector3(ancho, alto, 0.022f), plast, false);
        Caja(mo, "Panel", new Vector3(0f, centroY, -0.0115f), new Vector3(ancho - 0.02f, alto - 0.03f, 0.002f), pantallaNegra, false);
        var logo = Caja(mo, "Barbilla", new Vector3(0f, centroY - alto / 2f + 0.009f, -0.0115f), new Vector3(0.05f, 0.004f, 0.001f), acero, false);

        // La pantalla de la caja (canvas) se mete en el panel.
        var centro = mo.TransformPoint(new Vector3(0f, centroY, -0.0135f));
        if (caja != null) caja.ColocarPantallaEn(centro, mo.rotation, ancho - 0.03f);
        else
        {
            // Caja sin cajero: el monitor muestra que esta cerrada.
            if (m.lossyScale.x > 0f) ObjetosCaja.Texto(mo, "<color=#F28C28>" + numeroCaja + "</color>\n<size=40>CERRADA · pase a la CAJA 1</size>", new Vector3(0f, centroY, -0.013f),
                Quaternion.identity, new Vector2(560, 300), 0.001f, 70, Color.white);
        }
    }

    // ------------------------------------------------------------------ materiales
    static void CrearMateriales()
    {
        if (madera != null) return;
        madera = ConTextura(new Color(1f, 1f, 1f), 0.35f, Nogal(), new Vector2(2f, 1f));
        cuarzo = ConTextura(new Color(1f, 0.98f, 0.95f), 0.45f, Cuarzo(), new Vector2(2f, 2f));
        acero = ObjetosCaja.Mat(new Color(0.78f, 0.79f, 0.81f), 0.88f);
        if (acero.HasProperty("_Metallic")) acero.SetFloat("_Metallic", 0.9f);
        aceroOscuro = ObjetosCaja.Mat(new Color(0.25f, 0.26f, 0.28f), 0.7f);
        if (aceroOscuro.HasProperty("_Metallic")) aceroOscuro.SetFloat("_Metallic", 0.8f);
        negro = ObjetosCaja.Mat(new Color(0.05f, 0.05f, 0.05f), 0.4f);
        goma = ConTextura(Color.white, 0.25f, Goma(), new Vector2(1f, 6f));
        led = ObjetosCaja.Mat(new Color(1f, 0.78f, 0.45f), 0.3f, true);
        if (led.HasProperty("_EmissionColor")) led.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.38f) * 1.6f);
    }

    static Material ConTextura(Color c, float brillo, Texture2D t, Vector2 rep)
    {
        var m = ObjetosCaja.Mat(c, brillo);
        m.mainTexture = t;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
        m.mainTextureScale = rep;
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", rep);
        return m;
    }

    static Texture2D Nogal()
    {
        int n = 256; var t = new Texture2D(n, n, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
        var a = new Color(0.30f, 0.18f, 0.10f); var b = new Color(0.48f, 0.31f, 0.18f);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = x / (float)n, v = y / (float)n;
            float veta = Mathf.Sin((v * 18f + Mathf.PerlinNoise(u * 3f, v * 6f) * 4f) * Mathf.PI);
            float g = Mathf.Clamp01(0.5f + 0.35f * veta + 0.25f * (Mathf.PerlinNoise(u * 40f, v * 4f) - 0.5f));
            t.SetPixel(x, y, Color.Lerp(a, b, g));
        }
        t.Apply(); return t;
    }

    static Texture2D Cuarzo()
    {
        int n = 256; var t = new Texture2D(n, n, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
        var rnd = new System.Random(3);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = x / (float)n, v = y / (float)n;
            float vena = Mathf.Abs(Mathf.Sin((u + v * 0.6f + Mathf.PerlinNoise(u * 4f, v * 4f) * 0.5f) * 9f));
            float c = 0.93f - (vena < 0.04f ? 0.07f : 0f) - (rnd.NextDouble() < 0.015 ? 0.25f : 0f) + (float)(rnd.NextDouble() * 0.02);
            t.SetPixel(x, y, new Color(c, c, c * 1.01f));
        }
        t.Apply(); return t;
    }

    static Texture2D Goma()
    {
        int n = 128; var t = new Texture2D(n, n, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float raya = (y % 16) < 2 ? 0.03f : 0.09f;
            float c = raya + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.03f;
            t.SetPixel(x, y, new Color(c, c, c));
        }
        t.Apply(); return t;
    }
}

/// <summary>
/// La faja corre (textura) cuando hay un cliente en la caja y LLEVA los productos que
/// estan encima hacia el escaner (no mueve los que tienes agarrados).
/// </summary>
public class FajaMovil : MonoBehaviour
{
    public Transform mostrador; public Rect zona; public float altura; public bool activa;
    const float Velocidad = 0.12f;
    Material m; float off; Transform productos;
    readonly Collider[] buf = new Collider[48];
    void Start() { var r = GetComponent<Renderer>(); if (r != null) m = r.material; }
    void Update()
    {
        if (m == null || !activa) return;
        bool corre = GestorClientes.I != null && GestorClientes.I.ClienteEnCaja != null;
        if (!corre) return;
        off -= Time.deltaTime * Velocidad * 4f;
        if (m.HasProperty("_BaseMap")) m.SetTextureOffset("_BaseMap", new Vector2(0f, off));
        m.mainTextureOffset = new Vector2(0f, off);
        if (mostrador == null) return;
        if (productos == null) { var p = GameObject.Find("Productos"); if (p == null) return; productos = p.transform; }
        var centro = mostrador.TransformPoint(new Vector3(zona.center.x, altura + 0.08f, zona.center.y));
        int n = Physics.OverlapBoxNonAlloc(centro, new Vector3(zona.width / 2f, 0.09f, zona.height / 2f), buf, mostrador.rotation, ~0, QueryTriggerInteraction.Ignore);
        float fin = zona.yMin + 0.07f;
        var movidos = new System.Collections.Generic.HashSet<Transform>();
        for (int i = 0; i < n; i++)
        {
            var c = buf[i];
            if (c == null || !c.transform.IsChildOf(productos)) continue;
            var grab = c.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null && grab.isSelected) continue;
            var t = grab != null ? grab.transform : c.transform;
            if (!movidos.Add(t)) continue;
            if (mostrador.InverseTransformPoint(t.position).z <= fin) continue;   // ya llego al final
            var paso = -mostrador.forward * Velocidad * Time.deltaTime;
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) rb.MovePosition(rb.position + paso); else t.position += paso;
        }
    }
}

/// <summary>Luz roja del escaner: se enciende un instante al leer.</summary>
public class DestelloEscaner : MonoBehaviour
{
    public GameObject luz;
    LectorCodigos l; float hasta;
    void Start() { l = GetComponent<LectorCodigos>(); if (l != null) l.ProductoEscaneado += (g, p) => hasta = Time.time + 0.25f; }
    void Update() { if (luz != null) luz.SetActive(Time.time < hasta || Mathf.Repeat(Time.time, 2f) < 0.05f); }
}
