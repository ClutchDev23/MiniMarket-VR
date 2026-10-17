// COLOCAR EN: Assets/Editor/MenuBuilder.cs  (reemplaza el anterior)
// ICONOS: copia la carpeta "Icons" a Assets/Icons (los PNG se configuran solos como Sprite)
// Crea el menu VR de "Cajero XR": Tools > MiniMarket > Crear Menu VR
// Si ya existe MenuCanvas / MenuManager, los reemplaza.
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class MenuBuilder
{
    // ---------- Iconos (PNG en Assets/Icons) ----------
    const string IconFolder = "Assets/Icons/";

    static Sprite Icon(string name)
    {
        string path = IconFolder + name + ".png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null)
        {
            Debug.LogWarning("Falta el icono: " + path + " (copia la carpeta Icons a Assets/)");
            return null;
        }
        if (imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static GameObject MakeIcon(Transform parent, string icon, float size, Color color)
    {
        var go = new GameObject("Icono " + icon, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = Icon(icon);
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = size; le.minWidth = size;
        le.preferredHeight = size; le.minHeight = size;
        return go;
    }

    // Fila: icono + texto, centrados
    static TMP_Text MakeRow(Transform parent, string icon, string text, float fontSize, float height,
        Color color, bool bold = false, float iconSize = 0f)
    {
        if (iconSize <= 0f) iconSize = height * 0.85f;
        var row = new GameObject("Fila " + icon, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        Size(row, height);
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 14;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        MakeIcon(row.transform, icon, iconSize, color);

        var go = TMP_DefaultControls.CreateText(TmpRes());
        go.name = "Texto";
        go.transform.SetParent(row.transform, false);
        var t = go.GetComponent<TMP_Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    // ---------- Textos (cambialos aqui) ----------
    const string GameName = "Cajero XR";
    const string Subtitle = "Realidad virtual · OpenXR";
    const string Version = "Cajero XR · v1.0";
    const string Dev1 = "Abner Gonzales";
    const string Dev2 = "Hubert Hilaario";

    // ---------- Paleta (cambia estos colores para personalizar) ----------
    static readonly Color PanelColor   = new Color(0.06f, 0.08f, 0.15f, 0.95f);
    static readonly Color Accent       = new Color(0.30f, 0.78f, 1.00f, 1f);
    static readonly Color BtnMain      = new Color(0.16f, 0.45f, 0.95f, 1f);
    static readonly Color BtnSecondary = new Color(0.25f, 0.30f, 0.42f, 1f);
    static readonly Color BtnDanger    = new Color(0.80f, 0.22f, 0.28f, 1f);
    static readonly Color TextSoft     = new Color(0.82f, 0.88f, 1.00f, 1f);
    static readonly Color TextDim      = new Color(0.55f, 0.62f, 0.78f, 1f);
    static readonly Color Track        = new Color(0.14f, 0.17f, 0.27f, 1f);

    // ---------- Recursos de UI integrados de Unity ----------
    static Sprite Builtin(string path) { return AssetDatabase.GetBuiltinExtraResource<Sprite>(path); }

    static DefaultControls.Resources UiRes()
    {
        var r = new DefaultControls.Resources();
        r.standard = Builtin("UI/Skin/UISprite.psd");
        r.background = Builtin("UI/Skin/Background.psd");
        r.inputField = Builtin("UI/Skin/InputFieldBackground.psd");
        r.knob = Builtin("UI/Skin/Knob.psd");
        r.checkmark = Builtin("UI/Skin/Checkmark.psd");
        r.dropdown = Builtin("UI/Skin/DropdownArrow.psd");
        r.mask = Builtin("UI/Skin/UIMask.psd");
        return r;
    }

    static TMP_DefaultControls.Resources TmpRes()
    {
        var r = new TMP_DefaultControls.Resources();
        r.standard = Builtin("UI/Skin/UISprite.psd");
        r.background = Builtin("UI/Skin/Background.psd");
        r.inputField = Builtin("UI/Skin/InputFieldBackground.psd");
        r.knob = Builtin("UI/Skin/Knob.psd");
        r.checkmark = Builtin("UI/Skin/Checkmark.psd");
        r.dropdown = Builtin("UI/Skin/DropdownArrow.psd");
        r.mask = Builtin("UI/Skin/UIMask.psd");
        return r;
    }

    static string Hex(Color c) { return ColorUtility.ToHtmlStringRGB(c); }

    // ---------- Ayudas ----------
    static void Size(GameObject go, float h)
    {
        var le = go.GetComponent<LayoutElement>();
        if (!le) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight = h;
    }

    static GameObject MakePanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = Builtin("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = PanelColor;

        var ol = go.GetComponent<Outline>();
        ol.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.55f);
        ol.effectDistance = new Vector2(3f, -3f);

        var v = go.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(50, 50, 40, 40);
        v.spacing = 14;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return go;
    }

    static TMP_Text MakeLabel(Transform parent, string text, float fontSize, float height, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
    {
        var go = TMP_DefaultControls.CreateText(TmpRes());
        go.name = "Texto";
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TMP_Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        if (bold) t.fontStyle = FontStyles.Bold;
        Size(go, height);
        return t;
    }

    static void MakeLine(Transform parent)
    {
        var line = new GameObject("Linea", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);
        line.GetComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.6f);
        Size(line, 4);
    }

    static void MakeTitle(Transform parent, string text, float fontSize, float height, string icon = null)
    {
        if (icon == null) MakeLabel(parent, text, fontSize, height, Accent, TextAlignmentOptions.Center, true);
        else MakeRow(parent, icon, text, fontSize, height, Accent, true);
        MakeLine(parent);
    }

    // Logo circular con "XR"
    static void MakeBadge(Transform parent)
    {
        var holder = new GameObject("Logo", typeof(RectTransform), typeof(LayoutElement));
        holder.transform.SetParent(parent, false);
        Size(holder, 112);

        var circle = new GameObject("Circulo", typeof(RectTransform), typeof(Image), typeof(Outline));
        circle.transform.SetParent(holder.transform, false);
        var rt = circle.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(104f, 104f);
        var img = circle.GetComponent<Image>();
        img.sprite = Builtin("UI/Skin/Knob.psd");
        img.color = BtnMain;
        var ol = circle.GetComponent<Outline>();
        ol.effectColor = Accent;
        ol.effectDistance = new Vector2(4f, -4f);

        var txt = TMP_DefaultControls.CreateText(TmpRes());
        txt.name = "XR";
        txt.transform.SetParent(circle.transform, false);
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var t = txt.GetComponent<TMP_Text>();
        t.text = "XR";
        t.fontSize = 46;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
    }

    static Button MakeButton(Transform parent, string icon, string text, UnityAction onClick, Color baseColor)
    {
        var go = TMP_DefaultControls.CreateButton(TmpRes());
        go.name = "Boton " + text;
        go.transform.SetParent(parent, false);

        var label = go.GetComponentInChildren<TMP_Text>();
        label.text = text;
        label.fontSize = 34;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;

        go.GetComponent<Image>().color = Color.white;
        var b = go.GetComponent<Button>();
        var cb = b.colors;
        cb.normalColor = baseColor;
        cb.highlightedColor = Color.Lerp(baseColor, Color.white, 0.28f);
        cb.pressedColor = Color.Lerp(baseColor, Color.black, 0.30f);
        cb.selectedColor = baseColor;
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        cb.fadeDuration = 0.1f;
        b.colors = cb;

        Size(go, 72);

        if (icon != null)
        {
            var ic = MakeIcon(go.transform, icon, 44f, Color.white);
            ic.GetComponent<LayoutElement>().ignoreLayout = true;
            var irt = ic.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(44f, 44f);
            irt.anchoredPosition = new Vector2(48f, 0f);
        }
        UnityEventTools.AddPersistentListener(b.onClick, onClick);
        return b;
    }

    static Slider MakeSlider(Transform parent, string name, float min, float max, float value)
    {
        var go = DefaultControls.CreateSlider(UiRes());
        go.name = name;
        go.transform.SetParent(parent, false);
        var s = go.GetComponent<Slider>();
        s.minValue = min;
        s.maxValue = max;
        s.value = value;

        var bg = go.transform.Find("Background");
        if (bg) bg.GetComponent<Image>().color = Track;
        if (s.fillRect) s.fillRect.GetComponent<Image>().color = Accent;
        if (s.handleRect)
        {
            s.handleRect.GetComponent<Image>().color = Color.white;
            s.handleRect.sizeDelta = new Vector2(36f, 0f);
        }
        Size(go, 44);
        return s;
    }

    // ---------- Construccion ----------
    [MenuItem("Tools/MiniMarket/Crear Menu VR")]
    static void Build()
    {
        if (!EditorUtility.DisplayDialog("Crear menu VR",
            "Si ya existen MenuCanvas o MenuManager en la escena, se van a reemplazar. ¿Continuar?",
            "Continuar", "Cancelar")) return;

        foreach (var n in new[] { "MenuCanvas", "MenuManager" })
        {
            var old = GameObject.Find(n);
            if (old) Undo.DestroyObjectImmediate(old);
        }

        // Canvas en World Space, listo para VR
        var canvasGO = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Crear Menu VR");
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        canvasGO.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        var crt = canvasGO.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(800f, 900f);
        crt.position = new Vector3(0f, 1.5f, 1.6f);
        crt.localScale = Vector3.one * 0.0016f;

        var mgrGO = new GameObject("MenuManager");
        Undo.RegisterCreatedObjectUndo(mgrGO, "Crear MenuManager");
        var menu = mgrGO.AddComponent<MainMenu>();
        menu.gameScene = "MinimarketXR_Full";

        // ----- Panel principal -----
        var main = MakePanel(canvasGO.transform, "MainPanel");
        MakeBadge(main.transform);
        MakeTitle(main.transform, GameName, 76, 100);
        MakeRow(main.transform, "headset", Subtitle, 28, 40, TextSoft, false, 34f);
        MakeButton(main.transform, "play", "Jugar", menu.Play, BtnMain);
        MakeButton(main.transform, "settings", "Ajustes", menu.OpenSettings, BtnSecondary);
        MakeButton(main.transform, "controls", "Controles", menu.OpenControls, BtnSecondary);
        MakeButton(main.transform, "star", "Créditos", menu.OpenCredits, BtnSecondary);
        MakeButton(main.transform, "exit", "Salir", menu.Quit, BtnDanger);
        MakeLabel(main.transform, Version, 22, 30, TextDim);

        // ----- Panel de ajustes -----
        var settings = MakePanel(canvasGO.transform, "SettingsPanel");
        MakeTitle(settings.transform, "Ajustes", 56, 80, "settings");
        var volLabel = MakeRow(settings.transform, "volume", "Volumen: 100%", 30, 40, TextSoft, false, 34f);
        var volSlider = MakeSlider(settings.transform, "Slider Volumen", 0f, 1f, 1f);
        var walkLabel = MakeRow(settings.transform, "walk", "Velocidad al caminar: x1.0", 30, 40, TextSoft, false, 34f);
        var walkSlider = MakeSlider(settings.transform, "Slider Velocidad", 0.5f, 2f, 1f);

        var toggleGO = DefaultControls.CreateToggle(UiRes());
        toggleGO.name = "Toggle Caminar con brazos";
        toggleGO.transform.SetParent(settings.transform, false);
        var toggle = toggleGO.GetComponent<Toggle>();
        toggle.isOn = true;
        var tbg = toggleGO.transform.Find("Background");
        if (tbg)
        {
            tbg.GetComponent<Image>().color = Track;
            var trt = tbg.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(38f, 38f);
            trt.anchoredPosition = new Vector2(22f, -22f);
        }
        if (toggle.graphic)
        {
            toggle.graphic.color = Accent;
            toggle.graphic.rectTransform.sizeDelta = new Vector2(28f, 28f);
        }
        var toggleText = toggleGO.GetComponentInChildren<Text>();
        toggleText.text = "Caminar con brazos";
        toggleText.fontSize = 30;
        toggleText.color = TextSoft;
        toggleText.rectTransform.offsetMin = new Vector2(56f, toggleText.rectTransform.offsetMin.y);
        Size(toggleGO, 48);

        MakeRow(settings.transform, "palette", "Calidad gráfica", 30, 40, TextSoft, false, 34f);
        var ddGO = TMP_DefaultControls.CreateDropdown(TmpRes());
        ddGO.name = "Dropdown Calidad";
        ddGO.transform.SetParent(settings.transform, false);
        var dd = ddGO.GetComponent<TMP_Dropdown>();
        ddGO.GetComponent<Image>().color = Track;
        dd.captionText.fontSize = 28;
        dd.captionText.color = Color.white;
        dd.itemText.fontSize = 22;
        var arrow = ddGO.transform.Find("Arrow");
        if (arrow) arrow.GetComponent<Image>().color = Color.white;
        Size(ddGO, 54);

        MakeButton(settings.transform, "reset", "Restablecer", menu.ResetDefaults, BtnSecondary);
        MakeButton(settings.transform, "back", "Volver", menu.Back, BtnMain);

        // ----- Panel de controles -----
        var controls = MakePanel(canvasGO.transform, "ControlsPanel");
        MakeTitle(controls.transform, "Controles", 56, 80, "controls");
        MakeRow(controls.transform, "grab", "AGARRAR", 30, 44, Accent, true, 38f);
        MakeLabel(controls.transform,
            "Controles: toca el objeto y aprieta Grip.\nManos: toca el objeto y cierra la mano.",
            28, 80, TextSoft);
        MakeRow(controls.transform, "walk", "CAMINAR", 30, 44, Accent, true, 38f);
        MakeLabel(controls.transform,
            "Controles: joystick izquierdo.\nManos: estira los brazos y muévelos.",
            28, 80, TextSoft);
        MakeRow(controls.transform, "rotate", "GIRAR", 30, 44, Accent, true, 38f);
        MakeLabel(controls.transform,
            "Controles: joystick derecho.\nManos: gira tu cuerpo.",
            28, 80, TextSoft);
        MakeButton(controls.transform, "back", "Volver", menu.Back, BtnMain);

        // ----- Panel de creditos -----
        var credits = MakePanel(canvasGO.transform, "CreditsPanel");
        MakeTitle(credits.transform, "Créditos", 56, 80, "star");
        MakeRow(credits.transform, "store", "DESARROLLADO POR", 26, 36, Accent, true, 30f);
        MakeLabel(credits.transform, "<b>" + Dev1 + "</b>\n<b>" + Dev2 + "</b>", 44, 110, TextSoft);
        MakeRow(credits.transform, "box", "MODELOS 3D", 26, 36, Accent, true, 30f);
        MakeLabel(credits.transform, "Supermarket Props 3D Models Collection", 28, 40, TextSoft);
        MakeRow(credits.transform, "tools", "TECNOLOGÍA", 26, 36, Accent, true, 30f);
        MakeLabel(credits.transform, "Unity 6 · OpenXR · XR Interaction Toolkit", 28, 40, TextSoft);
        MakeRow(credits.transform, "headset", "PLATAFORMA", 26, 36, Accent, true, 30f);
        MakeLabel(credits.transform, "Meta Quest", 28, 40, TextSoft);
        MakeLabel(credits.transform, GameName + " · 2026", 24, 34, TextDim);
        MakeButton(credits.transform, "back", "Volver", menu.Back, BtnMain);

        // ----- Conectar referencias -----
        menu.mainPanel = main;
        menu.settingsPanel = settings;
        menu.controlsPanel = controls;
        menu.creditsPanel = credits;
        menu.volumeSlider = volSlider;
        menu.volumeLabel = volLabel;
        menu.walkSpeedSlider = walkSlider;
        menu.walkSpeedLabel = walkLabel;
        menu.armSwingToggle = toggle;
        menu.qualityDropdown = dd;

        settings.SetActive(false);
        controls.SetActive(false);
        credits.SetActive(false);

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
        {
            if (!EditorApplication.ExecuteMenuItem("GameObject/XR/UI Event System"))
                Debug.LogWarning("Crea el EventSystem a mano: GameObject > XR > UI Event System");
        }

        Selection.activeGameObject = canvasGO;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Menu VR de " + GameName + " creado. Revisa Game Scene en MenuManager y guarda la escena.");
    }
}
