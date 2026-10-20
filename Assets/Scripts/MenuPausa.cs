using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu de pausa DENTRO del juego (escena MinimarketXR_Full).
///
/// Se abre con el boton Menu del mando (o con Escape en el editor) y ofrece:
///   - Reanudar
///   - Reiniciar la escena
///   - Salir (volver al menu principal o cerrar la app)
///
/// Ademas es el sitio donde se ajustan las cosas que antes solo se podian cambiar en
/// el menu principal: volumen, velocidad al caminar, caminar con los brazos y calidad.
///
/// COMO FUNCIONA
/// -------------
/// Se monta solo: cualquier objeto con este componente en la escena crea el panel.
/// No hace falta tocar la escena a mano.
///
/// Al pausar se frena el tiempo (Time.timeScale = 0) para que el juego se detenga de
/// verdad, y la interfaz usa tiempo sin escalar, de modo que sigue respondiendo
/// mientras el juego esta congelado.
/// </summary>
[DefaultExecutionOrder(100)]
public class MenuPausa : MonoBehaviour
{
    [Header("Escena a la que vuelve 'Salir' (vacia = menu principal)")]
    public string escenaMenu = "Menu";

    [Header("Diseno")]
    public Color colorFondo = new Color(0.05f, 0.04f, 0.10f, 0.93f);
    public Color colorBoton = new Color(0.16f, 0.12f, 0.34f, 1f);
    public Color colorBotonHover = new Color(0.35f, 0.20f, 0.62f, 1f);
    public Color colorTexto = new Color(0.95f, 0.95f, 1f, 1f);
    public Color colorAcento = new Color(1f, 0.25f, 0.60f, 1f);
    public float distanciaAlJugador = 1.05f;

    [Tooltip("Altura FIJA del panel. Si es 0, el panel se coloca a la altura de la cabeza del jugador (ojos), que es lo que se usa normalmente.")]
    public float alturaFija = 0f;

    [Tooltip("Margen por debajo de la altura de los ojos del jugador. Un menu un poco mas bajo se mira mejor que uno a la altura de la cara.")]
    public float margenBajoOjos = 0.22f;

    GameObject panel;
    CanvasGroup grupo;
    bool abierto;
    bool construyendo;

    /// <summary>True si el menu de pausa esta abierto.</summary>
    public bool estaAbierto => abierto;
    Slider sliderVolumen, sliderVelocidad, sliderTamano;
    Toggle toggleBrazos;
    Dropdown dropdownCalidad;
    Text etiquetaCalibracion;
    AudioSource sfx;
    AudioClip cClick, cHover;

    void Start()
    {
        Construir();
        Cerrar();
    }

    void Update()
    {
        if (construyendo) return;

        // La apertura la maneja DisparadorMenuPausa (detecta el boton A/Menu del
        // mando). Aqui solo se cierra con Escape, comodo en el editor.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && abierto)
            Cerrar();

        // El panel va delante del jugador: si te mueves, se te vuelve a colocar.
        if (abierto) ColocarFrenteAlJugador();
    }

    void Alternar()
    {
        if (abierto) Cerrar();
        else Abrir();
    }

    // ------------------------------------------------------------------ abrir / cerrar
    public void Abrir()
    {
        if (abierto || construyendo) return;
        abierto = true;

        Time.timeScale = 0f;
        if (panel) panel.SetActive(true);
        if (grupo)
        {
            grupo.alpha = 0f;
            grupo.interactable = false;
            StartCoroutine(Aparecer());
        }

        ColocarFrenteAlJugador();
    }

    public void Cerrar()
    {
        abierto = false;
        if (panel) panel.SetActive(false);
        Time.timeScale = 1f;
    }

