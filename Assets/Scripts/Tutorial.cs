using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// TUTORIAL INTERACTIVO
///
/// La primera vez que entras a la tienda aparece delante de ti un panel con los dos
/// mandos en 3D. En cada paso el boton que hay que usar se ilumina en naranja y se
/// mueve solo (el stick se inclina, el gatillo se aprieta, el grip se cierra...) y el
/// mando gira para que lo veas. El paso termina cuando TU lo haces de verdad:
///
///   1. Gatillo para empezar          5. Agarrar el lector (grip)
///   2. Caminar (stick izquierdo)     6. Escanear un producto (gatillo)
///   3. Girar (stick derecho)         7. Abrir y cerrar la pausa (☰ o B)
///   4. Ir a la caja (flecha)         8. Hablarle a un cliente
///
/// Se puede volver a ver desde el menu de pausa (boton "Tutorial"). En la PC, N salta
/// el paso. Si un paso no se puede hacer (por ejemplo, sin microfono) se pasa solo.
/// </summary>
public class Tutorial : MonoBehaviour
{
    const string ClaveVisto = "tutorial_visto";
    const float SegundosMaxPorPaso = 60f;

    public static Tutorial I { get; private set; }
    /// <summary>True mientras el tutorial esta en pantalla (la ayuda de pausa no sale).</summary>
    public static bool Activo => I != null;
    static bool pedido;

