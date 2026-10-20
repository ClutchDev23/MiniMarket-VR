// COLOCAR EN: Assets/Editor/MenuBuilder.cs  (reemplaza el anterior)
// ICONOS: usa la carpeta Assets/Icons de siempre (los PNG se configuran solos como Sprite)
//
// Tools > MiniMarket > Crear Menu VR         -> menu + cuarto (o solo el menu, te pregunta)
// Tools > MiniMarket > Crear Cuarto del Menu -> solo el cuarto (y lo conecta al MainMenu si existe)
//
// Si ya existen MenuCanvas / MenuManager / MenuRoom, los reemplaza.
// DISENO: pantalla ancha synthwave -> barra lateral + paginas.
// Los sprites redondeados, brillos y anillos se generan solos en Assets/MenuRoom/Sprites.
using System.IO;
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
    // =====================================================================
    //  TEXTOS (cambialos aqui)
    // =====================================================================
    const string GameName = "Cajero XR";
    const string Subtitle = "Realidad virtual · OpenXR";
    const string Version  = "Cajero XR · v1.0";
    const string Dev1 = "Abner Gonzales";
    const string Dev2 = "Hubert Hilario";

    // =====================================================================
    //  PALETA (cambia estos colores para personalizar)
    // =====================================================================
    static readonly Color PanelColor   = new Color(0.07f, 0.04f, 0.15f, 0.84f);
    static readonly Color CardColor    = new Color(0.13f, 0.08f, 0.26f, 0.95f);
    static readonly Color Accent       = new Color(0.27f, 0.93f, 1.00f, 1f);   // cian neon
    static readonly Color AccentGreen  = new Color(0.40f, 1.00f, 0.70f, 1f);
    static readonly Color AccentAmber  = new Color(1.00f, 0.78f, 0.30f, 1f);
    static readonly Color AccentPink   = new Color(1.00f, 0.28f, 0.66f, 1f);
    static readonly Color BtnMain      = new Color(1.00f, 0.25f, 0.60f, 1f);   // rosa neon
    static readonly Color BtnSecondary = new Color(0.22f, 0.14f, 0.40f, 1f);
    static readonly Color BtnDanger    = new Color(0.78f, 0.20f, 0.30f, 1f);
    static readonly Color TextSoft     = new Color(0.93f, 0.89f, 1.00f, 1f);
    static readonly Color TextDim      = new Color(0.62f, 0.56f, 0.80f, 1f);
    static readonly Color Track        = new Color(0.17f, 0.11f, 0.32f, 1f);
    static readonly Color FogColor     = new Color(0.018f, 0.012f, 0.035f, 1f);

    // =====================================================================
    //  CUARTO: medidas (metros)
    // =====================================================================
    const float RoomW = 6f;
    const float RoomL = 6f;
    const float RoomH = 3f;
    const string RoomFolder = "Assets/MenuRoom";
    const string SpriteFolder = "Assets/MenuRoom/Sprites/";

    // lo que BuildRoom entrega para conectar al MainMenu
    static Transform[] lastFloaters, lastBob;
    static Renderer[] lastNeon;

    // =====================================================================
    //  SPRITES GENERADOS POR CODIGO (bordes redondeados, brillos, anillos)
    // =====================================================================
    static Sprite sRound, sRing, sGlow, sCircle, sDash, sArcs, sGrad, sDot;

    // distancia con signo a un rectangulo redondeado centrado (negativo = adentro)
    static float SdRound(float px, float py, float half, float r)
    {
        float qx = Mathf.Abs(px) - (half - r), qy = Mathf.Abs(py) - (half - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static Sprite SaveSprite(string name, int w, int h, System.Func<float, float, float> alpha, Vector4 border)
    {
        Directory.CreateDirectory(SpriteFolder);
        string path = SpriteFolder + name + ".png";
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color(1f, 1f, 1f, alpha(x + 0.5f, y + 0.5f));
        t.SetPixels(px);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = border;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureSprites()
    {
        var b44 = new Vector4(44, 44, 44, 44);

        sRound = SaveSprite("Redondo", 128, 128,
            (x, y) => Mathf.Clamp01(0.5f - SdRound(x - 64f, y - 64f, 64f, 40f)), b44);

        sRing = SaveSprite("Borde", 128, 128, (x, y) =>
        {
            float d = SdRound(x - 64f, y - 64f, 64f, 40f);
            return Mathf.Clamp01(0.5f - (Mathf.Abs(d + 2f) - 2f));
        }, b44);

        sGlow = SaveSprite("Resplandor", 128, 128, (x, y) =>
        {
            float d = SdRound(x - 64f, y - 64f, 34f, 26f);
            float a = Mathf.Clamp01(1f - Mathf.Max(d, 0f) / 30f);
            return a * a;
        }, new Vector4(60, 60, 60, 60));

        sCircle = SaveSprite("Circulo", 128, 128, (x, y) =>
        {
            float r = Mathf.Sqrt((x - 64f) * (x - 64f) + (y - 64f) * (y - 64f));
            return Mathf.Clamp01(0.5f - (r - 62f));
        }, Vector4.zero);

        // anillo de guiones (se nota al girar)
        sDash = SaveSprite("AnilloGuiones", 128, 128, (x, y) =>
        {
            float dx = x - 64f, dy = y - 64f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float band = Mathf.Clamp01(0.5f - (Mathf.Abs(r - 58f) - 3f));
            float seg = Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI) * 18f, 1f) < 0.55f ? 1f : 0f;
            return band * seg;
        }, Vector4.zero);

        // tres arcos
        sArcs = SaveSprite("AnilloArcos", 128, 128, (x, y) =>
        {
            float dx = x - 64f, dy = y - 64f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float band = Mathf.Clamp01(0.5f - (Mathf.Abs(r - 60f) - 3.5f));
            float seg = Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI) * 3f, 1f) < 0.38f ? 1f : 0f;
            return band * seg;
        }, Vector4.zero);

        // degradado vertical (arriba opaco, abajo transparente)
        sGrad = SaveSprite("Degradado", 4, 64, (x, y) => y / 64f, Vector4.zero);

        // punto suave (brillos y polvo)
        sDot = SaveSprite("Punto", 64, 64, (x, y) =>
        {
            float r = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 32f) * (y - 32f)) / 32f;
            float a = Mathf.Clamp01(1f - r);
            return a * a;
        }, Vector4.zero);
    }


    // =====================================================================
    //  ICONOS (PNG en Assets/Icons)
    // =====================================================================
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
        img.enabled = img.sprite != null;   // sin icono no se dibuja el cuadro blanco
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = size; le.minWidth = size;
        le.preferredHeight = size; le.minHeight = size;
        return go;
    }

    // =====================================================================
    //  RECURSOS DE UI INTEGRADOS DE UNITY
    // =====================================================================
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

    // =====================================================================
    //  AYUDAS DE UI
    // =====================================================================
    static void Size(GameObject go, float h)
    {
        var le = go.GetComponent<LayoutElement>();
        if (!le) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight = h;
    }

    static void MakeSpacer(Transform parent, float h)
    {
        var go = new GameObject("Espacio", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Size(go, h);
    }

    // =====================================================================
    //  AYUDAS DE UI (estilo nuevo)
    // =====================================================================
    // Imagen decorativa que no entra al layout (bordes, brillos, sombras)
    static Image AddDecor(Transform parent, string name, Sprite sprite, Color color, Image.Type type, bool stretch = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null ? type : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        if (stretch)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        go.transform.SetAsFirstSibling();
        return img;
    }

    static Image Center(Transform parent, string name, Sprite s, Color c, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    // Tarjeta con barra de color a la izquierda y borde que brilla en hover
    static GameObject MakeCard(Transform parent, string name, Color barColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sRound;
        img.type = Image.Type.Sliced;
        img.color = CardColor;
        img.raycastTarget = true;   // necesario para detectar hover

        var v = go.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(38, 26, 18, 18);
        v.spacing = 8;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        var bar = new GameObject("Barra", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bar.transform.SetParent(go.transform, false);
        bar.GetComponent<LayoutElement>().ignoreLayout = true;
        bar.GetComponent<Image>().color = barColor;
        bar.GetComponent<Image>().raycastTarget = false;
        var brt = bar.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(8f, -32f);
        brt.anchoredPosition = new Vector2(14f, 0f);

        AddDecor(go.transform, "Borde", sRing, new Color(barColor.r, barColor.g, barColor.b, 0.22f), Image.Type.Sliced);
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
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        Size(go, height);
        return t;
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
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    static void MakeLine(Transform parent)
    {
        var line = new GameObject("Linea", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);
        var img = line.GetComponent<Image>();
        img.color = new Color(Accent.r, Accent.g, Accent.b, 0.6f);
        img.raycastTarget = false;
        Size(line, 3);
    }

    static void MakeTitle(Transform parent, string text, float fontSize, float height, string icon = null, bool gradient = false)
    {
        TMP_Text t;
        if (icon == null) t = MakeLabel(parent, text, fontSize, height, Accent, TextAlignmentOptions.Center, true);
        else t = MakeRow(parent, icon, text, fontSize, height, Accent, true);
        if (gradient)
        {
            t.color = Color.white;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(Color.white, Color.white, Accent, Accent);
        }
        MakeLine(parent);
    }

    // Logo: brillo pulsante, dos anillos que giran y nucleo tocable (easter egg)
    static Image MakeBadge(Transform parent, MainMenu menu)
    {
        var holder = new GameObject("Logo", typeof(RectTransform), typeof(LayoutElement));
        holder.transform.SetParent(parent, false);
        Size(holder, 136);

        var glow = Center(holder.transform, "Resplandor", sDot, new Color(Accent.r, Accent.g, Accent.b, 0.45f), 230f);
        var ringA = Center(holder.transform, "Anillo Guiones", sDash, new Color(Accent.r, Accent.g, Accent.b, 0.95f), 138f);
        var ringB = Center(holder.transform, "Anillo Arcos", sArcs, new Color(AccentPink.r, AccentPink.g, AccentPink.b, 0.9f), 120f);
        var core = Center(holder.transform, "Logo Boton", sCircle, BtnMain, 100f);
        core.raycastTarget = true;

        var btn = core.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = core;
        UnityEventTools.AddPersistentListener(btn.onClick, menu.LogoPoke);

        var txt = TMP_DefaultControls.CreateText(TmpRes());
        txt.name = "XR";
        txt.transform.SetParent(core.transform, false);
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var t = txt.GetComponent<TMP_Text>();
        t.text = "XR";
        t.fontSize = 46;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;

        menu.logoCore = core.rectTransform;
        menu.logoRings = new[] { ringA.rectTransform, ringB.rectTransform };
        return glow;
    }

    static Transform MakeButtonRow(Transform parent, float height)
    {
        var row = new GameObject("Fila botones", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        Size(row, height);
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 16;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        return row.transform;
    }

    static Button MakeButton(Transform parent, string icon, string text, UnityAction onClick, Color baseColor,
        float height = 72f, float fontSize = 34f, bool shine = false)
    {
        var go = TMP_DefaultControls.CreateButton(TmpRes());
        go.name = "Boton " + text;
        go.transform.SetParent(parent, false);

        var label = go.GetComponentInChildren<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;

        var img = go.GetComponent<Image>();
        img.sprite = sRound;
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        var b = go.GetComponent<Button>();
        var cb = b.colors;
        cb.normalColor = baseColor;
        cb.highlightedColor = Color.Lerp(baseColor, Color.white, 0.25f);
        cb.pressedColor = Color.Lerp(baseColor, Color.black, 0.30f);
        cb.selectedColor = baseColor;
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.45f);
        sh.effectDistance = new Vector2(0f, -6f);

        Size(go, height);

        // borde claro que brilla en hover
        var lite = Color.Lerp(baseColor, Color.white, 0.55f);
        AddDecor(go.transform, "Borde", sRing, new Color(lite.r, lite.g, lite.b, 0.30f), Image.Type.Sliced);

        if (shine)
        {
            go.AddComponent<RectMask2D>();
            var s = AddDecor(go.transform, "Brillo", null, new Color(1f, 1f, 1f, 0f), Image.Type.Simple, false);
            var srt = s.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(70f, height * 3f);
            srt.localRotation = Quaternion.Euler(0f, 0f, 20f);
            s.transform.SetSiblingIndex(go.transform.childCount - 2);   // encima del fondo, debajo del texto
        }

        if (icon != null)
        {
            float isz = Mathf.Min(44f, height * 0.6f);
            var ic = MakeIcon(go.transform, icon, isz, Color.white);
            ic.GetComponent<LayoutElement>().ignoreLayout = true;
            var irt = ic.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(isz, isz);
            irt.anchoredPosition = new Vector2(isz + 10f, 0f);
        }
        UnityEventTools.AddPersistentListener(b.onClick, onClick);
        return b;
    }

    // Pestana (tab): color lo maneja MainMenu segun cual este activa
    static Button MakeTab(Transform parent, string icon, string text, UnityAction onClick, Color tint)
    {
        var b = MakeButton(parent, icon, text, onClick, Color.white, 64f, 28f);
        b.GetComponent<Image>().color = tint;
        var cb = b.colors;
        cb.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.selectedColor = cb.normalColor;
        b.colors = cb;
        return b;
    }

    static Slider MakeSlider(Transform parent, string name, float min, float max, float value, Color fill)
    {
        var go = DefaultControls.CreateSlider(UiRes());
        go.name = name;
        go.transform.SetParent(parent, false);
        var s = go.GetComponent<Slider>();
        s.minValue = min;
        s.maxValue = max;
        s.value = value;

        var bg = go.transform.Find("Background");
        if (bg)
        {
            var bi = bg.GetComponent<Image>();
            bi.sprite = sRound; bi.type = Image.Type.Sliced; bi.color = Track;
        }
        if (s.fillRect)
        {
            var fi = s.fillRect.GetComponent<Image>();
            fi.sprite = sRound; fi.type = Image.Type.Sliced; fi.color = fill;
        }
        var area = go.transform.Find("Handle Slide Area") as RectTransform;
        if (area) { area.offsetMin = new Vector2(22f, 0f); area.offsetMax = new Vector2(-22f, 0f); }
        if (s.handleRect)
        {
            var hi = s.handleRect.GetComponent<Image>();
            hi.sprite = sCircle; hi.type = Image.Type.Simple; hi.color = Color.white;
            s.handleRect.sizeDelta = new Vector2(44f, 0f);
            var hs = s.handleRect.gameObject.AddComponent<Shadow>();
            hs.effectColor = new Color(fill.r, fill.g, fill.b, 0.55f);
            hs.effectDistance = new Vector2(0f, -4f);
        }
        Size(go, 44);
        return s;
    }

    // Interruptor tipo "switch": MainMenu anima la bolita y el color
    static Toggle MakeSwitch(Transform parent, string label, out Image pill, out RectTransform knob)
    {
        var go = new GameObject("Toggle " + label, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Size(go, 56);
        var hit = go.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        var tg = go.GetComponent<Toggle>();
        tg.transition = Selectable.Transition.None;
        tg.targetGraphic = hit;
        tg.isOn = true;

        var txt = TMP_DefaultControls.CreateText(TmpRes());
        txt.name = "Texto";
        txt.transform.SetParent(go.transform, false);
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = new Vector2(-120f, 0f);
        var t = txt.GetComponent<TMP_Text>();
        t.text = label;
        t.fontSize = 30;
        t.color = TextSoft;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false;

        var p = new GameObject("Pill", typeof(RectTransform), typeof(Image));
        p.transform.SetParent(go.transform, false);
        var prt = p.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.5f);
        prt.pivot = new Vector2(1f, 0.5f);
        prt.sizeDelta = new Vector2(96f, 48f);
        prt.anchoredPosition = Vector2.zero;
        pill = p.GetComponent<Image>();
        pill.sprite = sRound; pill.type = Image.Type.Sliced; pill.color = AccentGreen;

        var kn = new GameObject("Knob", typeof(RectTransform), typeof(Image));
        kn.transform.SetParent(p.transform, false);
        knob = kn.GetComponent<RectTransform>();
        knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
        knob.sizeDelta = new Vector2(38f, 38f);
        knob.anchoredPosition = new Vector2(24f, 0f);
        var ki = kn.GetComponent<Image>();
        ki.sprite = sCircle; ki.color = Color.white; ki.raycastTarget = false;
        return tg;
    }

    static void StyleDropdown(TMP_Dropdown dd)
    {
        var img = dd.GetComponent<Image>();
        img.sprite = sRound; img.type = Image.Type.Sliced; img.color = Track;
        dd.captionText.fontSize = 28;
        dd.captionText.color = Color.white;
        dd.itemText.fontSize = 22;
        var arrow = dd.transform.Find("Arrow");
        if (arrow) arrow.GetComponent<Image>().color = Accent;

        if (dd.template)
        {
            var ti = dd.template.GetComponent<Image>();
            if (ti) { ti.sprite = sRound; ti.type = Image.Type.Sliced; ti.color = new Color(0.07f, 0.09f, 0.17f, 1f); }
            var ib = dd.template.Find("Viewport/Content/Item/Item Background");
            if (ib) { var ii = ib.GetComponent<Image>(); ii.sprite = sRound; ii.type = Image.Type.Sliced; ii.color = Track; }
            var ck = dd.template.Find("Viewport/Content/Item/Item Checkmark");
            if (ck) ck.GetComponent<Image>().color = Accent;
            // para que la lista desplegable tambien se pueda tocar con el rayo VR
            if (!dd.template.GetComponent<TrackedDeviceGraphicRaycaster>())
                dd.template.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }
    }


    // =====================================================================
    //  LAYOUT NUEVO: barra lateral + paginas
    // =====================================================================
    // Panel de vidrio oscuro con borde neon (sirve para la barra lateral y las paginas)
    static GameObject MakePanel(Transform parent, string name, Color border, RectOffset pad = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = sRound;
        img.type = Image.Type.Sliced;
        img.color = PanelColor;

        var v = go.GetComponent<VerticalLayoutGroup>();
        v.padding = pad != null ? pad : new RectOffset(46, 46, 40, 34);
        v.spacing = 16;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        var grad = AddDecor(go.transform, "Brillo Superior", sGrad, new Color(border.r, border.g, border.b, 0.16f), Image.Type.Simple, false);
        var grt = grad.rectTransform;
        grt.anchorMin = new Vector2(0f, 1f); grt.anchorMax = new Vector2(1f, 1f);
        grt.pivot = new Vector2(0.5f, 1f);
        grt.offsetMin = new Vector2(16f, -280f); grt.offsetMax = new Vector2(-16f, -8f);
        AddDecor(go.transform, "Borde", sRing, new Color(border.r, border.g, border.b, 0.85f), Image.Type.Sliced);
        return go;
    }

    static void Flex(GameObject go, float w = 1f)
    {
        var le = go.GetComponent<LayoutElement>();
        if (!le) le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = w;
        le.preferredWidth = 0f;
        le.minWidth = 0f;
    }

    static GameObject MakeHBox(Transform parent, float spacing)
    {
        var go = new GameObject("Fila", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.UpperCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;      // tarjetas de la fila con la misma altura
        return go;
    }

    static GameObject MakeVBox(Transform parent, float spacing)
    {
        var go = new GameObject("Columna", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var v = go.GetComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        Flex(go);
        return go;
    }

    static void MakeFlexSpace(Transform parent)
    {
        var go = new GameObject("Espacio flexible", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.flexibleHeight = 1f;
        le.minHeight = 0f;
        le.preferredHeight = 0f;
    }

    // Fila inferior con botones a la derecha
    static Transform MakeFooter(Transform parent, float height)
    {
        var go = new GameObject("Pie", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Size(go, height);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 16;
        h.childAlignment = TextAnchor.MiddleRight;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        var sp = new GameObject("Espacio", typeof(RectTransform), typeof(LayoutElement));
        sp.transform.SetParent(go.transform, false);
        sp.GetComponent<LayoutElement>().flexibleWidth = 1f;
        return go.transform;
    }

    static Button FooterButton(Transform footer, string icon, string text, UnityAction a, Color c)
    {
        var b = MakeButton(footer, icon, text, a, c, 72f, 32f);
        var le = b.GetComponent<LayoutElement>();
        le.preferredWidth = 330f; le.minWidth = 330f;
        return b;
    }

    // Boton de navegacion de la barra lateral (se ilumina cuando su pagina esta activa)
    static Button MakeNav(Transform parent, string icon, string text, UnityAction onClick, out Image pill)
    {
        var b = MakeButton(parent, icon, text, onClick, Color.white, 60f, 30f);
        b.name = "Nav " + text;
        pill = b.GetComponent<Image>();
        pill.color = new Color(BtnSecondary.r, BtnSecondary.g, BtnSecondary.b, 0.35f);
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        b.colors = cb;
        var lab = b.GetComponentInChildren<TMP_Text>();
        lab.alignment = TextAlignmentOptions.MidlineLeft;
        lab.margin = new Vector4(86f, 0f, 0f, 0f);
        var sh = b.GetComponent<Shadow>();
        if (sh) sh.effectColor = Color.clear;
        return b;
    }

    static void GradientText(TMP_Text t, Color top, Color bottom)
    {
        t.enableVertexGradient = true;
        t.colorGradient = new VertexGradient(top, top, bottom, bottom);
    }

    // Tarjeta de caracteristica (pagina de inicio)
    static void FeatureCard(Transform row, string name, string icon, Color color, string title, string text)
    {
        var card = MakeCard(row, name, color);
        Flex(card);
        MakeRow(card.transform, icon, title, 30, 56, color, true, 46f);
        MakeLabel(card.transform, text, 24, 72, TextSoft);
    }

    // Tarjeta de controles en columna, con dos textos (controladores / manos)
    static void ControlCard(Transform row, string name, string icon, Color color, string title,
        string controllerText, string handText,
        System.Collections.Generic.List<GameObject> ctrl, System.Collections.Generic.List<GameObject> hand)
    {
        var card = MakeCard(row, name, color);
        Flex(card);
        MakeIcon(card.transform, icon, 84f, color);
        MakeLabel(card.transform, title, 34, 46, color, TextAlignmentOptions.Center, true);
        var a = MakeLabel(card.transform, controllerText, 28, 120, TextSoft);
        a.name = "Texto Controladores";
        var b = MakeLabel(card.transform, handText, 28, 120, TextSoft);
        b.name = "Texto Manos";
        b.gameObject.SetActive(false);
        ctrl.Add(a.gameObject);
        hand.Add(b.gameObject);
    }

    // =====================================================================
    //  CONSTRUCCION DEL MENU
    // =====================================================================
    [MenuItem("Tools/MiniMarket/Crear Menu VR")]
    static void Build()
    {
        int opt = EditorUtility.DisplayDialogComplex("Crear menu VR",
            "Si ya existen MenuCanvas, MenuManager o MenuRoom en la escena, se van a reemplazar.\n\n¿Que quieres crear?",
            "Menu + Cuarto", "Cancelar", "Solo menu");
        if (opt == 1) return;

        EnsureSprites();
        lastFloaters = null; lastBob = null; lastNeon = null;
        if (opt == 0) BuildRoom();

        BuildMenu();
    }

    [MenuItem("Tools/MiniMarket/Crear Cuarto del Menu")]
    static void BuildRoomOnly()
    {
        EnsureSprites();
        BuildRoom();
        var mm = UnityEngine.Object.FindFirstObjectByType<MainMenu>();
        if (mm)
        {
            mm.floatingItems = lastFloaters;
            mm.bobItems = lastBob;
            mm.neonRenderers = lastNeon;
            EditorUtility.SetDirty(mm);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void BuildMenu()
    {
        foreach (var n in new[] { "MenuCanvas", "MenuManager" })
        {
            var old = GameObject.Find(n);
            if (old) Undo.DestroyObjectImmediate(old);
        }

        // Canvas ancho en World Space (16:9), listo para VR
        var canvasGO = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Crear Menu VR");
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        canvasGO.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        var crt = canvasGO.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(1600f, 900f);
        crt.position = new Vector3(0f, 1.5f, 1.8f);
        crt.localScale = Vector3.one * 0.0011f;

        // Halo detras de todo
        var halo = new GameObject("Halo", typeof(RectTransform), typeof(Image));
        halo.transform.SetParent(canvasGO.transform, false);
        var hrt = halo.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
        hrt.offsetMin = new Vector2(-50f, -50f); hrt.offsetMax = new Vector2(50f, 50f);
        var himg = halo.GetComponent<Image>();
        himg.sprite = sGlow;
        himg.type = Image.Type.Sliced;
        himg.color = new Color(AccentPink.r, AccentPink.g, AccentPink.b, 0.35f);
        himg.raycastTarget = false;

        // Orbes de luz que derivan (se ven a traves del vidrio de los paneles)
        var orbA = Center(canvasGO.transform, "Orbe Rosa", sDot, new Color(AccentPink.r, AccentPink.g, AccentPink.b, 0.38f), 560f);
        var orbB = Center(canvasGO.transform, "Orbe Cian", sDot, new Color(Accent.r, Accent.g, Accent.b, 0.30f), 500f);
        var orbC = Center(canvasGO.transform, "Orbe Violeta", sDot, new Color(0.60f, 0.30f, 1f, 0.35f), 460f);
        orbA.rectTransform.anchoredPosition = new Vector2(-520f, -180f);
        orbB.rectTransform.anchoredPosition = new Vector2(430f, 170f);
        orbC.rectTransform.anchoredPosition = new Vector2(80f, -230f);

        var mgrGO = new GameObject("MenuManager");
        Undo.RegisterCreatedObjectUndo(mgrGO, "Crear MenuManager");
        var menu = mgrGO.AddComponent<MainMenu>();
        menu.gameScene = "MinimarketXR_Full";
        menu.menuCanvas = canvasGO.transform;
        menu.floatingItems = lastFloaters;
        menu.bobItems = lastBob;
        menu.neonRenderers = lastNeon;
        menu.drifters = new[] { orbA.rectTransform, orbB.rectTransform, orbC.rectTransform };
        menu.navOn = BtnMain;
        menu.navOff = new Color(BtnSecondary.r, BtnSecondary.g, BtnSecondary.b, 0.35f);
        menu.tabOn = BtnMain;
        menu.tabOff = BtnSecondary;
        menu.pillOn = AccentGreen;
        menu.pillOff = Track;

        // ================= BARRA LATERAL =================
        var side = MakePanel(canvasGO.transform, "Sidebar", AccentPink, new RectOffset(34, 34, 30, 28));
        var srt = side.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(0f, 1f);
        srt.offsetMin = new Vector2(0f, 0f); srt.offsetMax = new Vector2(440f, 0f);
        side.GetComponent<VerticalLayoutGroup>().spacing = 10;

        var logoGlow = MakeBadge(side.transform, menu);
        var title = MakeLabel(side.transform, GameName.ToUpper(), 62, 72, Color.white, TextAlignmentOptions.Center, true);
        title.characterSpacing = 6f;
        GradientText(title, Color.white, AccentPink);
        MakeLabel(side.transform, Subtitle, 22, 30, TextDim);
        MakeSpacer(side.transform, 6);

        Image pHome, pSet, pCtl, pCre;
        MakeNav(side.transform, "store", "Inicio", menu.Back, out pHome);
        MakeNav(side.transform, "settings", "Ajustes", menu.OpenSettings, out pSet);
        MakeNav(side.transform, "controls", "Controles", menu.OpenControls, out pCtl);
        MakeNav(side.transform, "star", "Créditos", menu.OpenCredits, out pCre);
        MakeFlexSpace(side.transform);
        MakeButton(side.transform, "play", "JUGAR", menu.Play, BtnMain, 88f, 40f, true);
        MakeButton(side.transform, "exit", "Salir", menu.Quit, BtnDanger, 54f, 26f);
        MakeLabel(side.transform, Version, 20, 26, TextDim);

        // ================= CONTENIDO =================
        var content = new GameObject("Contenido", typeof(RectTransform));
        content.transform.SetParent(canvasGO.transform, false);
        var cr = content.GetComponent<RectTransform>();
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(464f, 0f); cr.offsetMax = Vector2.zero;

        // ----- Inicio -----
        var main = MakePanel(content.transform, "MainPanel", Accent);
        var kick = MakeLabel(main.transform, "BIENVENIDO AL SÚPER DEL FUTURO", 24, 32, AccentAmber, TextAlignmentOptions.MidlineLeft, true);
        kick.characterSpacing = 8f;
        var head = MakeLabel(main.transform, "Compra sin cola.\nSin límites.", 76, 190, Color.white, TextAlignmentOptions.MidlineLeft, true);
        GradientText(head, Color.white, Accent);
        MakeLabel(main.transform,
            "Entra al minimarket virtual, agarra productos con tus manos y recorre los pasillos sin salir de casa.",
            28, 96, TextSoft, TextAlignmentOptions.TopLeft);
        MakeSpacer(main.transform, 6);

        var feats = MakeHBox(main.transform, 18);
        FeatureCard(feats.transform, "Tarjeta Manos", "grab", Accent, "Manos o controles", "Agarra con Grip o cierra la mano.");
        FeatureCard(feats.transform, "Tarjeta Audio3D", "volume", AccentPink, "Audio 3D", "Los sonidos cambian según dónde estés.");
        FeatureCard(feats.transform, "Tarjeta DiaNoche", "star", AccentAmber, "Día y noche", "El súper cambia mientras compras.");
        MakeFlexSpace(main.transform);

        var tipCard = MakeCard(main.transform, "Tarjeta Consejo", AccentAmber);
        MakeLabel(tipCard.transform, "CONSEJO", 20, 26, AccentAmber, TextAlignmentOptions.Center, true).characterSpacing = 6f;
        var tip = MakeLabel(tipCard.transform, "", 28, 40, TextSoft);
        tip.fontStyle = FontStyles.Italic;
        menu.tipLabel = tip;

        // ----- Ajustes -----
        var settings = MakePanel(content.transform, "SettingsPanel", Accent);
        MakeTitle(settings.transform, "Ajustes", 56, 80, "settings");

        var cols = MakeHBox(settings.transform, 20);
        var colL = MakeVBox(cols.transform, 16);
        var colR = MakeVBox(cols.transform, 16);

        var cAudio = MakeCard(colL.transform, "Tarjeta Audio", Accent);
        var volLabel = MakeRow(cAudio.transform, "volume", "Volumen: 100%", 30, 44, TextSoft, false, 36f);
        var volSlider = MakeSlider(cAudio.transform, "Slider Volumen", 0f, 1f, 1f, Accent);

        var cGfx = MakeCard(colL.transform, "Tarjeta Graficos", AccentAmber);
        MakeRow(cGfx.transform, "palette", "Calidad gráfica", 30, 44, TextSoft, false, 36f);
        var ddGO = TMP_DefaultControls.CreateDropdown(TmpRes());
        ddGO.name = "Dropdown Calidad";
        ddGO.transform.SetParent(cGfx.transform, false);
        var dd = ddGO.GetComponent<TMP_Dropdown>();
        StyleDropdown(dd);
        Size(ddGO, 56);

        var cMove = MakeCard(colR.transform, "Tarjeta Movimiento", AccentGreen);
        var walkLabel = MakeRow(cMove.transform, "walk", "Velocidad al caminar: x1.0", 30, 44, TextSoft, false, 36f);
        var walkSlider = MakeSlider(cMove.transform, "Slider Velocidad", 0.5f, 2f, 1f, AccentGreen);
        MakeSpacer(cMove.transform, 4);
        Image pill; RectTransform knob;
        var toggle = MakeSwitch(cMove.transform, "Caminar con brazos", out pill, out knob);

        // ----- Calibracion -----
        var cCal = MakeCard(colL.transform, "Tarjeta Calibracion", AccentPink);
        var scaleLabel = MakeRow(cCal.transform, "ruler", "Tamano de los productos: 100%", 30, 44, TextSoft, false, 36f);
        var scaleSlider = MakeSlider(cCal.transform, "Slider Tamano", 0.5f, 2f, 1f, AccentPink);

        var cCal2 = MakeCard(colR.transform, "Tarjeta Brazos", AccentAmber);
        var calLabel = MakeRow(cCal2.transform, "walk", "Calibrar brazos", 30, 44, TextSoft, false, 36f);
        var calButton = MakeButton(cCal2.transform, "check", "Calibrar brazos", menu.CalibrateArms, AccentAmber);
        MakeSpacer(cCal2.transform, 4);
        MakeLabel(cCal2.transform, "Estira los brazos hacia delante\ny pulsa el boton.", 22, 56, TextDim);

        var cNote = MakeCard(colR.transform, "Tarjeta Nota", AccentPink);
        MakeLabel(cNote.transform, "Tus cambios se guardan solos\ny se aplican en el juego.", 24, 72, TextDim);

        MakeFlexSpace(settings.transform);
        var setFoot = MakeFooter(settings.transform, 72);
        FooterButton(setFoot, "reset", "Restablecer", menu.ResetDefaults, BtnSecondary);
        FooterButton(setFoot, "back", "Volver", menu.Back, BtnMain);

        // ----- Controles -----
        var controls = MakePanel(content.transform, "ControlsPanel", Accent);
        MakeTitle(controls.transform, "Controles", 56, 80, "controls");

        var tabRow = MakeButtonRow(controls.transform, 64);
        var tabC = MakeTab(tabRow, "controls", "Controladores", menu.ShowControllers, BtnMain);
        var tabH = MakeTab(tabRow, "grab", "Manos", menu.ShowHands, BtnSecondary);
        menu.tabControllers = tabC.GetComponent<Image>();
        menu.tabHands = tabH.GetComponent<Image>();

        var ctrlTexts = new System.Collections.Generic.List<GameObject>();
        var handTexts = new System.Collections.Generic.List<GameObject>();
        var ctrlRow = MakeHBox(controls.transform, 18);
        ControlCard(ctrlRow.transform, "Tarjeta Agarrar", "grab", Accent, "AGARRAR",
            "Toca el objeto y aprieta el botón Grip.", "Toca el objeto y cierra la mano.", ctrlTexts, handTexts);
        ControlCard(ctrlRow.transform, "Tarjeta Caminar", "walk", AccentGreen, "CAMINAR",
            "Mueve el joystick izquierdo.", "Estira los brazos y muévelos.", ctrlTexts, handTexts);
        ControlCard(ctrlRow.transform, "Tarjeta Girar", "rotate", AccentAmber, "GIRAR",
            "Mueve el joystick derecho.", "Gira tu cuerpo.", ctrlTexts, handTexts);
        menu.controllerTexts = ctrlTexts.ToArray();
        menu.handTexts = handTexts.ToArray();

        MakeFlexSpace(controls.transform);
        var ctlFoot = MakeFooter(controls.transform, 72);
        FooterButton(ctlFoot, "back", "Volver", menu.Back, BtnMain);

        // ----- Creditos -----
        var credits = MakePanel(content.transform, "CreditsPanel", Accent);
        MakeTitle(credits.transform, "Créditos", 56, 80, "star");

        var rowA = MakeHBox(credits.transform, 18);
        var c1 = MakeCard(rowA.transform, "Tarjeta Desarrollo", Accent);
        Flex(c1);
        MakeRow(c1.transform, "store", "DESARROLLADO POR", 26, 36, Accent, true, 30f);
        MakeLabel(c1.transform, "<b>" + Dev1 + "</b>\n<b>" + Dev2 + "</b>", 40, 110, TextSoft);
        var c2 = MakeCard(rowA.transform, "Tarjeta Modelos", AccentGreen);
        Flex(c2);
        MakeRow(c2.transform, "box", "MODELOS 3D", 26, 36, AccentGreen, true, 30f);
        MakeLabel(c2.transform, "Supermarket Props 3D Models Collection", 28, 80, TextSoft);

        var rowB = MakeHBox(credits.transform, 18);
        var c3 = MakeCard(rowB.transform, "Tarjeta Tecnologia", AccentAmber);
        Flex(c3);
        MakeRow(c3.transform, "tools", "TECNOLOGÍA", 26, 36, AccentAmber, true, 30f);
        MakeLabel(c3.transform, "Unity 6 · OpenXR\nXR Interaction Toolkit", 28, 80, TextSoft);
        var c4 = MakeCard(rowB.transform, "Tarjeta Plataforma", AccentPink);
        Flex(c4);
        MakeRow(c4.transform, "headset", "PLATAFORMA", 26, 36, AccentPink, true, 30f);
        MakeLabel(c4.transform, "Meta Quest · PC", 28, 80, TextSoft);

        MakeLabel(credits.transform, GameName + " · 2026", 24, 34, TextDim);
        MakeFlexSpace(credits.transform);
        var creFoot = MakeFooter(credits.transform, 72);
        FooterButton(creFoot, "back", "Volver", menu.Back, BtnMain);

        // ----- Conectar referencias -----
        menu.sidebar = side;
        menu.navPills = new[] { pHome, pSet, pCtl, pCre };
        menu.pulseGraphics = new Graphic[] { himg, logoGlow };
        menu.mainPanel = main;
        menu.settingsPanel = settings;
        menu.controlsPanel = controls;
        menu.creditsPanel = credits;
        menu.volumeSlider = volSlider;
        menu.volumeLabel = volLabel;
        menu.walkSpeedSlider = walkSlider;
        menu.walkSpeedLabel = walkLabel;
        menu.armSwingToggle = toggle;
        menu.armSwingPill = pill;
        menu.armSwingKnob = knob;
        menu.qualityDropdown = dd;
        menu.productScaleSlider = scaleSlider;
        menu.productScaleLabel = scaleLabel;
        menu.calibrateArmsButton = calButton;
        menu.calibrateArmsLabel = calLabel;

        settings.SetActive(false);
        controls.SetActive(false);
        credits.SetActive(false);

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
        {
            if (!EditorApplication.ExecuteMenuItem("GameObject/XR/UI Event System"))
                Debug.LogWarning("Crea el EventSystem a mano: GameObject > XR > UI Event System");
        }

        EditorUtility.SetDirty(menu);
        Selection.activeGameObject = canvasGO;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Menu VR de " + GameName + " creado. Revisa Game Scene en MenuManager y guarda la escena.");
    }


    // =====================================================================
    //  CONSTRUCCION DEL CUARTO (cuarto gamer: oscuro, LEDs y un foco)
    // =====================================================================
    static Transform Child(Transform parent, string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 scale, Material m, bool collider = false)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (!collider)
        {
            var c = g.GetComponent<Collider>();
            if (c) UnityEngine.Object.DestroyImmediate(c);
        }
        return g;
    }

    static Material Mat(string name, Color color, Color emission, float smooth, Texture2D tex = null, Vector2? tiling = null)
    {
        string path = RoomFolder + "/Materials/" + name + ".mat";
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        var sh = urp != null ? urp : Shader.Find("Standard");

        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);

        if (tex != null)
        {
            Vector2 tl = tiling.HasValue ? tiling.Value : Vector2.one;
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tl); }
            if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", tl); }
        }

        if (emission.maxColorComponent > 0.01f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", emission);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // Textura de piso: baldosas 2x2 con lineas cian (cada repeticion cubre 2 m)
    static Texture2D MakeFloorTexture()
    {
        const int s = 128;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, true);
        var a = new Color(0.075f, 0.065f, 0.105f, 1f);
        var b = new Color(0.055f, 0.048f, 0.082f, 1f);
        int half = s / 2;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                bool alt = (x < half) ^ (y < half);
                Color c = alt ? a : b;
                if ((x % half) < 2 || (y % half) < 2) c = Color.Lerp(c, Accent, 0.10f);
                t.SetPixel(x, y, c);
            }
        }
        t.Apply();

        string path = RoomFolder + "/Piso.png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void MakeLight(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
    }

    static void BuildShelf(Transform parent, string name, Vector3 pos, float rotY, Material metal,
        Material[] products, System.Random rng)
    {
        var s = Child(parent, name);
        s.position = pos;
        s.rotation = Quaternion.Euler(0f, rotY, 0f);

        const float W = 2.4f, D = 0.5f, H = 2.0f;
        Box(s, "Fondo", new Vector3(0f, H / 2f, -D / 2f + 0.02f), new Vector3(W, H, 0.04f), metal);
        Box(s, "PosteI", new Vector3(-W / 2f, H / 2f, 0f), new Vector3(0.05f, H, D), metal);
        Box(s, "PosteD", new Vector3(W / 2f, H / 2f, 0f), new Vector3(0.05f, H, D), metal);
        Box(s, "Tapa", new Vector3(0f, H, 0f), new Vector3(W, 0.04f, D), metal);

        float[] ys = { 0.12f, 0.62f, 1.12f, 1.62f };
        for (int i = 0; i < ys.Length; i++)
        {
            float y = ys[i];
            Box(s, "Tabla", new Vector3(0f, y, 0f), new Vector3(W, 0.04f, D), metal);
            for (float x = -W / 2f + 0.22f; x < W / 2f - 0.15f; x += 0.3f)
            {
                float h = 0.18f + (float)rng.NextDouble() * 0.20f;
                var m = products[rng.Next(products.Length)];
                float zOff = 0.03f + (float)rng.NextDouble() * 0.05f;
                Box(s, "Producto", new Vector3(x, y + 0.02f + h / 2f, zOff), new Vector3(0.24f, h, 0.30f), m);
            }
        }
    }

    static void BuildCounter(Transform parent, Vector3 pos, Material metal, Material top, Material belt,
        Material cyan, Material screen, Material[] products)
    {
        var c = Child(parent, "Mostrador");
        c.position = pos;
        Box(c, "Base", new Vector3(0f, 0.45f, 0f), new Vector3(2.3f, 0.9f, 0.7f), metal);
        Box(c, "NeonFrente", new Vector3(0f, 0.12f, -0.36f), new Vector3(2.2f, 0.05f, 0.03f), cyan);
        Box(c, "Tapa", new Vector3(0f, 0.93f, 0f), new Vector3(2.4f, 0.06f, 0.8f), top);
        Box(c, "Cinta", new Vector3(-0.3f, 0.97f, 0f), new Vector3(1.3f, 0.03f, 0.5f), belt);
        Box(c, "Caja registradora", new Vector3(0.8f, 1.1f, 0.1f), new Vector3(0.45f, 0.28f, 0.4f), metal);
        var scr = Box(c, "Pantalla", new Vector3(0.8f, 1.34f, 0.2f), new Vector3(0.38f, 0.24f, 0.03f), screen);
        scr.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);

        float[] xs = { -0.8f, -0.4f, 0.05f };
        float[] hs = { 0.22f, 0.30f, 0.18f };
        for (int i = 0; i < xs.Length; i++)
            Box(c, "Producto", new Vector3(xs[i], 0.985f + hs[i] / 2f, 0f), new Vector3(0.2f, hs[i], 0.22f), products[i % products.Length]);
    }

    // Material para particulas (aditivo, sin luz)
    static Material MatParticle(string name, Texture tex)
    {
        string path = RoomFolder + "/Materials/" + name + ".mat";
        var sh = Shader.Find("Mobile/Particles/Additive");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildRoom()
    {
        Directory.CreateDirectory(RoomFolder + "/Materials");

        var old = GameObject.Find("MenuRoom");
        if (old) Undo.DestroyObjectImmediate(old);

        var root = new GameObject("MenuRoom");
        Undo.RegisterCreatedObjectUndo(root, "Crear cuarto del menu");
        var estructura = Child(root.transform, "Estructura");   // estatico
        var leds = Child(root.transform, "LEDs");               // no estatico: pulsan
        var foco = Child(root.transform, "Foco");
        var luces = Child(root.transform, "Luces");

        // ---------- Materiales ----------
        var floorTex = MakeFloorTexture();
        var mFloor = Mat("Piso", Color.white, Color.clear, 0.65f, floorTex, new Vector2(RoomW / 2f, RoomL / 2f));
        var mWall  = Mat("Pared", new Color(0.045f, 0.04f, 0.075f), Color.clear, 0.25f);
        var mCeil  = Mat("Techo", new Color(0.03f, 0.028f, 0.05f), Color.clear, 0.1f);
        var mRug   = Mat("Alfombra", new Color(0.065f, 0.05f, 0.11f), Color.clear, 0.05f);
        var mCable = Mat("Cable", new Color(0.02f, 0.02f, 0.02f), Color.clear, 0.3f);
        var mSocket = Mat("Casquillo", new Color(0.10f, 0.10f, 0.12f), Color.clear, 0.5f);
        Color bulbCol = new Color(1f, 0.80f, 0.52f);
        var mBulb  = Mat("Foco", bulbCol, bulbCol * 3f, 0f);

        Color violet = new Color(0.55f, 0.30f, 1f);
        var mLedCyan   = Mat("LedCian",    Accent,     Accent * 2.6f, 0f);
        var mLedPink   = Mat("LedRosa",    AccentPink, AccentPink * 2.6f, 0f);
        var mLedViolet = Mat("LedVioleta", violet,     violet * 2.2f, 0f);

        float hw = RoomW / 2f, hl = RoomL / 2f, t = 0.1f;

        // ---------- Piso, techo, paredes ----------
        Box(estructura, "Piso", new Vector3(0f, -t / 2f, 0f), new Vector3(RoomW, t, RoomL), mFloor, true);
        Box(estructura, "Techo", new Vector3(0f, RoomH + t / 2f, 0f), new Vector3(RoomW, t, RoomL), mCeil);
        Box(estructura, "Pared Frente", new Vector3(0f, RoomH / 2f, hl + t / 2f), new Vector3(RoomW, RoomH, t), mWall);
        Box(estructura, "Pared Atras", new Vector3(0f, RoomH / 2f, -hl - t / 2f), new Vector3(RoomW, RoomH, t), mWall);
        Box(estructura, "Pared Izq", new Vector3(-hw - t / 2f, RoomH / 2f, 0f), new Vector3(t, RoomH, RoomL), mWall);
        Box(estructura, "Pared Der", new Vector3(hw + t / 2f, RoomH / 2f, 0f), new Vector3(t, RoomH, RoomL), mWall);

        // alfombra redonda bajo el foco (unico "mueble")
        var rug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rug.name = "Alfombra";
        rug.transform.SetParent(estructura, false);
        rug.transform.localPosition = new Vector3(0f, 0.006f, 0f);
        rug.transform.localScale = new Vector3(2.4f, 0.005f, 2.4f);
        UnityEngine.Object.DestroyImmediate(rug.GetComponent<Collider>());
        rug.GetComponent<MeshRenderer>().sharedMaterial = mRug;

        // ---------- LEDs (todos pulsan suave con MainMenu) ----------
        var neon = new System.Collections.Generic.List<Renderer>();
        System.Action<GameObject> addNeon = g => neon.Add(g.GetComponent<MeshRenderer>());

        // columnas LED en las 4 esquinas
        float c = 0.05f, cs = 0.06f;
        Vector3[] corners =
        {
            new Vector3(-hw + c, RoomH / 2f,  hl - c), new Vector3(hw - c, RoomH / 2f,  hl - c),
            new Vector3(-hw + c, RoomH / 2f, -hl + c), new Vector3(hw - c, RoomH / 2f, -hl + c)
        };
        Material[] cornerMats = { mLedCyan, mLedPink, mLedPink, mLedCyan };
        for (int i = 0; i < corners.Length; i++)
            addNeon(Box(leds, "LED Esquina " + i, corners[i], new Vector3(cs, RoomH - 0.1f, cs), cornerMats[i]));

        // perimetro del techo (violeta) y del piso (cian/rosa)
        float n = 0.05f, off = 0.05f, yTop = RoomH - 0.06f, yLow = 0.05f;
        addNeon(Box(leds, "LED Techo F", new Vector3(0f, yTop,  hl - off), new Vector3(RoomW, n, n), mLedViolet));
        addNeon(Box(leds, "LED Techo A", new Vector3(0f, yTop, -hl + off), new Vector3(RoomW, n, n), mLedViolet));
        addNeon(Box(leds, "LED Techo I", new Vector3(-hw + off, yTop, 0f), new Vector3(n, n, RoomL), mLedViolet));
        addNeon(Box(leds, "LED Techo D", new Vector3( hw - off, yTop, 0f), new Vector3(n, n, RoomL), mLedViolet));
        addNeon(Box(leds, "LED Piso F", new Vector3(0f, yLow,  hl - off), new Vector3(RoomW, n, n), mLedCyan));
        addNeon(Box(leds, "LED Piso A", new Vector3(0f, yLow, -hl + off), new Vector3(RoomW, n, n), mLedPink));
        addNeon(Box(leds, "LED Piso I", new Vector3(-hw + off, yLow, 0f), new Vector3(n, n, RoomL), mLedPink));
        addNeon(Box(leds, "LED Piso D", new Vector3( hw - off, yLow, 0f), new Vector3(n, n, RoomL), mLedCyan));

        // marco LED en la pared de enfrente, justo detras del menu
        float fz = hl - 0.03f, fw = 3.1f, fh = 1.75f, fy = 1.5f, ft = 0.05f;
        addNeon(Box(leds, "Marco Arriba", new Vector3(0f, fy + fh / 2f, fz), new Vector3(fw, ft, ft), mLedPink));
        addNeon(Box(leds, "Marco Abajo",  new Vector3(0f, fy - fh / 2f, fz), new Vector3(fw, ft, ft), mLedPink));
        addNeon(Box(leds, "Marco Izq", new Vector3(-fw / 2f, fy, fz), new Vector3(ft, fh, ft), mLedCyan));
        addNeon(Box(leds, "Marco Der", new Vector3( fw / 2f, fy, fz), new Vector3(ft, fh, ft), mLedCyan));

        // ---------- Foco colgando en el centro ----------
        float bulbY = 2.38f;
        var cable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cable.name = "Cable";
        cable.transform.SetParent(foco, false);
        cable.transform.localPosition = new Vector3(0f, (RoomH + 2.58f) / 2f, 0f);
        cable.transform.localScale = new Vector3(0.012f, (RoomH - 2.58f) / 2f, 0.012f);
        UnityEngine.Object.DestroyImmediate(cable.GetComponent<Collider>());
        cable.GetComponent<MeshRenderer>().sharedMaterial = mCable;

        var socket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        socket.name = "Casquillo";
        socket.transform.SetParent(foco, false);
        socket.transform.localPosition = new Vector3(0f, 2.52f, 0f);
        socket.transform.localScale = new Vector3(0.07f, 0.065f, 0.07f);
        UnityEngine.Object.DestroyImmediate(socket.GetComponent<Collider>());
        socket.GetComponent<MeshRenderer>().sharedMaterial = mSocket;

        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "Bombillo";
        bulb.transform.SetParent(foco, false);
        bulb.transform.localPosition = new Vector3(0f, bulbY, 0f);
        bulb.transform.localScale = Vector3.one * 0.17f;
        UnityEngine.Object.DestroyImmediate(bulb.GetComponent<Collider>());
        bulb.GetComponent<MeshRenderer>().sharedMaterial = mBulb;

        // halo suave alrededor del foco (una sola particula fija, aditiva)
        var halo = new GameObject("Halo Foco");
        halo.transform.SetParent(foco, false);
        halo.transform.localPosition = new Vector3(0f, bulbY, 0f);
        var hps = halo.AddComponent<ParticleSystem>();
        var hm = hps.main;
        hm.loop = true;
        hm.startLifetime = 100000f;
        hm.startSpeed = 0f;
        hm.startSize = 1.5f;
        hm.startColor = new Color(1f, 0.70f, 0.38f, 0.30f);
        hm.maxParticles = 1;
        hm.simulationSpace = ParticleSystemSimulationSpace.Local;
        var hem = hps.emission;
        hem.rateOverTime = 2f;
        var hsh = hps.shape;
        hsh.enabled = false;
        var hpr = halo.GetComponent<ParticleSystemRenderer>();
        hpr.renderMode = ParticleSystemRenderMode.Billboard;
        hpr.sharedMaterial = MatParticle("Polvo", sDot != null ? sDot.texture : null);

        // ---------- Polvo en el aire (se ve bonito bajo el foco) ----------
        var dust = new GameObject("Polvo");
        dust.transform.SetParent(root.transform, false);
        dust.transform.localPosition = new Vector3(0f, RoomH / 2f, 0f);
        var ps = dust.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.65f, 0.7f), new Color(Accent.r, Accent.g, Accent.b, 0.5f));
        main.maxParticles = 70;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 9f;
        var shp = ps.shape;
        shp.shapeType = ParticleSystemShapeType.Box;
        shp.scale = new Vector3(RoomW - 1f, RoomH - 0.5f, RoomL - 1f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        vel.y = new ParticleSystem.MinMaxCurve(0.01f, 0.07f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
        var pr = dust.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sharedMaterial = MatParticle("Polvo", sDot != null ? sDot.texture : null);

        // ---------- Luces (solo 3, para Quest) ----------
        MakeLight(luces, "Luz Foco", new Vector3(0f, bulbY - 0.05f, 0.2f), new Color(1f, 0.78f, 0.50f), 2.2f, 6.5f);
        MakeLight(luces, "Luz LED Rosa", new Vector3(hw - 0.6f, 1.6f, hl - 0.6f), AccentPink, 0.9f, 4.5f);
        MakeLight(luces, "Luz LED Cian", new Vector3(-hw + 0.6f, 1.6f, hl - 0.6f), Accent, 0.9f, 4.5f);

        // ---------- Ambiente: oscuro ----------
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.035f, 0.06f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.022f;
        RenderSettings.fogColor = FogColor;

        var cam = Camera.main;
        if (cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
        }

        // Estructura quieta -> batching estatico (menos draw calls en Quest)
        foreach (var tr in estructura.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic);

        // cuarto vacio: sin objetos flotantes
        lastFloaters = new Transform[0];
        lastBob = new Transform[0];
        lastNeon = neon.ToArray();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("Cuarto gamer creado. El jugador (XR Origin) va en el centro, en 0,0,0.");
    }
}