    IEnumerator Aparecer()
    {
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.unscaledDeltaTime;
            if (grupo) grupo.alpha = Mathf.Clamp01(t / 0.18f);
            yield return null;
        }
        if (grupo) { grupo.alpha = 1f; grupo.interactable = true; }
    }

    void ColocarFrenteAlJugador()
    {
        var cam = Camera.main;
        if (!cam || !panel) return;

        var xrOrigin = cam.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
        Transform refTransform = xrOrigin != null ? xrOrigin.transform : cam.transform;

        Vector3 fwd = refTransform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 pos = refTransform.position + fwd * distanciaAlJugador;

        // La altura sale de la ALTURA DE LA CABEZA del jugador (la de sus ojos),
        // no de un valor fijo: asi el menu sale siempre a la vista, mida lo que
        // mida la persona, y no hay que ajustarlo a mano.
        pos.y = alturaFija > 0.01f ? alturaFija : AlturaDelPanel(cam);

        panel.transform.position = pos;
        panel.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
    }

    /// <summary>
    /// La altura a la que se pone el panel: la altura de los ojos del jugador menos
    /// un margen. Si no se puede saber la altura de la cabeza se usa la de por
    /// defecto.
    /// </summary>
    float AlturaDelPanel(Camera cam)
    {
        // La altura de la cabeza calibrada (si la hay) manda, porque es la medida
        // real de la persona.
        float calibrada = PlayerPrefs.GetFloat("headHeight", 0f);

        float alturaOjos = cam.transform.position.y;

        // Si la camara esta dentro del XR Origin, su altura sobre el suelo es la
        // altura de la cabeza.
        var origin = cam.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
            alturaOjos = cam.transform.position.y - origin.transform.position.y;

        if (calibrada > 0.5f) alturaOjos = Mathf.Max(alturaOjos, calibrada);

        return Mathf.Max(0.9f, alturaOjos - margenBajoOjos);
    }

    // ------------------------------------------------------------------ botones
    public void Reanudar()
    {
        PlayClic();
        Cerrar();
    }

    public void Reiniciar()
    {
        PlayClic();
        Time.timeScale = 1f;
        var escena = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(escena);
    }

    public void Salir()
    {
        PlayClic();
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(escenaMenu) && Application.CanStreamedLevelBeLoaded(escenaMenu))
        {
            SceneManager.LoadScene(escenaMenu);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SetVolumen(float v)
    {
        AudioListener.volume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat("volume", AudioListener.volume);
        PlayerPrefs.Save();
        AjustesAplicador.AplicarVolumen(v);
    }

    public void SetVelocidad(float s)
    {
        PlayerPrefs.SetFloat("walkSpeed", s);
        PlayerPrefs.Save();
        // Se aplica YA: antes habia que reiniciar la escena para que cambiara.
        AjustesAplicador.AplicarVelocidad(s);
    }

    public void SetCaminarConBrazos(bool on)
    {
        PlayerPrefs.SetInt("armSwing", on ? 1 : 0);
        PlayerPrefs.Save();
        // Se aplica YA: antes habia que reiniciar la escena para que cambiara.
        AjustesAplicador.AplicarCaminarConBrazos(on);
    }

    /// <summary>
    /// Tamano de los productos al agarrarlos (0.5 a 2).
    /// </summary>
    public void SetProductScale(float s)
    {
        s = Mathf.Clamp(s, 0.5f, 2f);
        PlayerPrefs.SetFloat("productScale", s);
        PlayerPrefs.Save();

        // Se aplica al momento a los productos que ya estan en la escena.
        AjustesAplicador.AplicarTamanoProductos(s);
    }

    /// <summary>
    /// Calibra la longitud de los brazos (para el caminar con balanceo).
    /// El jugador debe tener los brazos estirados hacia delante.
    /// </summary>
    public void BotonCalibrarBrazos()
    {
        if (CalibrarBrazos.CalibrarEstatico())
        {
            float longitud = PlayerPrefs.GetFloat("armLength", 0f);
            float altura = PlayerPrefs.GetFloat("headHeight", 0f);
            Debug.Log("[Pausa] Brazos calibrados: " + longitud.ToString("0.00") + " m, altura " + altura.ToString("0.00") + " m.");
            if (etiquetaCalibracion != null)
                etiquetaCalibracion.text = "Brazos: " + longitud.ToString("0.00") + " m  |  Altura: " + altura.ToString("0.00") + " m";
        }
        else
        {
            Debug.Log("[Pausa] No se pudo medir: estira los brazos hacia delante.");
            if (etiquetaCalibracion != null)
                etiquetaCalibracion.text = "Estira los brazos y vuelve a pulsar";
        }
    }

    public void SetCalidad(int index)
    {
        PlayerPrefs.SetInt("quality", index);
        PlayerPrefs.Save();
        // Se aplica YA (antes solo se guardaba).
        AjustesAplicador.AplicarCalidad(index);
    }

    void PlayClic()
    {
        if (sfx && cClick) sfx.PlayOneShot(cClick);
    }

    // ------------------------------------------------------------------ construccion
    void Construir()
    {
        construyendo = true;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        cClick = GenerarTono("clic", 520f, 760f, 0.11f, 0.5f);
        cHover = GenerarTono("hover", 900f, 1250f, 0.07f, 0.3f);

        // ------------------------------------------------------------- canvas
        panel = new GameObject("MenuPausa_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        panel.transform.SetParent(transform, false);

        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // La camara puede no estar todavia (este script se monta en runtime): se
        // busca la principal y, si no hay, se deja que se asigne sola.
        var cam = Camera.main;
        if (cam != null) canvas.worldCamera = cam;
        canvas.sortingOrder = 100;

        // Raycaster de dispositivo (mando): sin el, el GraphicRaycaster normal solo
        // funciona con raton y los botones no se podrian pulsar en el visor.
        var raycaster = panel.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false;
        panel.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        var rt = (RectTransform)panel.transform;
        rt.sizeDelta = new Vector2(760f, 860f);
        rt.localScale = Vector3.one * 0.0012f;

        var scaler = panel.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        var fondo = CrearUI("Fondo", panel.transform, typeof(Image));
        Stretch(fondo);
        fondo.GetComponent<Image>().color = colorFondo;

        grupo = panel.AddComponent<CanvasGroup>();
        grupo.alpha = 1f;

        // ------------------------------------------------------------- contenido
        var raiz = CrearUI("Contenido", panel.transform);
        var rr = (RectTransform)raiz.transform;
        rr.anchorMin = new Vector2(0.5f, 1f);
        rr.anchorMax = new Vector2(0.5f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.anchoredPosition = new Vector2(0f, -30f);
        rr.sizeDelta = new Vector2(720f, 800f);

        var titulo = CrearTexto("Titulo", raiz.transform, "PAUSA");
        titulo.fontSize = 60;
        titulo.fontStyle = FontStyle.Bold;
        titulo.color = colorAcento;
        titulo.alignment = TextAnchor.MiddleCenter;
        var tr = (RectTransform)titulo.transform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -10f);
        tr.sizeDelta = new Vector2(0f, 76f);

        // ------------------------------------------------------------- columna izquierda
        var colIzq = CrearUI("ColIzq", raiz.transform);
        var ci = (RectTransform)colIzq.transform;
        ci.anchorMin = new Vector2(0f, 1f); ci.anchorMax = new Vector2(0.5f, 1f);
        ci.pivot = new Vector2(0f, 1f);
        ci.anchoredPosition = new Vector2(0f, -100f);
        ci.sizeDelta = new Vector2(330f, 620f);

        float y = 0f;
        CrearBoton("Reanudar", colIzq.transform, ref y, Reanudar, true);
        CrearBoton("Reiniciar", colIzq.transform, ref y, Reiniciar, false);
        CrearBoton("Salir", colIzq.transform, ref y, Salir, false);

        var ayuda = CrearTexto("Ayuda", colIzq.transform, "Boton Menu del mando para cerrar");
        ayuda.fontSize = 21;
        ayuda.color = new Color(1f, 1f, 1f, 0.5f);
        ayuda.alignment = TextAnchor.UpperCenter;
        var aer = (RectTransform)ayuda.transform;
        aer.anchorMin = new Vector2(0f, 0f); aer.anchorMax = new Vector2(1f, 0f);
        aer.pivot = new Vector2(0.5f, 0f);
        aer.anchoredPosition = new Vector2(0f, 6f);
        aer.sizeDelta = new Vector2(0f, 50f);

        // ------------------------------------------------------------- columna derecha
        var colDer = CrearUI("ColDer", raiz.transform);
        var cd = (RectTransform)colDer.transform;
        cd.anchorMin = new Vector2(0.5f, 1f); cd.anchorMax = new Vector2(1f, 1f);
        cd.pivot = new Vector2(0f, 1f);
        cd.anchoredPosition = new Vector2(20f, -100f);
        cd.sizeDelta = new Vector2(330f, 620f);

        float y2 = 0f;

        var tAjustes = CrearTexto("TituloAjustes", colDer.transform, "AJUSTES");
        tAjustes.fontSize = 32;
        tAjustes.fontStyle = FontStyle.Bold;
        tAjustes.color = colorAcento;
        tAjustes.alignment = TextAnchor.MiddleLeft;
        var ta = (RectTransform)tAjustes.transform;
        ta.anchorMin = new Vector2(0f, 1f); ta.anchorMax = new Vector2(1f, 1f);
        ta.pivot = new Vector2(0.5f, 1f);
        ta.anchoredPosition = new Vector2(0f, -6f);
        ta.sizeDelta = new Vector2(0f, 42f);
        y2 = -54f;

        sliderVolumen = CrearSlider("Volumen", colDer.transform, ref y2, "Volumen", PlayerPrefs.GetFloat("volume", 1f), SetVolumen);
        sliderVelocidad = CrearSlider("Velocidad", colDer.transform, ref y2, "Velocidad al caminar", PlayerPrefs.GetFloat("walkSpeed", 1f), SetVelocidad);
        sliderTamano = CrearSlider("Tamano productos", colDer.transform, ref y2, "Tamano de los productos", PlayerPrefs.GetFloat("productScale", 1f), SetProductScale);
        toggleBrazos = CrearToggle("Caminar con brazos", colDer.transform, ref y2, "Caminar con brazos", PlayerPrefs.GetInt("armSwing", 1) == 1, SetCaminarConBrazos);
        dropdownCalidad = CrearDropdown("Calidad", colDer.transform, ref y2, SetCalidad);

        CrearBoton("Calibrar brazos", colDer.transform, ref y2, BotonCalibrarBrazos, false);

        // Etiqueta con el resultado de la calibracion (se rellena al calibrar).
        etiquetaCalibracion = CrearTexto("EtiquetaCalibracion", colDer.transform, "");
        etiquetaCalibracion.fontSize = 15;
        etiquetaCalibracion.alignment = TextAnchor.MiddleCenter;
        etiquetaCalibracion.color = new Color(0.75f, 0.75f, 0.85f, 1f);
        var rtCal = (RectTransform)etiquetaCalibracion.transform;
        rtCal.anchorMin = new Vector2(0f, 1f); rtCal.anchorMax = new Vector2(1f, 1f);
        rtCal.pivot = new Vector2(0.5f, 1f);
        rtCal.anchoredPosition = new Vector2(0f, y2);
        rtCal.sizeDelta = new Vector2(0f, 26f);
        y2 -= 30f;

        CrearBoton("Cerrar", colDer.transform, ref y2, Cerrar, false);

        // ------------------------------------------------------------- EventSystem
        // XRUIInputModule es el que permite apuntar y pulsar los botones con el mando
        // (el StandaloneInputModule solo responde a raton/teclado).
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.transform.SetParent(transform, false);
            es.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
        }

        construyendo = false;
    }

    // ------------------------------------------------------------------ helpers UI
    GameObject CrearUI(string nombre, Transform padre, params Type[] componentes)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        foreach (var c in componentes)
            if (c != typeof(RectTransform)) go.AddComponent(c);
        return go;
    }

    void Stretch(GameObject go)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    Text CrearTexto(string nombre, Transform padre, string texto)
    {
        var go = CrearUI(nombre, padre, typeof(Text));
        var t = go.GetComponent<Text>();
        t.text = texto;
        t.font = RecursoTexto();
        t.color = colorTexto;
        t.fontSize = 28;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Font RecursoTexto()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    Button CrearBoton(string nombre, Transform padre, ref float y, UnityEngine.Events.UnityAction accion, bool acento)
    {
        var go = CrearUI(nombre, padre, typeof(Image), typeof(Button));
        var img = go.GetComponent<Image>();
        img.color = acento ? colorAcento : colorBoton;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => { accion(); });

        var colores = btn.colors;
        colores.normalColor = acento ? colorAcento : colorBoton;
        colores.highlightedColor = colorBotonHover;
        colores.pressedColor = colorBotonHover * 0.85f;
        colores.selectedColor = colorBotonHover;
        colores.fadeDuration = 0.08f;
        btn.colors = colores;

        var texto = CrearTexto("Texto", go.transform, nombre);
        texto.fontSize = 32;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = Color.white;
        texto.raycastTarget = false;
        Stretch(texto.gameObject);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, 74f);
        y -= 88f;

        var fx = go.AddComponent<BotonHover>();
        fx.imagen = img;
        fx.colorNormal = acento ? colorAcento : colorBoton;
        fx.colorHover = colorBotonHover;
        fx.sfx = sfx;
        fx.clip = cHover;

        return btn;
    }

    Slider CrearSlider(string nombre, Transform padre, ref float y, string etiqueta, float valorInicial, UnityEngine.Events.UnityAction<float> onChanged)
    {
        var cont = CrearUI(nombre, padre);
        var crt = (RectTransform)cont.transform;
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, y);
        crt.sizeDelta = new Vector2(0f, 86f);
        y -= 96f;

        var txt = CrearTexto("Etiqueta", cont.transform, etiqueta);
        txt.fontSize = 25;
        txt.alignment = TextAnchor.MiddleLeft;
        var trt = (RectTransform)txt.transform;
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, 0f);
        trt.sizeDelta = new Vector2(0f, 30f);

        var sliderGo = CrearUI("Slider", cont.transform, typeof(Slider));
        var srt = (RectTransform)sliderGo.transform;
        srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = new Vector2(0f, 8f);
        srt.sizeDelta = new Vector2(0f, 28f);

        var slider = sliderGo.GetComponent<Slider>();

        var fondo = CrearUI("Fondo", sliderGo.transform, typeof(Image));
        Stretch(fondo);
        fondo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        var rellenoArea = CrearUI("Relleno", sliderGo.transform);
        var rat = (RectTransform)rellenoArea.transform;
        rat.anchorMin = new Vector2(0f, 0f); rat.anchorMax = new Vector2(1f, 0f);
        rat.offsetMin = Vector2.zero; rat.offsetMax = Vector2.zero;

        var relleno = CrearUI("RellenoImg", rellenoArea.transform, typeof(Image));
        Stretch(relleno);
        relleno.GetComponent<Image>().color = colorAcento;

        var area = CrearUI("Area", sliderGo.transform);
        Stretch(area);

        var handleArea = CrearUI("HandleArea", area.transform);
        Stretch(handleArea);

        var handle = CrearUI("Handle", handleArea.transform, typeof(Image));
        var hrt = (RectTransform)handle.transform;
        hrt.sizeDelta = new Vector2(32f, 32f);
        handle.GetComponent<Image>().color = Color.white;

        slider.fillRect = (RectTransform)relleno.transform;
        slider.handleRect = hrt;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(valorInicial);
        slider.onValueChanged.AddListener(onChanged);

        return slider;
    }

    Toggle CrearToggle(string nombre, Transform padre, ref float y, string etiqueta, bool valorInicial, UnityEngine.Events.UnityAction<bool> onChanged)
    {
        var cont = CrearUI(nombre, padre);
        var crt = (RectTransform)cont.transform;
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, y);
        crt.sizeDelta = new Vector2(0f, 52f);
        y -= 60f;

        var txt = CrearTexto("Etiqueta", cont.transform, etiqueta);
        txt.fontSize = 25;
        txt.alignment = TextAnchor.MiddleLeft;
        var trt = (RectTransform)txt.transform;
        trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0f);
        trt.pivot = new Vector2(0f, 0f);
        trt.anchoredPosition = new Vector2(54f, 0f);
        trt.sizeDelta = new Vector2(0f, 40f);

        var box = CrearUI("Box", cont.transform, typeof(Image));
        var brt = (RectTransform)box.transform;
        brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(0f, 0.5f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.anchoredPosition = new Vector2(0f, 0f);
        brt.sizeDelta = new Vector2(38f, 38f);
        var bimg = box.GetComponent<Image>();
        bimg.color = new Color(1f, 1f, 1f, 0.2f);

        var check = CrearUI("Check", box.transform, typeof(Image));
        Stretch(check);
        check.GetComponent<Image>().color = colorAcento;

        var toggle = cont.AddComponent<Toggle>();
        toggle.targetGraphic = bimg;
        toggle.graphic = check.GetComponent<Image>();
        toggle.isOn = valorInicial;
        toggle.onValueChanged.AddListener(onChanged);

        return toggle;
    }

    Dropdown CrearDropdown(string nombre, Transform padre, ref float y, UnityEngine.Events.UnityAction<int> onChanged)
    {
        var cont = CrearUI(nombre, padre);
        var crt = (RectTransform)cont.transform;
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, y);
        crt.sizeDelta = new Vector2(0f, 52f);
        y -= 60f;

        var txt = CrearTexto("Etiqueta", cont.transform, "Calidad grafica");
        txt.fontSize = 25;
        txt.alignment = TextAnchor.MiddleLeft;
        var trt = (RectTransform)txt.transform;
        trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0f);
        trt.pivot = new Vector2(0f, 0f);
        trt.anchoredPosition = new Vector2(0f, 0f);
        trt.sizeDelta = new Vector2(0f, 40f);

        // Template: es la lista desplegable en si.
        var template = CrearUI("Template", cont.transform, typeof(Image));
        var trt2 = (RectTransform)template.transform;
        trt2.anchorMin = new Vector2(0f, 0f); trt2.anchorMax = new Vector2(0f, 0f);
        trt2.pivot = new Vector2(0f, 0f);
        trt2.anchoredPosition = new Vector2(140f, 0f);
        trt2.sizeDelta = new Vector2(190f, 40f);
        template.GetComponent<Image>().color = colorBoton;

        var caption = CrearTexto("Caption", template.transform, "");
        caption.fontSize = 22;
        caption.alignment = TextAnchor.MiddleLeft;
        caption.raycastTarget = false;
        var crt3 = (RectTransform)caption.transform;
        crt3.offsetMin = new Vector2(10f, 0f); crt3.offsetMax = new Vector2(-10f, 0f);

        var scrollArea = CrearUI("Scroll", template.transform);
        Stretch(scrollArea);

        var scrollContent = CrearUI("Item", scrollArea.transform);
        var srt = (RectTransform)scrollContent.transform;
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(0f, 34f);

        var itemToggle = scrollContent.AddComponent<Toggle>();
        var itemFondo = CrearUI("ItemFondo", scrollContent.transform, typeof(Image));
        Stretch(itemFondo);
        itemFondo.GetComponent<Image>().color = colorBoton;
        itemToggle.targetGraphic = itemFondo.GetComponent<Image>();

        var itemTxt = CrearTexto("ItemTexto", scrollContent.transform, "");
        itemTxt.fontSize = 21;
        itemTxt.alignment = TextAnchor.MiddleLeft;
        itemTxt.raycastTarget = false;
        var irt = (RectTransform)itemTxt.transform;
        irt.offsetMin = new Vector2(10f, 0f); irt.offsetMax = new Vector2(-6f, 0f);

        var dd = cont.AddComponent<Dropdown>();
        dd.captionText = caption;
        dd.itemText = itemTxt;
        dd.template = (RectTransform)template.transform;
        dd.itemImage = itemFondo.GetComponent<Image>();
        dd.ClearOptions();
        dd.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
        dd.SetValueWithoutNotify(PlayerPrefs.GetInt("quality", QualitySettings.GetQualityLevel()));
        dd.onValueChanged.AddListener(onChanged);

        return dd;
    }

    static AudioClip GenerarTono(string nombre, float f0, float f1, float dur, float vol)
    {
        const int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * dur));
        var data = new float[n];
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, u) / sr;
            float env = Mathf.Min(1f, i / (sr * 0.004f)) * Mathf.Exp(-14f * u);
            data[i] = Mathf.Sin(ph) * env * vol;
        }
        var clip = AudioClip.Create(nombre, n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// <summary>Efecto de hover para los botones del menu de pausa.</summary>
public class BotonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image imagen;
    public Color colorNormal = Color.white;
    public Color colorHover = Color.gray;
    public AudioSource sfx;
    public AudioClip clip;

    public void OnPointerEnter(PointerEventData e)
    {
        if (imagen) imagen.color = colorHover;
        if (sfx && clip) sfx.PlayOneShot(clip, 0.4f);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (imagen) imagen.color = colorNormal;
    }
}