    // --------------------------------------------------------------- arranque
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (esc, modo) => Revisar(esc);
        Revisar(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void Revisar(UnityEngine.SceneManagement.Scene esc)
    {
        if (!esc.IsValid() || !esc.name.StartsWith("MinimarketXR")) return;
        if (PlayerPrefs.GetInt(ClaveVisto, 0) == 1 && !pedido) return;
        pedido = false;
        Empezar();
    }

    /// <summary>Empieza (o vuelve a empezar) el tutorial.</summary>
    public static void Empezar()
    {
        if (I != null) Destroy(I.gameObject);
        var go = new GameObject("Tutorial");
        I = go.AddComponent<Tutorial>();
    }

    /// <summary>Da por hecho el paso actual (para probar sin visor).</summary>
    public static void Saltar() { if (I == null) return; if (I.fin) { I.listoDesde = -10f; Destroy(I.gameObject); return; } if (I.hechoHasta == 0f && I.indice >= 0) I.Hecho(); }

    /// <summary>Para verlo otra vez la proxima vez que se cargue la tienda.</summary>
    public static void VerOtraVezAlCargar() { pedido = true; }

    // --------------------------------------------------------------- pasos
    enum Parte { Ninguna, Stick, Gatillo, Grip, BotonA, BotonB, Menu }

    class Paso
    {
        public string titulo, texto;
        public bool izq, der;          // que mando se ilumina
        public Parte parte;
        public string anim;            // "adelante", "lados", "apretar", "circulo"
        public float vista;            // giro del mando para que se vea la parte
    }

    readonly List<Paso> pasos = new List<Paso>();
    int indice = -1;
    float inicioPaso;
    float hechoHasta;                  // muestra "¡Bien!" un momento antes de pasar
    bool fin;

    // estado para detectar cada paso
    Vector3 posInicio;
    float giroAcumulado, yawAnterior;
    bool pausaAbierta;
    string fraseInicial = "";
    bool escaneado;
    int toquesInicio;
    float listoDesde;
    LectorCodigos lectorSuscrito;

    // --------------------------------------------------------------- visual
    Transform raiz, mandoIzq, mandoDer, flecha;
    Text tPaso, tTitulo, tTexto, tPie;
    Image barra, fondoTitulo;
    readonly Dictionary<string, Pieza> piezas = new Dictionary<string, Pieza>();
    AudioSource audioFuente;
    Unity.XR.CoreUtils.XROrigin origen;
    CajaRegistradora caja;

    static readonly Color Naranja = new Color(0.95f, 0.55f, 0.16f);
    static readonly Color Gris = new Color(0.16f, 0.17f, 0.19f);

    class Pieza
    {
        public Transform t;
        public Vector3 pos;
        public Quaternion rot;
        public Material mat;
        public Color baseColor;
        public float lado;             // +1 derecho, -1 izquierdo
    }

    void Start()
    {
        I = this;
        origen = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        caja = FindAnyObjectByType<CajaRegistradora>();
        audioFuente = gameObject.AddComponent<AudioSource>();
        audioFuente.spatialBlend = 0f;
        audioFuente.ignoreListenerPause = true;

        bool windows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
        Agregar("BIENVENIDO A CAJERO XR", "Te enseño los controles en 1 minuto.\nAprieta el <b>GATILLO</b> de cualquier mando para empezar.",
                true, true, Parte.Gatillo, "apretar", 70f);
        Agregar("CAMINAR", "Inclina el <b>STICK IZQUIERDO</b> y camina unos pasos.",
                true, false, Parte.Stick, "adelante", 0f);
        Agregar("GIRAR", "Mueve el <b>STICK DERECHO</b> a los lados para girar.\nTambién puedes girar el cuerpo.",
                false, true, Parte.Stick, "lados", 0f);
        Agregar("VE A LA CAJA", "Sigue la <b>FLECHA NARANJA</b> y acércate al mostrador de la caja.",
                true, false, Parte.Stick, "adelante", 0f);
        Agregar("AGARRAR", "Acerca la mano a la <b>PISTOLA LECTORA</b> y aprieta el <b>GRIP</b> (botón del costado) para agarrarla.\nAsí agarras todo: productos, billetes, la boleta.",
                false, true, Parte.Grip, "apretar", -90f);
        Agregar("ESCANEAR", "Con la pistola en la mano, apunta al código de un producto y aprieta el <b>GATILLO</b>.\nToma un producto del estante o espera a un cliente.",
                false, true, Parte.Gatillo, "apretar", 70f);
        Agregar("MENÚ DE PAUSA", "Aprieta el botón <b>☰</b> del mando izquierdo (o <b>B</b>) para abrir la pausa.\nLuego apriétalo otra vez para cerrarla.",
                true, true, Parte.Menu, "apretar", 0f);
        Agregar("TOCAR LA PANTALLA", "Mantén apretado el <b>GATILLO</b>: tu mano pone el <b>DEDO ÍNDICE</b>.\nToca con ese dedo un botón de la pantalla de la caja (así cobras).",
                false, true, Parte.Gatillo, "apretar", 70f);

        CrearPanel();
        mandoIzq = CrearMando(-1f);
        mandoDer = CrearMando(1f);
        CrearFlecha();
        Colocar(true);
        Siguiente();
        Debug.Log("[Tutorial] Empieza (" + pasos.Count + " pasos).");
    }

    void Agregar(string titulo, string texto, bool izq, bool der, Parte parte, string anim, float vista)
    {
        pasos.Add(new Paso { titulo = titulo, texto = texto, izq = izq, der = der, parte = parte, anim = anim, vista = vista });
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        if (lectorSuscrito != null) lectorSuscrito.ProductoEscaneado -= AlEscanear;
        if (flecha != null) Destroy(flecha.gameObject);
    }

    void AlEscanear(GameObject go, Producto p) { escaneado = true; }

    // --------------------------------------------------------------- flujo
    void Siguiente()
    {
        indice++;
        if (indice >= pasos.Count) { Terminar(); return; }
        var p = pasos[indice];
        inicioPaso = Time.unscaledTime;
        hechoHasta = 0f;
        tPaso.text = "TUTORIAL  ·  PASO " + (indice + 1) + " DE " + pasos.Count;
        tTitulo.text = p.titulo;
        tTexto.text = p.texto;
        tPie.text = "N: saltar paso (PC)";
        fondoTitulo.color = Naranja;

        // Estado inicial para detectar el paso.
        var cam = Camera.main;
        posInicio = cam != null ? cam.transform.position : Vector3.zero;
        giroAcumulado = 0f;
        yawAnterior = origen != null ? origen.transform.eulerAngles.y : 0f;
        pausaAbierta = false;
        fraseInicial = VozJugador.I != null ? VozJugador.I.UltimaFrase : "";
        escaneado = false;
        toquesInicio = AvatarJugador.Toques;
        if (p.titulo == "ESCANEAR" && lectorSuscrito == null)
        {
            lectorSuscrito = FindAnyObjectByType<LectorCodigos>();
            if (lectorSuscrito != null) lectorSuscrito.ProductoEscaneado += AlEscanear;
        }
        if (flecha != null) flecha.gameObject.SetActive(p.titulo == "VE A LA CAJA");
        mandoIzq.gameObject.SetActive(p.izq);
        mandoDer.gameObject.SetActive(p.der);
    }

    void Hecho()
    {
        hechoHasta = Time.unscaledTime + 1.3f;
        tTitulo.text = "¡MUY BIEN!";
        fondoTitulo.color = new Color(0.25f, 0.7f, 0.35f);
        audioFuente.PlayOneShot(SonidosProcedurales.Logro(), 0.7f);
        Haptica.Ambos(0.4f, 0.08f);
    }

    void Terminar()
    {
        fin = true;
        PlayerPrefs.SetInt(ClaveVisto, 1);
        PlayerPrefs.Save();
        tPaso.text = "TUTORIAL COMPLETO";
        tTitulo.text = "¡LISTO PARA ATENDER!";
        tTexto.text = "Los clientes entran cuando tú digas.\nAprieta el <b>GATILLO</b> para <b>COMENZAR EL TURNO</b>.";
        tPie.text = "Tip: a los clientes les puedes hablar (\"hola\", \"¿cómo va a pagar?\").  Espacio: comenzar (PC)";
        listoDesde = Time.unscaledTime;
        barra.fillAmount = 1f;
        mandoIzq.gameObject.SetActive(false);
        mandoDer.gameObject.SetActive(false);
        if (flecha != null) flecha.gameObject.SetActive(false);
        Debug.Log("[Tutorial] Completo: esperando que comience el turno.");
    }

    void Update()
    {
        if (fin)
        {
            Colocar(false);
            // Recien al apretar el gatillo empieza el turno (y entran los clientes).
            if (Time.unscaledTime - listoDesde > 0.8f && (GatilloApretado() ||
                (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.nKey.wasPressedThisFrame))))
            {
                audioFuente.PlayOneShot(SonidosProcedurales.Logro(), 0.7f);
                Debug.Log("[Tutorial] Turno iniciado.");
                Destroy(gameObject, 0.3f);
                fin = false; indice = pasos.Count + 1; enabled = false;
            }
            return;
        }
        float t = Time.unscaledTime;
        barra.fillAmount = (indice + (hechoHasta > 0f ? 1f : 0f)) / (float)pasos.Count;

        if (hechoHasta > 0f)
        {
            if (t > hechoHasta) Siguiente();
        }
        else if (Cumplido() || (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame))
            Hecho();
        else if (t - inicioPaso > SegundosMaxPorPaso)
            Siguiente();       // no se pudo (sin microfono, sin clientes...): se sigue

        Colocar(false);
        Animar(t);
    }

