using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Objetos 3D de la caja (se crean solos junto a la CajaRegistradora):
///
///   - BANDEJA DE DINERO: el cliente deja ahi sus billetes y monedas (de practica) y el
///     cajero deja el vuelto. Se pueden agarrar.
///   - POS: la maquinita de tarjetas, con su pantallita que muestra el monto y el
///     estado. El cliente acerca su tarjeta.
///   - IMPRESORA: al terminar cada venta imprime una BOLETA DE PRACTICA que se puede
///     agarrar y entregar.
///
/// Los billetes y monedas son de PRACTICA: llevan el valor en soles y "Cajero XR",
/// pero no copian el diseno del dinero real.
/// </summary>
public static class ObjetosCaja
{
    // Posiciones relativas al mostrador (Pref_CashCounter). Se ajustan mirando la escena.
    public static Vector3 BandejaLocal = new Vector3(0.55f, 0f, 0.05f);
    public static Vector3 PosLocal = new Vector3(0.05f, 0f, -0.30f);
    public static Vector3 ImpresoraLocal = new Vector3(-0.15f, 0f, -0.55f);

    /// <summary>Crea la bandeja, el POS y la impresora junto a esta caja.</summary>
    public static void Instalar(CajaRegistradora caja, Transform mostrador)
    {
        if (caja == null || mostrador == null) return;
        var raiz = new GameObject("Objetos de la caja").transform;
        raiz.SetParent(caja.transform, false);

        var b = new GameObject("Bandeja de dinero").AddComponent<BandejaDinero>();
        b.transform.SetParent(raiz, false);
        b.Iniciar(caja, mostrador);

        var p = new GameObject("POS").AddComponent<POS3D>();
        p.transform.SetParent(raiz, false);
        p.Iniciar(caja, mostrador);

        var i = new GameObject("Impresora de boletas").AddComponent<ImpresoraBoletas>();
        i.transform.SetParent(raiz, false);
        i.Iniciar(caja, mostrador);
    }

    // ================================================================== utilidades

