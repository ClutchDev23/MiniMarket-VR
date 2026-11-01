using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CARTELES DE OFERTA INTERACTIVOS (los "50% OFF" de la tienda).
///
/// Encima de cada cartel se pone una pantalla digital que va pasando ofertas (con
/// animacion de entrada y un borde que late). Si la TOCAS con el dedo (gatillo + indice)
/// o la apuntas con el rayo y aprietas, salta a la siguiente oferta con un sonido.
/// </summary>
public class CartelesOferta : MonoBehaviour
{
    static readonly (string titulo, string detalle, string color)[] Ofertas =
    {
        ("50% OFF", "en la segunda unidad\nde snacks", "#7CFF3A"),
        ("2 x 1", "en gaseosas de 500 ml", "#3AD0FF"),
        ("-30%", "en lacteos\nsolo hoy", "#FFD23A"),
        ("S/ 9.90", "pack de 6 cervezas\n(+18)", "#FF8A3A"),
        ("3 x 2", "en galletas y chocolates", "#FF5AA8"),
        ("NUEVO", "paga con tarjeta\ny acumula puntos", "#B48CFF"),
    };

    Text titulo, detalle;
    Image borde;
    int indice;
    float siguiente, entrada;
    AudioSource sfx;

    public static void Instalar()
    {
        int n = 0;
        foreach (var raiz in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!raiz.name.StartsWith("Pref_DisplayBoard")) continue;
            if (raiz.parent != null && raiz.parent.name.StartsWith("Pref_DisplayBoard")) continue;
            if (raiz.GetComponentInChildren<CartelesOferta>() != null) continue;
            // La pieza mas grande del cartel (el panel con el "50%").
            MeshRenderer r = null; float mejor = 0f;
            foreach (var x in raiz.GetComponentsInChildren<MeshRenderer>())
            {
                var e = x.bounds.size; float area = e.x * e.y + e.z * e.y;
                if (area > mejor) { mejor = area; r = x; }
            }
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var go = new GameObject("Oferta digital " + raiz.name);
            // En la raiz de la escena (sin la escala del cartel).
            go.AddComponent<CartelesOferta>().Armar(r, n++);
        }
        Debug.Log("[Ofertas] " + n + " carteles interactivos.");
    }

    void Armar(MeshRenderer cartel, int semilla)
    {
        // Tamaño real en el mundo (el modelo viene en otra escala): caja de limites.
        var wb = cartel.bounds;
        bool frenteX = wb.size.x < wb.size.z;
        Vector3[] ejes = { frenteX ? Vector3.right : Vector3.forward, Vector3.up, frenteX ? Vector3.forward : Vector3.right };
        float[] tam = { frenteX ? wb.size.x : wb.size.z, wb.size.y, frenteX ? wb.size.z : wb.size.x };
        int delgado = 0, alto = 1, ancho = 2;
        var centroMundo = wb.center;
        indice = semilla % Ofertas.Length;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 1f; sfx.maxDistance = 8f; sfx.playOnAwake = false;
        float anchoM = tam[ancho], altoM = tam[alto];
        Debug.Log("[Ofertas] " + cartel.name + " ancho=" + anchoM.ToString("0.00") + " alto=" + altoM.ToString("0.00") + " grosor=" + tam[delgado].ToString("0.00"));
        if (anchoM > 2.5f || altoM > 3f || anchoM < 0.1f) { Destroy(gameObject); return; }
        var arriba = Vector3.ProjectOnPlane(Vector3.up, ejes[delgado]).normalized;
        if (arriba.sqrMagnitude < 0.5f) arriba = ejes[alto];
        foreach (var lado in new[] { 1f, -1f })
        {
            var normal = ejes[delgado] * lado;
            var cv = new GameObject("Pantalla", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cv.transform.SetParent(transform, false);
            var canvas = cv.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            cv.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            var tipo = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (tipo != null) cv.AddComponent(tipo);
            var rt = (RectTransform)cv.transform;
            float w = anchoM * 0.9f, hgt = altoM * 0.9f;
            rt.sizeDelta = new Vector2(400, 400f * hgt / Mathf.Max(0.01f, w));
            rt.position = centroMundo + normal * (tam[delgado] / 2f + 0.012f);
            // Se lee desde el lado de "normal": el canvas mira hacia -normal.
            rt.rotation = Quaternion.LookRotation(-normal, arriba);
            float mpp = w / 400f;
            rt.localScale = Vector3.one * mpp;
            var fondo = Img(rt, new Color(0.03f, 0.03f, 0.06f, 0.96f));
            var marco = Img(rt, Color.white); marco.rectTransform.offsetMin = Vector2.zero;
            var dentro = Img(marco.rectTransform, new Color(0.03f, 0.03f, 0.06f, 1f));
            dentro.rectTransform.offsetMin = new Vector2(8, 8); dentro.rectTransform.offsetMax = new Vector2(-8, -8);
            var t1 = Txt(dentro.rectTransform, 110, FontStyle.Bold, new Vector2(0.5f, 0.62f));
            var t2 = Txt(dentro.rectTransform, 34, FontStyle.Normal, new Vector2(0.5f, 0.25f));
            var btn = fondo.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Siguiente);
            if (lado > 0) { titulo = t1; detalle = t2; borde = marco; }
            else { espejoT = t1; espejoD = t2; espejoB = marco; }
        }
        Mostrar();
        siguiente = Time.time + 4f + semilla * 0.7f;
    }

    Text espejoT, espejoD; Image espejoB;

    static Image Img(RectTransform padre, Color c)
    {
        var go = new GameObject("img", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        var i = go.GetComponent<Image>(); i.color = c; return i;
    }

    static Text Txt(RectTransform padre, int tam, FontStyle st, Vector2 ancla)
    {
        var go = new GameObject("txt", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = ancla; r.sizeDelta = new Vector2(380, tam * 2.6f);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = tam; t.fontStyle = st; t.alignment = TextAnchor.MiddleCenter; t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        return t;
    }

    void Siguiente()
    {
        indice = (indice + 1) % Ofertas.Length;
        Mostrar();
        siguiente = Time.time + 6f;
        if (sfx != null) sfx.PlayOneShot(SonidosProcedurales.Logro(), 0.5f);
    }

    void Mostrar()
    {
        var o = Ofertas[indice];
        ColorUtility.TryParseHtmlString(o.color, out var c);
        foreach (var (t, d, m) in new[] { (titulo, detalle, borde), (espejoT, espejoD, espejoB) })
        {
            if (t == null) continue;
            t.text = o.titulo; t.color = c;
            d.text = o.detalle + "\n<size=22><color=#AAAAAA>toca para ver otra</color></size>"; d.color = Color.white;
            m.color = c;
        }
        entrada = Time.time;
    }

    void Update()
    {
        if (Time.time > siguiente) { indice = (indice + 1) % Ofertas.Length; Mostrar(); siguiente = Time.time + 5f; }
        // Entrada con "pop" y borde que late.
        float k = Mathf.Clamp01((Time.time - entrada) / 0.35f);
        float pop = 1f + 0.25f * (1f - k) * Mathf.Sin(k * Mathf.PI);
        float pulso = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 2.4f));
        foreach (var (t, m) in new[] { (titulo, borde), (espejoT, espejoB) })
        {
            if (t == null) continue;
            t.rectTransform.localScale = Vector3.one * pop;
            var c = m.color; c.a = pulso; m.color = c;
        }
    }
}