    bool Cumplido()
    {
        if (indice < 0 || indice >= pasos.Count) return false;
        var cam = Camera.main;
        switch (pasos[indice].titulo)
        {
            case "BIENVENIDO A CAJERO XR":
                return Time.unscaledTime - inicioPaso > 0.6f && (GatilloApretado() ||
                       (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame));
            case "CAMINAR":
                if (cam == null) return false;
                var d = cam.transform.position - posInicio; d.y = 0f;
                return d.magnitude > 1.2f;
            case "GIRAR":
                float yaw = origen != null ? origen.transform.eulerAngles.y : (cam != null ? cam.transform.eulerAngles.y : 0f);
                giroAcumulado += Mathf.Abs(Mathf.DeltaAngle(yawAnterior, yaw));
                yawAnterior = yaw;
                return giroAcumulado > 60f;
            case "VE A LA CAJA":
                if (cam == null || caja == null || caja.Mostrador == null) return true;
                var c = caja.Mostrador.position - cam.transform.position; c.y = 0f;
                return c.magnitude < 2.2f;
            case "AGARRAR":
                foreach (var l in FindObjectsByType<LectorCodigos>(FindObjectsSortMode.None))
                    if (l.grab != null && l.grab.isSelected) return true;
                return false;
            case "ESCANEAR":
                return escaneado;
            case "MENÚ DE PAUSA":
                var menu = FindAnyObjectByType<MenuPausa>();
                if (menu == null) return true;
                if (menu.estaAbierto) pausaAbierta = true;
                return pausaAbierta && !menu.estaAbierto;
            case "TOCAR LA PANTALLA":
                return AvatarJugador.Toques > toquesInicio;
        }
        return false;
    }

    static bool GatilloApretado()
    {
        foreach (var dev in InputSystem.devices)
        {
            if (!(dev is UnityEngine.InputSystem.XR.XRController)) continue;
            foreach (var n in new[] { "triggerPressed", "triggerButton" })
                if (dev.TryGetChildControl(n) is ButtonControl b && b.wasPressedThisFrame) return true;
            if (dev.TryGetChildControl("trigger") is AxisControl a && a.ReadValue() > 0.75f) return true;
        }
        return false;
    }