    /// <summary>Punto sobre la superficie del mostrador (raycast hacia abajo).</summary>
    public static Vector3 SobreMostrador(Transform mostrador, Vector3 local)
    {
        var p = mostrador.TransformPoint(new Vector3(local.x, 0f, local.z));
        float y = mostrador.position.y + 0.95f;
        var hits = Physics.RaycastAll(p + Vector3.up * 2.5f, Vector3.down, 3.5f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MinValue;
        foreach (var h in hits)
        {
            if (!h.collider.transform.IsChildOf(mostrador)) continue;
            if (h.point.y > mejor) mejor = h.point.y;
        }
        if (mejor > float.MinValue) y = mejor;
        p.y = y;
        return p;
    }

    public static Material Mat(Color c, float brillo = 0.3f, bool emisivo = false)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", brillo);
        if (emisivo && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.6f);
        }
        return m;
    }

    public static GameObject Caja(string nombre, Transform padre, Vector3 tam, Material mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        if (!collider) Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localScale = tam;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    /// <summary>Texto 3D (canvas en el mundo) pegado a una cara.</summary>
    public static Text Texto(Transform padre, string texto, Vector3 posLocal, Quaternion rotLocal, Vector2 tamPx,
                             float metrosPorPx, int fuente, Color color, TextAnchor ancla = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(padre, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = tamPx;
        rt.localPosition = posLocal;
        rt.localRotation = rotLocal;
        // El padre puede estar escalado: se compensa para que el texto mida lo pedido.
        var s = padre.lossyScale;
        rt.localScale = new Vector3(metrosPorPx / Mathf.Max(1e-5f, s.x), metrosPorPx / Mathf.Max(1e-5f, s.y), metrosPorPx / Mathf.Max(1e-5f, s.z));
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 6f;

        var tgo = new GameObject("T", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(go.transform, false);
        var t = tgo.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = texto;
        t.fontSize = fuente;
        t.fontStyle = FontStyle.Bold;
        t.alignment = ancla;
        t.color = color;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return t;
    }

    /// <summary>Hace agarrable un objeto (fisica + XR Grab).</summary>
    public static XRGrabInteractable Agarrable(GameObject go, float masa)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = masa;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var g = go.AddComponent<XRGrabInteractable>();
        g.movementType = XRBaseInteractable.MovementType.Kinematic;
        g.throwOnDetach = true;
        g.useDynamicAttach = true;
        g.smoothPosition = true;
        g.smoothRotation = true;
        // Quieto donde lo dejan (bandeja, mostrador) hasta que alguien lo agarra; al
        // soltarlo cae como un objeto normal. Asi no se resbala ni atraviesa la bandeja.
        rb.isKinematic = true;
        g.selectExited.AddListener(_ => { if (rb != null) { rb.isKinematic = false; rb.useGravity = true; rb.WakeUp(); } });
        return g;
    }

    public static string Soles(int centimos) =>
        "S/ " + (centimos / 100f).ToString("0.00", CultureInfo.InvariantCulture);

    public static IEnumerator Desaparecer(GameObject go, float dur)
    {
        if (go == null) yield break;
        var ini = go.transform.localScale;
        float t = 0f;
        while (t < dur && go != null)
        {
            t += Time.deltaTime;
            go.transform.localScale = ini * Mathf.Lerp(1f, 0.01f, t / dur);
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }
}

// ====================================================================== BANDEJA DE DINERO

public class BandejaDinero : MonoBehaviour
{
    public static BandejaDinero I { get; private set; }

    CajaRegistradora caja;
    Transform mostrador;
    Vector3 centro;
    readonly List<GameObject> pago = new List<GameObject>();
    readonly List<GameObject> vuelto = new List<GameObject>();
    int vueltoMostrado;
    AudioSource sfx;

    static readonly Dictionary<int, Material> mats = new Dictionary<int, Material>();

    public void Iniciar(CajaRegistradora c, Transform m)
    {
        I = this;
        caja = c;
        mostrador = m;
        centro = ObjetosCaja.SobreMostrador(m, ObjetosCaja.BandejaLocal);

        // La bandeja (un platito negro con borde).
        transform.position = centro;
        transform.rotation = Quaternion.Euler(0f, m.eulerAngles.y, 0f);
        var negro = ObjetosCaja.Mat(new Color(0.08f, 0.08f, 0.09f), 0.5f);
        ObjetosCaja.Caja("Base", transform, new Vector3(0.30f, 0.008f, 0.22f), negro).transform.localPosition = new Vector3(0, 0.004f, 0);
        ObjetosCaja.Caja("Borde1", transform, new Vector3(0.30f, 0.02f, 0.008f), negro).transform.localPosition = new Vector3(0, 0.01f, 0.11f);
        ObjetosCaja.Caja("Borde2", transform, new Vector3(0.30f, 0.02f, 0.008f), negro).transform.localPosition = new Vector3(0, 0.01f, -0.11f);

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 1f; sfx.minDistance = 0.6f; sfx.maxDistance = 6f; sfx.playOnAwake = false;

        caja.VentaCobrada += AlCobrar;
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        if (caja != null) caja.VentaCobrada -= AlCobrar;
    }

    /// <summary>El cliente deja su dinero en la bandeja.</summary>
    public void ClienteDeja(int centimos)
    {
        Limpiar(pago);
        int i = 0;
        foreach (var d in CajaRegistradora.DesgloseLista(centimos))
        {
            if (i >= 12) break;
            pago.Add(Crear(d, i, true));
            i++;
        }
        if (sfx != null) sfx.PlayOneShot(centimos >= 1000 ? SonidosProcedurales.Billete() : SonidosProcedurales.Moneda());
    }

    /// <summary>El cliente se fue sin pagar: se lleva su dinero.</summary>
    public void RetirarPago()
    {
        foreach (var g in pago) if (g != null) StartCoroutine(ObjetosCaja.Desaparecer(g, 0.3f));
        pago.Clear();
    }

    /// <summary>El cliente recoge su vuelto (y la bandeja queda vacia).</summary>
    public void ClienteRecoge()
    {
        foreach (var g in vuelto) if (g != null) StartCoroutine(ObjetosCaja.Desaparecer(g, 0.3f));
        vuelto.Clear();
        vueltoMostrado = 0;
    }

    void AlCobrar(CajaRegistradora.Venta v)
    {
        // El pago entra al cajon.
        foreach (var g in pago) if (g != null) StartCoroutine(ObjetosCaja.Desaparecer(g, 0.35f));
        pago.Clear();
    }

    void Update()
    {
        if (caja == null) return;
        // El vuelto que va marcando el cajero aparece en la bandeja.
        int entregado = caja.PasoActual == CajaRegistradora.Paso.EntregarVuelto || caja.PasoActual == CajaRegistradora.Paso.Completada
            ? caja.EntregadoCent : 0;
        if (caja.PasoActual == CajaRegistradora.Paso.Completada && caja.UltimaVenta != null)
            entregado = caja.UltimaVenta.vueltoCent;
        if (entregado == vueltoMostrado) return;
        if (entregado < vueltoMostrado)
        {
            Limpiar(vuelto);
            vueltoMostrado = 0;
        }
        int falta = entregado - vueltoMostrado;
        foreach (var d in CajaRegistradora.DesgloseLista(falta))
            if (vuelto.Count < 14) vuelto.Add(Crear(d, vuelto.Count, false));
        vueltoMostrado = entregado;
        Debug.Log("[Bandeja] Vuelto en la bandeja: " + ObjetosCaja.Soles(entregado) + " (" + vuelto.Count + " piezas) en " + transform.position);
    }

    void Limpiar(List<GameObject> l)
    {
        foreach (var g in l) if (g != null) Destroy(g);
        l.Clear();
    }

    GameObject Crear(int centimos, int indice, bool delCliente)
    {
        bool billete = centimos >= 1000;
        // El pago del cliente va a un lado de la bandeja y el vuelto al otro.
        float lado = delCliente ? 0.07f : -0.07f;
        var local = new Vector3(lado + (indice % 3 - 1) * 0.025f, 0.012f + indice * 0.004f, (indice % 2 == 0 ? 1 : -1) * 0.02f * (indice / 2 % 3));
        var go = billete ? Billete3D(centimos) : Moneda3D(centimos);
        go.transform.position = transform.TransformPoint(local);
        go.transform.rotation = transform.rotation * Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f);
        return go;
    }

    // Colores parecidos a los soles de verdad: S/10 verde, S/20 naranja, S/50 rosado,
    // S/100 azul, S/200 morado. Monedas: S/5 y S/2 bimetalicas (centro dorado, aro
    // plateado), S/1 plateada y los centimos dorados.
    static Color ColorDe(int c)
    {
        switch (c)
        {
            case 20000: return new Color(0.66f, 0.45f, 0.72f);   // morado
            case 10000: return new Color(0.30f, 0.52f, 0.85f);   // azul
            case 5000: return new Color(0.88f, 0.45f, 0.55f);    // rosado
            case 2000: return new Color(0.90f, 0.58f, 0.30f);    // naranja
            case 1000: return new Color(0.35f, 0.70f, 0.45f);    // verde
            case 500: case 200: case 100: return new Color(0.80f, 0.80f, 0.82f);   // plateada (aro)
            default: return new Color(0.84f, 0.68f, 0.32f);      // dorada (centimos)
        }
    }

    static Material dorado;
    static Material Dorado()
    {
        if (dorado == null) dorado = ObjetosCaja.Mat(new Color(0.86f, 0.70f, 0.30f), 0.85f);
        return dorado;
    }

    static Material MatDe(int c)
    {
        if (!mats.TryGetValue(c, out var m) || m == null)
            mats[c] = m = ObjetosCaja.Mat(ColorDe(c), c < 1000 ? 0.85f : 0.15f);
        return m;
    }

    static Material MatFranja(int c)
    {
        if (!mats.TryGetValue(-c, out var m) || m == null)
            mats[-c] = m = ObjetosCaja.Mat(Color.Lerp(ColorDe(c), Color.white, 0.65f), 0.15f);
        return m;
    }

    // Imagenes del BCRP (anverso/reverso de billetes y cara de las monedas), con la marca
    // "MUESTRA" ya puesta en la imagen. Estan en Resources/Soles. Si faltan, se usa el
    // billete de practica de colores.
    static readonly Dictionary<string, Material> matsImagen = new Dictionary<string, Material>();
    static Material MatImagen(string nombre, float brillo)
    {
        if (matsImagen.TryGetValue(nombre, out var m)) return m;
        var tex = Resources.Load<Texture2D>("Soles/" + nombre);
        if (tex != null)
        {
            m = ObjetosCaja.Mat(Color.white, brillo);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        }
        matsImagen[nombre] = m;
        return m;
    }

    static void Cara(Transform padre, Material mat, bool arriba)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = arriba ? "Anverso" : "Reverso";
        Object.Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(padre, false);
        q.transform.localPosition = new Vector3(0f, arriba ? 0.00078f : -0.00078f, 0f);
        q.transform.localRotation = Quaternion.Euler(arriba ? 90f : -90f, 0f, 0f);
        q.transform.localScale = new Vector3(0.14f, 0.066f, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public static GameObject Billete3D(int centimos)
    {
        var go = new GameObject("Billete S/ " + centimos / 100);
        var cuerpo = ObjetosCaja.Caja("Papel", go.transform, new Vector3(0.14f, 0.0015f, 0.066f), MatDe(centimos), false);
        var anverso = MatImagen("b" + centimos / 100 + "a", 0.1f);
        var reverso = MatImagen("b" + centimos / 100 + "r", 0.1f);
        if (anverso != null && reverso != null)
        {
            Cara(go.transform, anverso, true);
            Cara(go.transform, reverso, false);
            var c2 = go.AddComponent<BoxCollider>();
            c2.size = new Vector3(0.14f, 0.012f, 0.066f);
            ObjetosCaja.Agarrable(go, 0.01f);
            return go;
        }
        // Franja clara a un lado (como la zona de la marca de agua de un billete real).
        ObjetosCaja.Caja("Franja", go.transform, new Vector3(0.032f, 0.0016f, 0.062f), MatFranja(centimos), false)
            .transform.localPosition = new Vector3(-0.05f, 0f, 0f);
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.14f, 0.012f, 0.066f);   // un poco mas grueso: facil de agarrar
        // Cara de arriba: valor grande + marca.
        ObjetosCaja.Texto(go.transform, "<size=60>S/ " + (centimos / 100) + "</size>\n<size=18>CAJERO XR · PRACTICA</size>",
            new Vector3(0f, 0.0009f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector2(280, 130), 0.0005f, 30, new Color(1f, 1f, 1f, 0.95f));
        ObjetosCaja.Agarrable(go, 0.01f);
        return go;
    }

    public static GameObject Moneda3D(int centimos)
    {
        var go = new GameObject("Moneda " + ObjetosCaja.Soles(centimos));
        // Diametros reales: S/5 24.4 mm, S/2 22.3 mm, S/1 25.5 mm, 50c 22 mm, 20c 21 mm, 10c 20.5 mm.
        float d = centimos >= 500 ? 0.0244f : centimos >= 200 ? 0.0223f : centimos >= 100 ? 0.0255f
                : centimos >= 50 ? 0.022f : centimos >= 20 ? 0.021f : 0.0205f;
        var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disco.GetComponent<Collider>());
        disco.transform.SetParent(go.transform, false);
        disco.transform.localScale = new Vector3(d, 0.001f, d);
        var r = disco.GetComponent<MeshRenderer>();
        r.sharedMaterial = MatDe(centimos);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Con la imagen del BCRP: la tapa del cilindro usa toda la imagen (la moneda llena el cuadro).
        var img = MatImagen("m" + centimos, 0.75f);
        if (img != null)
        {
            r.sharedMaterial = img;
            disco.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // la imagen queda derecha
            var c3 = go.AddComponent<BoxCollider>();
            c3.size = new Vector3(0.04f, 0.01f, 0.04f);
            ObjetosCaja.Agarrable(go, 0.008f);
            return go;
        }
        if (centimos == 500 || centimos == 200)
        {
            // Bimetalica: centro dorado un poquito mas alto que el aro.
            var centro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(centro.GetComponent<Collider>());
            centro.transform.SetParent(go.transform, false);
            centro.transform.localScale = new Vector3(d * 0.68f, 0.00105f, d * 0.68f);
            var rc = centro.GetComponent<MeshRenderer>();
            rc.sharedMaterial = Dorado();
            rc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.04f, 0.01f, 0.04f);     // mas grande que la moneda: se agarra facil
        string v = centimos >= 100 ? (centimos / 100).ToString() : (centimos / 100f).ToString("0.00", CultureInfo.InvariantCulture);
        ObjetosCaja.Texto(go.transform, v, new Vector3(0f, 0.0012f, 0f), Quaternion.Euler(90f, 0f, 0f),
            new Vector2(80, 80), 0.00025f, 34, new Color(0.15f, 0.12f, 0.05f));
        ObjetosCaja.Agarrable(go, 0.008f);
        return go;
    }
}

// ====================================================================== POS

public class POS3D : MonoBehaviour
{
    public static POS3D I { get; private set; }

    CajaRegistradora caja;
    Text pantalla;
    AudioSource sfx;
    float proximo;

    public void Iniciar(CajaRegistradora c, Transform m)
    {
        I = this;
        caja = c;
        transform.position = ObjetosCaja.SobreMostrador(m, ObjetosCaja.PosLocal);
        // Mira hacia el lado del cliente (+x del mostrador).
        transform.rotation = Quaternion.LookRotation(m.right, Vector3.up);

        var cuerpo = ObjetosCaja.Mat(new Color(0.12f, 0.12f, 0.14f), 0.6f);
        var gris = ObjetosCaja.Mat(new Color(0.30f, 0.31f, 0.34f), 0.4f);
        var verde = ObjetosCaja.Mat(new Color(0.55f, 0.75f, 0.55f), 0.2f, true);

        var b = ObjetosCaja.Caja("Cuerpo", transform, new Vector3(0.08f, 0.035f, 0.17f), cuerpo);
        b.transform.localPosition = new Vector3(0f, 0.0175f, 0f);
        b.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        var pant = ObjetosCaja.Caja("Pantallita", b.transform, new Vector3(0.8f, 0.05f, 0.32f), verde, false);
        pant.transform.localPosition = new Vector3(0f, 0.52f, 0.22f);
        // Teclado: 3 x 4 botoncitos.
        for (int i = 0; i < 12; i++)
        {
            var k = ObjetosCaja.Caja("Tecla", b.transform, new Vector3(0.18f, 0.1f, 0.07f), gris, false);
            k.transform.localPosition = new Vector3(-0.25f + (i % 3) * 0.25f, 0.52f, -0.06f - (i / 3) * 0.1f);
        }
        // Papelito del voucher.
        var papel = ObjetosCaja.Caja("Ranura", b.transform, new Vector3(0.7f, 0.12f, 0.03f), ObjetosCaja.Mat(Color.white, 0.1f), false);
        papel.transform.localPosition = new Vector3(0f, 0.5f, 0.49f);

        pantalla = ObjetosCaja.Texto(pant.transform, "", new Vector3(0f, 0.6f, 0f), Quaternion.Euler(90f, 0f, 0f),
            new Vector2(220, 90), 0.0003f, 20, new Color(0.05f, 0.15f, 0.05f));

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 1f; sfx.minDistance = 0.5f; sfx.maxDistance = 5f; sfx.playOnAwake = false;
    }

    void OnDestroy() { if (I == this) I = null; }

    void Update()
    {
        if (caja == null || pantalla == null || Time.time < proximo) return;
        proximo = Time.time + 0.1f;
        pantalla.text = caja.TextoPOS;
    }

    /// <summary>El cliente acerca su tarjeta: una tarjeta azul va desde su mano hasta el POS.</summary>
    public void AnimarTarjeta(Vector3 desde)
    {
        StartCoroutine(RutinaTarjeta(desde));
    }

    IEnumerator RutinaTarjeta(Vector3 desde)
    {
        var t = ObjetosCaja.Caja("Tarjeta", null, new Vector3(0.0856f, 0.0008f, 0.054f), ObjetosCaja.Mat(new Color(0.12f, 0.30f, 0.70f), 0.7f), false);
        ObjetosCaja.Texto(t.transform, "CAJERO XR\n<size=14>**** 2026</size>", new Vector3(0f, 0.6f, 0f), Quaternion.Euler(90f, 0f, 0f),
            new Vector2(170, 100), 0.0004f, 22, Color.white);
        var arriba = transform.position + Vector3.up * 0.06f;
        float dur = 0.7f, k = 0f;
        while (k < 1f)
        {
            k += Time.deltaTime / dur;
            t.transform.position = Vector3.Lerp(desde, arriba, Mathf.SmoothStep(0f, 1f, k));
            t.transform.rotation = Quaternion.Slerp(Quaternion.Euler(0f, transform.eulerAngles.y, 60f), transform.rotation, k);
            yield return null;
        }
        if (sfx != null) sfx.PlayOneShot(SonidosProcedurales.PitidoPOS());
        yield return new WaitForSeconds(1.4f);
        k = 0f;
        while (k < 1f)
        {
            k += Time.deltaTime / dur;
            t.transform.position = Vector3.Lerp(arriba, desde, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        Destroy(t);
    }
}

// ====================================================================== IMPRESORA + BOLETA

public class ImpresoraBoletas : MonoBehaviour
{
    public static ImpresoraBoletas I { get; private set; }

    CajaRegistradora caja;
    Transform salida;
    readonly List<GameObject> boletas = new List<GameObject>();
    static int numeroBoleta = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { numeroBoleta = 1; }

    public void Iniciar(CajaRegistradora c, Transform m)
    {
        I = this;
        caja = c;
        transform.position = ObjetosCaja.SobreMostrador(m, ObjetosCaja.ImpresoraLocal);
        transform.rotation = Quaternion.LookRotation(m.right, Vector3.up);

        var negro = ObjetosCaja.Mat(new Color(0.15f, 0.15f, 0.17f), 0.5f);
        var cuerpo = ObjetosCaja.Caja("Cuerpo", transform, new Vector3(0.12f, 0.10f, 0.16f), negro);
        cuerpo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        var tapa = ObjetosCaja.Caja("Tapa", transform, new Vector3(0.115f, 0.01f, 0.08f), ObjetosCaja.Mat(new Color(0.25f, 0.25f, 0.28f), 0.6f), false);
        tapa.transform.localPosition = new Vector3(0f, 0.104f, -0.03f);
        var led = ObjetosCaja.Caja("Led", transform, new Vector3(0.01f, 0.006f, 0.006f), ObjetosCaja.Mat(new Color(0.2f, 1f, 0.3f), 0.2f, true), false);
        led.transform.localPosition = new Vector3(0.045f, 0.103f, 0.07f);
        ObjetosCaja.Texto(transform, "CAJERO XR", new Vector3(0f, 0.06f, 0.0805f), Quaternion.identity, new Vector2(200, 40), 0.0004f, 26,
            new Color(0.95f, 0.55f, 0.15f));

        salida = new GameObject("Salida").transform;
        salida.SetParent(transform, false);
        salida.localPosition = new Vector3(0f, 0.104f, 0.02f);

        caja.VentaCobrada += Imprimir;
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        if (caja != null) caja.VentaCobrada -= Imprimir;
    }

    void Imprimir(CajaRegistradora.Venta v) { StartCoroutine(RutinaImprimir(v)); }

    /// <summary>El cliente se lleva su boleta (la ultima que salio).</summary>
    public void ClienteSeLlevaBoleta()
    {
        if (boletas.Count == 0) return;
        var b = boletas[boletas.Count - 1];
        boletas.RemoveAt(boletas.Count - 1);
        if (b != null) StartCoroutine(ObjetosCaja.Desaparecer(b, 0.3f));
    }

    IEnumerator RutinaImprimir(CajaRegistradora.Venta v)
    {
        string texto = TextoBoleta(v, numeroBoleta++);
        int lineas = texto.Split('\n').Length;
        float largo = Mathf.Clamp(0.06f + lineas * 0.0085f, 0.12f, 0.32f);

        var boleta = new GameObject("Boleta de practica");
        var papel = ObjetosCaja.Caja("Papel", boleta.transform, new Vector3(0.08f, 0.0006f, largo), ObjetosCaja.Mat(new Color(0.97f, 0.97f, 0.95f), 0.05f), false);
        ObjetosCaja.Texto(boleta.transform, texto, new Vector3(0f, 0.0005f, 0f), Quaternion.Euler(90f, 0f, 0f),
            new Vector2(300, largo / 0.00026f), 0.00026f, 13, new Color(0.08f, 0.08f, 0.1f), TextAnchor.UpperLeft);
        var col = boleta.AddComponent<BoxCollider>();
        col.size = new Vector3(0.08f, 0.01f, largo);

        // Sale de la impresora poco a poco (de pie, como un papel que va saliendo).
        var rb = boleta.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        var rot = transform.rotation * Quaternion.Euler(-80f, 0f, 0f);
        float dur = 1.0f, k = 0f;
        while (k < 1f)
        {
            k += Time.deltaTime / dur;
            boleta.transform.rotation = rot;
            boleta.transform.position = salida.position + (rot * Vector3.forward) * (largo * (k - 0.5f));
            yield return null;
        }
        // Se corta y cae sobre el mostrador: ya se puede agarrar.
        ObjetosCaja.Agarrable(boleta, 0.005f);
        rb.isKinematic = false;
        boletas.Add(boleta);
        while (boletas.Count > 3)
        {
            if (boletas[0] != null) Destroy(boletas[0]);
            boletas.RemoveAt(0);
        }
    }

    static string TextoBoleta(CajaRegistradora.Venta v, int numero)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<size=20><b>CAJERO XR</b></size>");
        sb.AppendLine("MiniMarket - Caja 1");
        sb.AppendLine("BOLETA DE PRACTICA N° " + numero.ToString("000000"));
        sb.AppendLine(System.DateTime.Now.ToString("dd/MM/yyyy") + "  " + v.hora);
        sb.AppendLine("--------------------------------");
        foreach (var l in v.lineas)
        {
            string n = l.producto.nombre.Length > 18 ? l.producto.nombre.Substring(0, 17) + "." : l.producto.nombre;
            sb.AppendLine(l.cantidad + " x " + n + "  " + ObjetosCaja.Soles(l.SubtotalCent));
        }
        sb.AppendLine("--------------------------------");
        sb.AppendLine("<b>TOTAL   " + ObjetosCaja.Soles(v.totalCent) + "</b>");
        if (v.metodo == MetodoPago.Efectivo)
        {
            sb.AppendLine("EFECTIVO  " + ObjetosCaja.Soles(v.recibidoCent));
            sb.AppendLine("VUELTO    " + ObjetosCaja.Soles(v.vueltoCent));
        }
        else sb.AppendLine("TARJETA  **** 2026  APROBADO");
        sb.AppendLine("Articulos: " + v.articulos);
        sb.AppendLine("");
        sb.AppendLine("¡Gracias por su compra!");
        sb.Append("<size=10>Documento de practica, sin valor tributario</size>");
        return sb.ToString();
    }
}
