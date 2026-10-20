// COLOCAR EN: Assets/Scripts/MainMenu.cs  (reemplaza el anterior)
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>Claves de los ajustes guardados (PlayerPrefs).</summary>
public static class GameSettings
{
    public const string Volume = "volume";
    public const string WalkSpeed = "walkSpeed";
    public const string ArmSwing = "armSwing";
    public const string Quality = "quality";
    public const string NightMinutes = "minutesUntilNight";
}

/// <summary>
/// Efecto de hover/click para cualquier elemento de UI (botones, tarjetas, handles).
/// Se agrega solo desde MainMenu en runtime: escala suave, "squash" al presionar y brillo del borde.
/// </summary>
public class UIHoverFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float hoverScale = 1.06f;
    public float pressScale = 0.95f;
    public bool usePress = true;
    public Graphic glow;                 // normalmente el hijo "Borde"
    public System.Action onEnter;
    [HideInInspector] public float entrance = 1f;   // lo usa la animacion de entrada del panel

    bool hover, press;
    float cur = 1f, g, idle;
    Selectable sel;

    void Awake() { sel = GetComponent<Selectable>(); }

    public void SetGlow(Graphic target)
    {
        glow = target;
        if (glow) { idle = glow.color.a; g = idle; }
    }

    bool Ok { get { return sel == null || sel.IsInteractable(); } }

    public void OnPointerEnter(PointerEventData e) { if (!Ok) return; hover = true; if (onEnter != null) onEnter(); }
    public void OnPointerExit(PointerEventData e) { hover = false; press = false; }
    public void OnPointerDown(PointerEventData e) { if (usePress && Ok) press = true; }
    public void OnPointerUp(PointerEventData e) { press = false; }

    void OnDisable()
    {
        hover = press = false;
        cur = 1f;
        transform.localScale = Vector3.one * entrance;
        if (glow) { g = idle; var c = glow.color; c.a = idle; glow.color = c; }
    }

    void Update()
    {
        float target = press ? pressScale : (hover ? hoverScale : 1f);
        float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
        cur = Mathf.Lerp(cur, target, k);
        transform.localScale = Vector3.one * (cur * entrance);

        if (glow)
        {
            float gt = hover ? Mathf.Min(1f, idle + 0.65f) : idle;
            g = Mathf.Lerp(g, gt, k);
            var c = glow.color; c.a = g; glow.color = c;
        }
    }
}