    // --------------------------------------------------------------- posicion
    void Colocar(bool deGolpe)
    {
        var cam = Camera.main;
        if (cam == null || raiz == null) return;
        var f = cam.transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        f.Normalize();
        var objetivo = cam.transform.position + f * 1.25f + Vector3.down * 0.2f;
        // Se mueve con calma: solo si te alejas o giras bastante.
        float dist = Vector3.Distance(raiz.position, objetivo);
        float ang = Vector3.Angle(raiz.forward, f);
        float k = deGolpe ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * (dist > 0.35f || ang > 30f ? 3f : 0.6f));
        raiz.position = Vector3.Lerp(raiz.position, objetivo, k);
        raiz.rotation = Quaternion.Slerp(raiz.rotation, Quaternion.LookRotation(f, Vector3.up), k);
    }

    // --------------------------------------------------------------- animacion
    void Animar(float t)
    {
        var p = indice >= 0 && indice < pasos.Count ? pasos[indice] : null;
        foreach (var kv in piezas)
        {
            var pz = kv.Value;
            bool activa = p != null && EsParte(kv.Key, p.parte);
            float pulso = activa ? 0.5f + 0.5f * Mathf.Sin(t * 6f) : 0f;
            var col = Color.Lerp(pz.baseColor, Naranja, activa ? 0.55f + 0.45f * pulso : 0f);
            pz.mat.color = col;
            if (pz.mat.HasProperty("_BaseColor")) pz.mat.SetColor("_BaseColor", col);
            if (pz.mat.HasProperty("_EmissionColor")) pz.mat.SetColor("_EmissionColor", activa ? Naranja * (0.25f + 0.6f * pulso) : Color.black);

            pz.t.localPosition = pz.pos;
            pz.t.localRotation = pz.rot;
            if (!activa) continue;
            float ciclo = Mathf.Repeat(t, 1.6f) / 1.6f;            // 0..1
            float onda = Mathf.Sin(ciclo * Mathf.PI);              // sube y baja
            switch (kv.Key.Substring(2))
            {
                case "stick":
                    if (p.anim == "adelante") pz.t.localRotation = pz.rot * Quaternion.Euler(25f * onda, 0f, 0f);
                    else if (p.anim == "lados") pz.t.localRotation = pz.rot * Quaternion.Euler(0f, 0f, 25f * Mathf.Sin(t * 3.5f));
                    else pz.t.localPosition = pz.pos + pz.t.parent.InverseTransformDirection(-pz.t.up) * 0.004f * onda;
                    break;
                case "gatillo":
                    pz.t.localRotation = pz.rot * Quaternion.Euler(-28f * onda, 0f, 0f);
                    break;
                case "grip":
                    pz.t.localPosition = pz.pos + new Vector3((pz.pos.x > 0f ? -0.006f : 0.006f) * onda, 0f, 0f);
                    break;
                default:   // botones
                    pz.t.localPosition = pz.pos + Vector3.down * 0.003f * onda;
                    break;
            }
        }

        // El mando gira para mostrar la parte (de frente, de costado...).
        float vista = p != null ? p.vista : 0f;
        foreach (var m in new[] { mandoIzq, mandoDer })
        {
            if (m == null) continue;
            float lado = m == mandoDer ? 1f : -1f;
            var objetivo = Quaternion.Euler(0f, 180f + vista * lado + 6f * Mathf.Sin(t * 1.3f), 0f);
            m.localRotation = Quaternion.Slerp(m.localRotation, objetivo, Time.unscaledDeltaTime * 4f);
            m.localPosition = new Vector3(m.localPosition.x, -0.02f + 0.01f * Mathf.Sin(t * 2f + lado), m.localPosition.z);
        }

        if (flecha != null && flecha.gameObject.activeSelf && caja != null && caja.Mostrador != null)
        {
            flecha.position = caja.Mostrador.position + Vector3.up * (1.9f + 0.15f * Mathf.Sin(t * 4f));
            flecha.rotation = Quaternion.Euler(0f, t * 90f, 0f);
        }
    }

    static bool EsParte(string clave, Parte parte)
    {
        string p = clave.Substring(2);
        switch (parte)
        {
            case Parte.Stick: return p == "stick";
            case Parte.Gatillo: return p == "gatillo";
            case Parte.Grip: return p == "grip";
            case Parte.BotonA: return p == "a";
            case Parte.BotonB: return p == "b";
            case Parte.Menu: return p == "menu" || (clave[0] == 'D' && p == "b");
        }
        return false;
    }

    // --------------------------------------------------------------- construccion
    void CrearPanel()
    {
        raiz = new GameObject("Tutorial (panel)").transform;
        raiz.SetParent(transform, false);

        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(raiz, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(760, 330);
        rt.localScale = Vector3.one * 0.0011f;
        rt.localPosition = new Vector3(0f, 0.17f, 0f);

        Imagen(go.transform, "Fondo", new Color(0.07f, 0.08f, 0.11f, 0.97f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fondoTitulo = Imagen(go.transform, "Franja", Naranja, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), Vector2.zero);
        var barraFondo = Imagen(go.transform, "BarraFondo", new Color(1f, 1f, 1f, 0.12f), new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 8f));
        barra = Imagen(barraFondo.transform, "Barra", Naranja, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        barra.type = Image.Type.Filled; barra.fillMethod = Image.FillMethod.Horizontal; barra.fillAmount = 0f;
        barra.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

        tPaso = Texto(go.transform, 20, new Color(1f, 1f, 1f, 0.55f), FontStyle.Bold, new Vector2(0f, -40f), 30f);
        tTitulo = Texto(go.transform, 44, Naranja, FontStyle.Bold, new Vector2(0f, -88f), 56f);
        tTexto = Texto(go.transform, 25, Color.white, FontStyle.Normal, new Vector2(0f, -188f), 130f);
        tPie = Texto(go.transform, 17, new Color(1f, 1f, 1f, 0.4f), FontStyle.Normal, new Vector2(0f, -300f), 24f);

        // Que el panel se vea siempre encima (aunque haya un estante o un carrito delante).
        var encima = new Material(Shader.Find("UI/Default"));
        encima.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        foreach (var g in go.GetComponentsInChildren<Graphic>(true)) g.material = encima;
        go.GetComponent<Canvas>().sortingOrder = 50;
    }

    static Image Imagen(Transform padre, string nombre, Color c, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.color = c; img.raycastTarget = false;
        var r = (RectTransform)go.transform;
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = offMin; r.offsetMax = offMax;
        if (nombre == "Franja") { r.offsetMin = new Vector2(0f, -8f); r.offsetMax = Vector2.zero; }
        return img;
    }

    static Text Texto(Transform padre, int tam, Color c, FontStyle estilo, Vector2 pos, float alto)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = tam; t.color = c; t.fontStyle = estilo;
        t.alignment = TextAnchor.MiddleCenter; t.supportRichText = true; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        var r = t.rectTransform;
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = new Vector2(-50f, alto);
        return t;
    }

    /// <summary>Un mando tipo Quest hecho con piezas simples (x2.4 para que se vea bien).</summary>
    Transform CrearMando(float lado)
    {
        string pre = lado > 0 ? "D_" : "I_";
        var m = new GameObject(lado > 0 ? "Mando derecho" : "Mando izquierdo").transform;
        m.SetParent(raiz, false);
        m.localPosition = new Vector3(0.52f * lado, -0.02f, -0.02f);
        m.localRotation = Quaternion.Euler(0f, 180f, 0f);
        m.localScale = Vector3.one * 2.4f;

        // Mango
        Pieza_(pre + "cuerpo", PrimitiveType.Capsule, m, new Vector3(0f, -0.035f, 0.01f), Quaternion.Euler(20f, 0f, 0f), new Vector3(0.032f, 0.045f, 0.034f), Gris, lado);
        // Cabeza inclinada hacia ti, con su cara de botones
        var cabeza = new GameObject("Cabeza").transform;
        cabeza.SetParent(m, false);
        cabeza.localPosition = new Vector3(0f, 0.018f, -0.004f);
        cabeza.localRotation = Quaternion.Euler(40f, 0f, 0f);
        Pieza_(pre + "cara", PrimitiveType.Cylinder, cabeza, Vector3.zero, Quaternion.identity, new Vector3(0.058f, 0.007f, 0.058f), new Color(0.1f, 0.1f, 0.11f), lado);
        // Anillo
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI * 2f / 16f;
            Pieza_(pre + "anillo" + i, PrimitiveType.Cube, cabeza, new Vector3(Mathf.Cos(a) * 0.043f, -0.002f, Mathf.Sin(a) * 0.043f),
                   Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), new Vector3(0.006f, 0.012f, 0.018f), new Color(0.22f, 0.23f, 0.25f), lado);
        }
        // Stick
        var stick = Pieza_(pre + "stick", PrimitiveType.Cylinder, cabeza, new Vector3(-0.012f * lado, 0.006f, 0.004f), Quaternion.identity,
                           new Vector3(0.016f, 0.006f, 0.016f), new Color(0.2f, 0.2f, 0.22f), lado);
        var tapa = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(tapa.GetComponent<Collider>());
        tapa.transform.SetParent(stick, false);
        tapa.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        tapa.transform.localScale = new Vector3(1.1f, 0.9f, 1.1f);
        tapa.GetComponent<MeshRenderer>().sharedMaterial = stick.GetComponent<MeshRenderer>().sharedMaterial;
        // Botones (A/B en el derecho, X/Y y ☰ en el izquierdo)
        Pieza_(pre + "a", PrimitiveType.Cylinder, cabeza, new Vector3(0.012f * lado, 0.004f, 0.01f), Quaternion.identity, new Vector3(0.011f, 0.003f, 0.011f), new Color(0.3f, 0.3f, 0.33f), lado);
        Pieza_(pre + "b", PrimitiveType.Cylinder, cabeza, new Vector3(0.017f * lado, 0.004f, -0.006f), Quaternion.identity, new Vector3(0.011f, 0.003f, 0.011f), new Color(0.3f, 0.3f, 0.33f), lado);
        if (lado < 0)
            Pieza_(pre + "menu", PrimitiveType.Cylinder, cabeza, new Vector3(0.002f, 0.004f, -0.018f), Quaternion.identity, new Vector3(0.008f, 0.003f, 0.008f), new Color(0.3f, 0.3f, 0.33f), lado);
        // Gatillo (adelante, debajo de la cabeza) y grip (costado de adentro del mango)
        Pieza_(pre + "gatillo", PrimitiveType.Cube, m, new Vector3(0f, 0.0f, 0.032f), Quaternion.Euler(-25f, 0f, 0f), new Vector3(0.014f, 0.026f, 0.01f), new Color(0.2f, 0.2f, 0.22f), lado);
        Pieza_(pre + "grip", PrimitiveType.Cube, m, new Vector3(0.017f * lado, -0.035f, 0.012f), Quaternion.Euler(20f, 0f, 0f), new Vector3(0.008f, 0.042f, 0.022f), new Color(0.2f, 0.2f, 0.22f), lado);

        // Nombre debajo
        ObjetosCaja.Texto(m, lado > 0 ? "DERECHO" : "IZQUIERDO", new Vector3(0f, -0.1f, 0f), Quaternion.Euler(0f, 180f, 0f),
                          new Vector2(300, 60), 0.0004f, 40, new Color(1f, 1f, 1f, 0.75f));
        return m;
    }

    Transform Pieza_(string clave, PrimitiveType tipo, Transform padre, Vector3 pos, Quaternion rot, Vector3 escala, Color c, float lado)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = clave;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = escala;
        var mat = ObjetosCaja.Mat(c, 0.55f);
        mat.EnableKeyword("_EMISSION");
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!clave.Contains("anillo") && !clave.EndsWith("cara") && !clave.EndsWith("cuerpo"))
            piezas[clave] = new Pieza { t = go.transform, pos = pos, rot = rot, mat = mat, baseColor = c, lado = lado };
        return go.transform;
    }

    void CrearFlecha()
    {
        flecha = new GameObject("Tutorial (flecha a la caja)").transform;
        var mat = ObjetosCaja.Mat(Naranja, 0.3f, true);
        var palo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        palo.transform.SetParent(flecha, false);
        palo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        palo.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
        foreach (var s in new[] { -1f, 1f })
        {
            var punta = GameObject.CreatePrimitive(PrimitiveType.Cube);
            punta.transform.SetParent(flecha, false);
            punta.transform.localPosition = new Vector3(0.07f * s, 0.06f, 0f);
            punta.transform.localRotation = Quaternion.Euler(0f, 0f, -45f * s);
            punta.transform.localScale = new Vector3(0.08f, 0.24f, 0.08f);
        }
        foreach (var r in flecha.GetComponentsInChildren<MeshRenderer>())
        {
            Destroy(r.GetComponent<Collider>());
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        flecha.gameObject.SetActive(false);
    }
}