/// <summary>
/// Menu de inicio VR: Jugar, Ajustes, Controles, Creditos y Salir.
///
/// Extras: transiciones deslizantes entre paneles con entrada escalonada, hover/press en todo,
/// sonidos sintetizados (no necesitas clips), vibracion en controles, logo con anillos y easter egg,
/// pestanas Controles/Manos, interruptor animado, consejos rotativos, neon que pulsa,
/// polvo, objetos flotantes y carga con fundido a negro.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Escena de juego (nombre EXACTO)")]
    public string gameScene = "MinimarketXR_Full";

    [Header("Paneles")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject controlsPanel;
    public GameObject creditsPanel;

    [Header("Ajustes: volumen (Slider 0 a 1)")]
    public Slider volumeSlider;
    public TMP_Text volumeLabel;

    [Header("Ajustes: velocidad al caminar (Slider 0.5 a 2)")]
    public Slider walkSpeedSlider;
    public TMP_Text walkSpeedLabel;

    [Header("Ajustes: caminar con brazos")]
    public Toggle armSwingToggle;
    public Image armSwingPill;
    public RectTransform armSwingKnob;
    public Color pillOn = new Color(0.35f, 0.92f, 0.62f, 1f);
    public Color pillOff = new Color(0.20f, 0.14f, 0.36f, 1f);

    [Header("Ajustes: calidad grafica")]
    public TMP_Dropdown qualityDropdown;

    [Header("Ajustes: minutos hasta la noche (opcional)")]
    public Slider nightMinutesSlider;
    public TMP_Text nightMinutesLabel;

    [Header("Pestanas de Controles")]
    public Image tabControllers;
    public Image tabHands;
    public Color tabOn = new Color(1f, 0.25f, 0.60f, 1f);
    public Color tabOff = new Color(0.22f, 0.14f, 0.40f, 1f);
    public GameObject[] controllerTexts;
    public GameObject[] handTexts;

    [Header("Barra lateral (mismo orden que los paneles: Inicio, Ajustes, Controles, Creditos)")]
    public GameObject sidebar;
    public Image[] navPills;
    public Color navOn = new Color(1f, 0.25f, 0.60f, 1f);
    public Color navOff = new Color(0.22f, 0.14f, 0.40f, 0.35f);

    [Header("Orbes de luz que derivan detras del menu")]
    public RectTransform[] drifters;

    [Header("Logo y decoracion del menu")]
    public RectTransform logoCore;
    public RectTransform[] logoRings;
    public Graphic[] pulseGraphics;
    public TMP_Text tipLabel;
    public string[] tips =
    {
        "Toca el logo de la barra lateral... pasan cosas",
        "Agarra con Grip o cierra la mano",
        "Estira los brazos y muévelos para caminar",
        "Sube el volumen para el audio 3D del súper",
        "Pasa por las zonas para activar sonidos",
        "Sin cola y sin carrito. Por ahora"
    };

    [Header("Ambiente del cuarto")]
    public Transform menuCanvas;
    public bool floatMenu = true;
    public Transform[] floatingItems;      // giran, cabecean y flotan
    public Transform[] bobItems;           // solo flotan (etiquetas de precio)
    public Renderer[] neonRenderers;       // neon que pulsa
    public float spinSpeed = 40f;

    [Header("Transiciones y hover")]
    public float fadeOutTime = 0.14f;
    public float fadeInTime = 0.22f;
    public float hoverScale = 1.06f;

    [Header("Sonidos (opcional: si no pones clips se generan solos)")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float sfxVolume = 0.6f;
    public bool haptics = true;

    // ------------------------------------------------------------ estado interno
    AudioSource sfx;
    AudioClip cHover, cClick, cBack, cTick, cChime, cStart;
    GameObject current;
    Coroutine switching, stagger;
    Vector3 canvasBase, canvasScale;
    Quaternion canvasRot;
    Vector3[] itemBase, bobBase;
    Vector2[] drifterBase;
    Material[] neonMats;
    Color[] neonBase;
    float[] pulseBase;
    readonly List<RectTransform> shines = new List<RectTransform>();
    readonly List<InputDevice> devices = new List<InputDevice>();
    float introK = 1f, logoBoost, roomBoost, lastTick;
    int pokes, tipIndex;
    bool loading, handsMode;

    // ============================================================ ciclo de vida
    void Start()
    {
        SetupUI();
        SetupFX();

        if (menuCanvas)
        {
            canvasBase = menuCanvas.position;
            canvasRot = menuCanvas.rotation;
            canvasScale = menuCanvas.localScale;
            introK = 0.6f;
        }
        if (floatingItems != null)
        {
            itemBase = new Vector3[floatingItems.Length];
            for (int i = 0; i < floatingItems.Length; i++)
                if (floatingItems[i]) itemBase[i] = floatingItems[i].position;
        }
        if (bobItems != null)
        {
            bobBase = new Vector3[bobItems.Length];
            for (int i = 0; i < bobItems.Length; i++)
                if (bobItems[i]) bobBase[i] = bobItems[i].position;
        }
        if (neonRenderers != null)
        {
            neonMats = new Material[neonRenderers.Length];
            neonBase = new Color[neonRenderers.Length];
            for (int i = 0; i < neonRenderers.Length; i++)
            {
                if (!neonRenderers[i]) continue;
                neonMats[i] = neonRenderers[i].material;   // instancia propia, no toca el asset
                neonBase[i] = neonMats[i].HasProperty("_EmissionColor") ? neonMats[i].GetColor("_EmissionColor") : Color.black;
            }
        }
        if (pulseGraphics != null)
        {
            pulseBase = new float[pulseGraphics.Length];
            for (int i = 0; i < pulseGraphics.Length; i++)
                if (pulseGraphics[i]) pulseBase[i] = pulseGraphics[i].color.a;
        }

        if (drifters != null)
        {
            drifterBase = new Vector2[drifters.Length];
            for (int i = 0; i < drifters.Length; i++)
                if (drifters[i]) drifterBase[i] = drifters[i].anchoredPosition;
        }
        if (navPills != null)
            for (int i = 0; i < navPills.Length; i++)
                if (navPills[i]) navPills[i].color = i == 0 ? navOn : navOff;

        ApplyControlsMode();
        ShowOnly(mainPanel, true);
        if (sidebar) StartCoroutine(StaggerIn(sidebar, false));
        StartCoroutine(IntroRoutine());
        if (tipLabel && tips != null && tips.Length > 0) StartCoroutine(TipRoutine());
    }

    void Update()
    {
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        float k = 1f - Mathf.Exp(-14f * dt);

        // menu flotando + balanceo suave
        if (menuCanvas)
        {
            if (floatMenu)
            {
                menuCanvas.position = canvasBase + Vector3.up * (Mathf.Sin(t * 0.9f) * 0.012f);
                menuCanvas.rotation = canvasRot * Quaternion.Euler(Mathf.Sin(t * 0.7f) * 0.9f, Mathf.Sin(t * 0.5f + 1f) * 1.6f, 0f);
            }
            menuCanvas.localScale = canvasScale * introK;
        }

        // anillos del logo (giran mas rapido tras tocar el logo)
        logoBoost = Mathf.Lerp(logoBoost, 0f, 1f - Mathf.Exp(-2.2f * dt));
        roomBoost = Mathf.Lerp(roomBoost, 0f, 1f - Mathf.Exp(-0.8f * dt));
        if (logoRings != null)
            for (int i = 0; i < logoRings.Length; i++)
                if (logoRings[i])
                    logoRings[i].Rotate(0f, 0f, (i % 2 == 0 ? -1f : 1.4f) * (26f + logoBoost * 260f) * dt);

        // pulso de brillo
        if (pulseGraphics != null && pulseBase != null)
            for (int i = 0; i < pulseGraphics.Length; i++)
            {
                if (!pulseGraphics[i]) continue;
                var c = pulseGraphics[i].color;
                c.a = pulseBase[i] * (0.65f + 0.35f * Mathf.Sin(t * 1.7f + i * 1.3f));
                pulseGraphics[i].color = c;
            }

        // brillo que barre el boton principal
        for (int i = 0; i < shines.Count; i++)
        {
            var s = shines[i];
            if (!s) continue;
            var img = s.GetComponent<Image>();
            float phase = Mathf.Repeat(t * 0.32f + i * 0.5f, 1f);
            float w = ((RectTransform)s.parent).rect.width;
            float u = Mathf.Clamp01(phase / 0.3f);
            s.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.62f, w * 0.62f, u), 0f);
            if (img) { var c = img.color; c.a = phase < 0.3f ? 0.22f : 0f; img.color = c; }
        }

        // navegacion lateral: la pagina activa se ilumina
        if (navPills != null)
        {
            int ci = System.Array.IndexOf(Panels(), current);
            for (int i = 0; i < navPills.Length; i++)
                if (navPills[i])
                    navPills[i].color = Color.Lerp(navPills[i].color, i == ci ? navOn : navOff, k);
        }

        // orbes de luz que derivan
        if (drifters != null && drifterBase != null)
            for (int i = 0; i < drifters.Length; i++)
            {
                if (!drifters[i]) continue;
                drifters[i].anchoredPosition = drifterBase[i] + new Vector2(
                    Mathf.Sin(t * 0.23f + i * 2.1f) * 70f, Mathf.Cos(t * 0.19f + i * 1.4f) * 50f);
                drifters[i].localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 0.6f + i));
            }

        // interruptor animado
        if (armSwingToggle && armSwingKnob && armSwingPill)
        {
            bool on = armSwingToggle.isOn;
            var p = armSwingKnob.anchoredPosition;
            p.x = Mathf.Lerp(p.x, on ? 24f : -24f, k);
            armSwingKnob.anchoredPosition = p;
            armSwingPill.color = Color.Lerp(armSwingPill.color, on ? pillOn : pillOff, k);
        }

        // neon que respira
        if (neonMats != null)
            for (int i = 0; i < neonMats.Length; i++)
            {
                if (!neonMats[i]) continue;
                float m = 0.75f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 1.4f + i * 0.9f));
                neonMats[i].SetColor("_EmissionColor", neonBase[i] * m);
            }

        // objetos flotantes: giran, cabecean y suben/bajan
        if (floatingItems != null && itemBase != null)
            for (int i = 0; i < floatingItems.Length && i < itemBase.Length; i++)
            {
                var it = floatingItems[i];
                if (!it) continue;
                float sp = spinSpeed * (1f + roomBoost * 6f);
                it.Rotate(sp * 0.3f * dt, sp * dt, 0f, Space.Self);
                it.position = itemBase[i] + Vector3.up * (Mathf.Sin(t * 1.1f + i * 1.7f) * 0.07f);
            }
        if (bobItems != null && bobBase != null)
            for (int i = 0; i < bobItems.Length && i < bobBase.Length; i++)
                if (bobItems[i])
                    bobItems[i].position = bobBase[i] + Vector3.up * (Mathf.Sin(t * 0.8f + i * 2.1f) * 0.05f);
    }

    IEnumerator IntroRoutine()
    {
        float t = 0f, d = 0.75f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            introK = Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(Mathf.Clamp01(t / d)));
            yield return null;
        }
        introK = 1f;
    }

    IEnumerator TipRoutine()
    {
        var c = tipLabel.color;
        while (true)
        {
            tipLabel.text = tips[tipIndex % tips.Length];
            yield return FadeText(0f, 1f, 0.4f);
            yield return new WaitForSecondsRealtime(3.6f);
            yield return FadeText(1f, 0f, 0.4f);
            tipIndex++;
        }
    }

    IEnumerator FadeText(float a, float b, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            tipLabel.alpha = Mathf.Lerp(a, b, Mathf.Clamp01(t / dur));
            yield return null;
        }
        tipLabel.alpha = b;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // ============================================================ UI y ajustes
    void SetupUI()
    {
        float vol = PlayerPrefs.GetFloat(GameSettings.Volume, 1f);
        float speed = PlayerPrefs.GetFloat(GameSettings.WalkSpeed, 1f);
        bool swing = PlayerPrefs.GetInt(GameSettings.ArmSwing, 1) == 1;
        float mins = PlayerPrefs.GetFloat(GameSettings.NightMinutes, 10f);
        int quality = PlayerPrefs.GetInt(GameSettings.Quality, QualitySettings.GetQualityLevel());

        AudioListener.volume = vol;
        QualitySettings.SetQualityLevel(quality, true);

        if (volumeSlider)
        {
            volumeSlider.SetValueWithoutNotify(vol);
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        if (walkSpeedSlider)
        {
            walkSpeedSlider.SetValueWithoutNotify(speed);
            walkSpeedSlider.onValueChanged.RemoveAllListeners();
            walkSpeedSlider.onValueChanged.AddListener(SetWalkSpeed);
        }
        if (armSwingToggle)
        {
            armSwingToggle.SetIsOnWithoutNotify(swing);
            armSwingToggle.onValueChanged.RemoveAllListeners();
            armSwingToggle.onValueChanged.AddListener(SetArmSwing);
            if (armSwingKnob) armSwingKnob.anchoredPosition = new Vector2(swing ? 24f : -24f, 0f);
            if (armSwingPill) armSwingPill.color = swing ? pillOn : pillOff;
        }
        if (qualityDropdown)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.SetValueWithoutNotify(quality);
            qualityDropdown.onValueChanged.RemoveAllListeners();
            qualityDropdown.onValueChanged.AddListener(SetQuality);
        }
        if (nightMinutesSlider)
        {
            nightMinutesSlider.SetValueWithoutNotify(mins);
            nightMinutesSlider.onValueChanged.RemoveAllListeners();
            nightMinutesSlider.onValueChanged.AddListener(SetNightMinutes);
        }
        RefreshLabels();
    }

    // ============================================================ Botones
    public void Play()
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(gameScene))
        {
            Debug.LogError("La escena '" + gameScene + "' no esta en Build Settings o el nombre no coincide.");
            return;
        }
        StartCoroutine(PlayRoutine());
    }

    public void OpenSettings() { ShowOnly(settingsPanel); }
    public void OpenControls() { ShowOnly(controlsPanel); }
    public void OpenCredits() { ShowOnly(creditsPanel); }
    public void Back() { ShowOnly(mainPanel); }

    public void ShowControllers() { handsMode = false; ApplyControlsMode(); }
    public void ShowHands() { handsMode = true; ApplyControlsMode(); }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ResetDefaults()
    {
        PlayerPrefs.SetFloat(GameSettings.Volume, 1f);
        PlayerPrefs.SetFloat(GameSettings.WalkSpeed, 1f);
        PlayerPrefs.SetInt(GameSettings.ArmSwing, 1);
        PlayerPrefs.SetFloat(GameSettings.NightMinutes, 10f);
        PlayerPrefs.SetInt(GameSettings.Quality, QualitySettings.GetQualityLevel());
        PlayerPrefs.Save();
        SetupUI();
    }

    /// <summary>Easter egg: toca el logo. Cada 7 toques el cuarto entra en modo fiesta.</summary>
    public void LogoPoke()
    {
        pokes++;
        logoBoost = 1f;
        PlaySfx(cChime, 0.9f + Mathf.Min(pokes % 7, 6) * 0.12f);
        Haptic(0.5f, 0.05f);
        if (logoCore) StartCoroutine(Punch(logoCore));
        if (pokes % 7 == 0)
        {
            roomBoost = 1f;
            StartCoroutine(Arpeggio());
        }
    }

    IEnumerator Punch(RectTransform rt)
    {
        float t = 0f, d = 0.35f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            rt.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(u * Mathf.PI) * (1f - u));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    IEnumerator Arpeggio()
    {
        float[] pitch = { 1f, 1.25f, 1.5f, 2f };
        foreach (float p in pitch)
        {
            PlaySfx(cChime, p);
            Haptic(0.3f, 0.03f);
            yield return new WaitForSecondsRealtime(0.09f);
        }
    }

    // ============================================================ Ajustes (se conectan solos)
    public void SetVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(GameSettings.Volume, v);
        PlayerPrefs.Save();
        RefreshLabels();
        Tick(0.8f + v * 0.8f);
    }

    public void SetWalkSpeed(float s)
    {
        PlayerPrefs.SetFloat(GameSettings.WalkSpeed, s);
        PlayerPrefs.Save();
        RefreshLabels();
        Tick(0.8f + (s - 0.5f) * 0.5f);
    }

    public void SetArmSwing(bool on)
    {
        PlayerPrefs.SetInt(GameSettings.ArmSwing, on ? 1 : 0);
        PlayerPrefs.Save();
        PlaySfx(cClick, on ? 1.25f : 0.8f);
        Haptic(0.4f, 0.04f);
    }

    public void SetQuality(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        PlayerPrefs.SetInt(GameSettings.Quality, index);
        PlayerPrefs.Save();
        PlaySfx(cClick, 1f);
    }

    public void SetNightMinutes(float m)
    {
        PlayerPrefs.SetFloat(GameSettings.NightMinutes, Mathf.Round(m));
        PlayerPrefs.Save();
        RefreshLabels();
        Tick(1f);
    }

    void Tick(float pitch)
    {
        if (Time.unscaledTime - lastTick < 0.05f) return;
        lastTick = Time.unscaledTime;
        PlaySfx(cTick, pitch);
        Haptic(0.12f, 0.015f);
    }

    void RefreshLabels()
    {
        if (volumeLabel)
            volumeLabel.text = "Volumen: " + Mathf.RoundToInt(PlayerPrefs.GetFloat(GameSettings.Volume, 1f) * 100f) + "%";
        if (walkSpeedLabel)
            walkSpeedLabel.text = "Velocidad al caminar: x" + PlayerPrefs.GetFloat(GameSettings.WalkSpeed, 1f).ToString("0.0");
        if (nightMinutesLabel)
            nightMinutesLabel.text = "Anochece en: " + Mathf.RoundToInt(PlayerPrefs.GetFloat(GameSettings.NightMinutes, 10f)) + " min";
    }

    void ApplyControlsMode()
    {
        if (controllerTexts != null) foreach (var g in controllerTexts) if (g) g.SetActive(!handsMode);
        if (handTexts != null) foreach (var g in handTexts) if (g) g.SetActive(handsMode);
        if (tabControllers) tabControllers.color = handsMode ? tabOff : tabOn;
        if (tabHands) tabHands.color = handsMode ? tabOn : tabOff;
    }

    // ============================================================ Carga con fundido
    IEnumerator PlayRoutine()
    {
        loading = true;
        PlaySfx(cStart, 1f);
        Haptic(0.7f, 0.12f);

        Image black; TMP_Text txt;
        BuildOverlay(out black, out txt);

        float t = 0f, d = 0.7f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            u = u * u * (3f - 2f * u);
            black.color = new Color(0f, 0f, 0f, u);
            introK = Mathf.Lerp(1f, 0.85f, u);
            yield return null;
        }
        black.color = Color.black;

        var op = SceneManager.LoadSceneAsync(gameScene);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            txt.text = "Cargando... " + Mathf.RoundToInt(op.progress / 0.9f * 100f) + "%";
            yield return null;
        }
        txt.text = "Listo";
        yield return new WaitForSecondsRealtime(0.25f);
        op.allowSceneActivation = true;
    }

    void BuildOverlay(out Image black, out TMP_Text txt)
    {
        var go = new GameObject("FadeOverlay", typeof(Canvas), typeof(CanvasScaler));
        var cv = go.GetComponent<Canvas>();
        var cam = Camera.main;
        if (cam)
        {
            cv.renderMode = RenderMode.ScreenSpaceCamera;
            cv.worldCamera = cam;
            cv.planeDistance = 0.5f;
        }
        else cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 999;
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);

        var im = new GameObject("Negro", typeof(RectTransform), typeof(Image));
        im.transform.SetParent(go.transform, false);
        var rt = im.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        black = im.GetComponent<Image>();
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;

        var tg = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        tg.transform.SetParent(go.transform, false);
        var trt = tg.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        txt = tg.GetComponent<TextMeshProUGUI>();
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontSize = 56;
        txt.color = new Color(0.82f, 0.88f, 1f, 1f);
        txt.text = "";
        txt.raycastTarget = false;
    }

    // ============================================================ Paneles con transicion
    GameObject[] Panels() { return new[] { mainPanel, settingsPanel, controlsPanel, creditsPanel }; }

    void ShowOnly(GameObject panel) { ShowOnly(panel, false); }

    void ShowOnly(GameObject panel, bool instant)
    {
        if (!panel || loading) return;
        if (panel == current && !instant && switching == null) return;
        if (instant || !isActiveAndEnabled)
        {
            ApplyInstant(panel);
            return;
        }
        if (switching != null) StopCoroutine(switching);
        if (stagger != null) StopCoroutine(stagger);
        switching = StartCoroutine(SwitchRoutine(panel));
    }

    void ApplyInstant(GameObject panel)
    {
        foreach (var p in Panels())
        {
            if (!p) continue;
            p.SetActive(p == panel);
            var cg = p.GetComponent<CanvasGroup>();
            if (cg) { cg.alpha = 1f; cg.interactable = true; }
            p.transform.localScale = Vector3.one;
            ((RectTransform)p.transform).anchoredPosition = Vector2.zero;
            ResetItems(p);
        }
        current = panel;
        if (isActiveAndEnabled) stagger = StartCoroutine(StaggerIn(panel));
    }

    IEnumerator SwitchRoutine(GameObject target)
    {
        bool back = target == mainPanel;
        float dir = back ? -1f : 1f;

        if (current && current != target && current.activeSelf)
        {
            yield return Fade(current, 1f, 0f, fadeOutTime, 0f, -dir * 50f, false);
            current.SetActive(false);
            ResetItems(current);
        }
        foreach (var p in Panels())
            if (p && p != target) p.SetActive(false);

        current = target;
        ResetItems(target);
        target.SetActive(true);
        stagger = StartCoroutine(StaggerIn(target));
        yield return Fade(target, 0f, 1f, fadeInTime, dir * 50f, 0f, true);
        switching = null;
    }

    IEnumerator Fade(GameObject go, float from, float to, float dur, float slideFrom, float slideTo, bool pop)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        var rt = (RectTransform)go.transform;
        cg.interactable = false;
        cg.alpha = from;
        rt.anchoredPosition = new Vector2(slideFrom, 0f);
        if (pop) go.transform.localScale = Vector3.one * 0.95f;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = k * k * (3f - 2f * k);
            cg.alpha = Mathf.Lerp(from, to, k);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(slideFrom, slideTo, k), 0f);
            if (pop) go.transform.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, k);
            yield return null;
        }
        cg.alpha = to;
        cg.interactable = to > 0.5f;
        rt.anchoredPosition = new Vector2(slideTo, 0f);
        if (pop) go.transform.localScale = Vector3.one;
    }

    // hijos del panel (los que entran al layout) aparecen uno tras otro
    List<Transform> LayoutChildren(GameObject panel)
    {
        var list = new List<Transform>();
        foreach (Transform c in panel.transform)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le && le.ignoreLayout) continue;
            list.Add(c);
        }
        return list;
    }

    void SetItem(Transform c, float k)
    {
        var cg = c.GetComponent<CanvasGroup>();
        if (!cg) cg = c.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = k;
        float s = Mathf.LerpUnclamped(0.82f, 1f, k);
        var fx = c.GetComponent<UIHoverFX>();
        if (fx) fx.entrance = s; else c.localScale = Vector3.one * s;
    }

    void ResetItems(GameObject panel)
    {
        if (!panel) return;
        foreach (var c in LayoutChildren(panel)) SetItem(c, 1f);
    }

    IEnumerator StaggerIn(GameObject panel, bool own = true)
    {
        var items = LayoutChildren(panel);
        const float step = 0.045f, dur = 0.24f;
        foreach (var c in items) SetItem(c, 0f);

        float t = 0f, total = dur + step * items.Count;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < items.Count; i++)
            {
                float u = Mathf.Clamp01((t - i * step) / dur);
                SetItem(items[i], u <= 0f ? 0f : EaseOutBack(u));
            }
            yield return null;
        }
        foreach (var c in items) SetItem(c, 1f);
        if (own) stagger = null;
    }

    // ============================================================ FX de botones, tarjetas y sliders
    void SetupFX()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        cHover = hoverSound ? hoverSound : Synth("hover", 900f, 1250f, 0.07f, 0.35f, 0.15f, 28f);
        cClick = clickSound ? clickSound : Synth("click", 520f, 760f, 0.11f, 0.55f, 0.35f, 14f);
        cBack = Synth("back", 700f, 380f, 0.12f, 0.5f, 0.3f, 14f);
        cTick = Synth("tick", 1500f, 1500f, 0.03f, 0.3f, 0f, 60f);
        cChime = Synth("chime", 1318f, 1318f, 0.45f, 0.5f, 0.45f, 6f);
        cStart = Synth("start", 330f, 990f, 0.5f, 0.55f, 0.4f, 4f);

        var roots = new List<GameObject>(Panels());
        if (sidebar) roots.Add(sidebar);
        foreach (var p in roots)
        {
            if (!p) continue;

            foreach (var b in p.GetComponentsInChildren<Button>(true))
            {
                float sc = b.name == "Logo Boton" ? 1.12f : (b.name.StartsWith("Nav") ? 1.035f : hoverScale);
                AddFX(b.gameObject, sc, true, FindGlow(b.transform));
                var btn = b;
                btn.onClick.AddListener(() =>
                {
                    PlaySfx(btn.name.Contains("Volver") ? cBack : cClick, 1f);
                    Haptic(0.35f, 0.04f);
                });
                var shine = b.transform.Find("Brillo") as RectTransform;
                if (shine) shines.Add(shine);
            }

            foreach (var tr in p.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("Tarjeta"))
                    AddFX(tr.gameObject, 1.025f, false, FindGlow(tr));

            foreach (var s in p.GetComponentsInChildren<Slider>(true))
                if (s.handleRect) AddFX(s.handleRect.gameObject, 1.25f, false, null);

            foreach (var tg in p.GetComponentsInChildren<Toggle>(true))
                AddFX(tg.gameObject, 1.03f, false, null);
            foreach (var dd in p.GetComponentsInChildren<TMP_Dropdown>(true))
                AddFX(dd.gameObject, 1.03f, false, null);
        }
    }

    static Graphic FindGlow(Transform t)
    {
        var g = t.Find("Borde");
        return g ? g.GetComponent<Graphic>() : null;
    }

    UIHoverFX AddFX(GameObject go, float scale, bool press, Graphic glow)
    {
        var fx = go.GetComponent<UIHoverFX>();
        if (!fx) fx = go.AddComponent<UIHoverFX>();
        fx.hoverScale = scale;
        fx.usePress = press;
        fx.SetGlow(glow);
        fx.onEnter = () => { PlaySfx(cHover, 1f); Haptic(0.12f, 0.02f); };
        return fx;
    }

    void PlaySfx(AudioClip clip, float pitch)
    {
        if (!sfx || !clip) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, sfxVolume);
    }

    void Haptic(float amp, float dur)
    {
        if (!haptics || !XRSettings.isDeviceActive) return;
        devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        foreach (var d in devices)
        {
            HapticCapabilities cap;
            if (d.TryGetHapticCapabilities(out cap) && cap.supportsImpulse)
                d.SendHapticImpulse(0, amp, dur);
        }
    }

    // sonido generado por codigo (seno + armonico con envolvente)
    static AudioClip Synth(string name, float f0, float f1, float dur, float vol, float harmonic, float decay)
    {
        const int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * dur));
        var data = new float[n];
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, u) / sr;
            float env = Mathf.Min(1f, i / (sr * 0.004f)) * Mathf.Exp(-decay * u);
            data[i] = (Mathf.Sin(ph) + harmonic * Mathf.Sin(ph * 2f)) * env * vol;
        }
        var clip = AudioClip.Create(name, n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
