using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RANKING: guarda los mejores turnos (nota 0-20, ventas y total cobrado) en PlayerPrefs
/// y los muestra en un panel al lado del menu principal.
/// Cada venta terminada actualiza el turno actual; si es mejor que alguno del top 5, entra.
/// </summary>
public static class Ranking
{
    const string Clave = "ranking_v1";
    const int Max = 5;
    static string turnoId;   // un id por turno, asi el mismo turno se actualiza y no se duplica

    public struct Fila { public string id; public int nota, ventas, totalCent; public string fecha; }

    public static List<Fila> Leer()
    {
        var l = new List<Fila>();
        foreach (var linea in PlayerPrefs.GetString(Clave, "").Split('\n'))
        {
            var p = linea.Split('|');
            if (p.Length < 5) continue;
            int.TryParse(p[1], out var n); int.TryParse(p[2], out var v); int.TryParse(p[3], out var t);
            l.Add(new Fila { id = p[0], nota = n, ventas = v, totalCent = t, fecha = p[4] });
        }
        return l;
    }

    public static void Registrar(CajaRegistradora.Evaluacion e)
    {
        if (e.ventas <= 0) return;
        if (turnoId == null) turnoId = System.DateTime.Now.Ticks.ToString();
        var l = Leer();
        l.RemoveAll(f => f.id == turnoId);
        l.Add(new Fila { id = turnoId, nota = e.nota, ventas = e.ventas, totalCent = e.totalCent, fecha = System.DateTime.Now.ToString("dd/MM HH:mm") });
        l.Sort((a, b) => a.nota != b.nota ? b.nota.CompareTo(a.nota) : b.ventas != a.ventas ? b.ventas.CompareTo(a.ventas) : b.totalCent.CompareTo(a.totalCent));
        if (l.Count > Max) l.RemoveRange(Max, l.Count - Max);
        var sb = new System.Text.StringBuilder();
        foreach (var f in l) sb.Append(f.id).Append('|').Append(f.nota).Append('|').Append(f.ventas).Append('|').Append(f.totalCent).Append('|').Append(f.fecha).Append('\n');
        PlayerPrefs.SetString(Clave, sb.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>Al cargar una escena nueva empieza otro turno.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Instalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { turnoId = null; Panel(); };
        Panel();
    }

    static void Panel()
    {
        var menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.mainPanel == null) return;
        var canvas = menu.mainPanel.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return;
        var ct = canvas.transform;
        var b = ((RectTransform)ct).rect;

        var go = new GameObject("Ranking", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(520, 640);
        float escala = ct.lossyScale.x;
        rt.localScale = Vector3.one * escala;
        // A la derecha del menu, girado un poco hacia el jugador.
        rt.position = ct.position + ct.right * ((b.width * 0.5f + 300f) * escala);
        rt.rotation = ct.rotation * Quaternion.Euler(0, 20, 0);

        var fondo = Img(rt, new Color(0.055f, 0.065f, 0.10f, 0.95f), 0, 0, 520, 640);
        var o = fondo.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.6f, 0.2f, 0.6f); o.effectDistance = new Vector2(3, -3);
        Img(rt, new Color(0.95f, 0.47f, 0.13f, 1f), 0, 0, 520, 70);
        Txt(rt, "MEJORES TURNOS", 32, FontStyle.Bold, TextAnchor.MiddleCenter, 0, 0, 520, 70, Color.white);

        var l = Leer();
        if (l.Count == 0)
            Txt(rt, "Todavia no hay turnos.\n\nAtiende clientes en la tienda\ny tu mejor nota aparecera aqui.", 26, FontStyle.Normal, TextAnchor.MiddleCenter, 20, 90, 480, 500, new Color(0.75f, 0.8f, 0.88f));
        for (int i = 0; i < l.Count; i++)
        {
            var f = l[i];
            float y = 90 + i * 104;
            var fila = Img(rt, i == 0 ? new Color(0.30f, 0.25f, 0.08f, 1f) : new Color(0.14f, 0.165f, 0.235f, 1f), 20, y, 480, 92);
            string medalla = "<color=" + (i == 0 ? "#FFD34D" : i == 1 ? "#D8DEE8" : i == 2 ? "#E0965A" : "#9AA5B8") + ">" + (i + 1) + "°</color>";
            Txt(rt, medalla, 34, FontStyle.Bold, TextAnchor.MiddleCenter, 24, y, 70, 92, Color.white);
            Txt(rt, "<b>" + f.nota + "</b><size=22>/20</size>", 44, FontStyle.Normal, TextAnchor.MiddleLeft, 100, y, 150, 92,
                f.nota >= 18 ? new Color(0.45f, 1f, 0.6f) : f.nota >= 14 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.5f));
            Txt(rt, f.ventas + " ventas  ·  S/ " + (f.totalCent / 100f).ToString("0.00") + "\n<size=20><color=#9AA5B8>" + f.fecha + "</color></size>",
                24, FontStyle.Normal, TextAnchor.MiddleRight, 230, y, 255, 92, Color.white);
        }

        // Se muestra solo con el panel principal del menu.
        go.AddComponent<SeguirVisible>().objetivo = menu.mainPanel;
    }

    static Image Img(RectTransform p, Color c, float x, float y, float w, float h)
    {
        var go = new GameObject("Img", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        var i = go.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
        Rect((RectTransform)go.transform, x, y, w, h);
        return i;
    }

    static void Txt(RectTransform p, string s, int tam, FontStyle st, TextAnchor a, float x, float y, float w, float h, Color c)
    {
        var go = new GameObject("Txt", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(p, false);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = s; t.fontSize = tam; t.fontStyle = st; t.alignment = a; t.color = c; t.supportRichText = true; t.raycastTarget = false;
        Rect((RectTransform)go.transform, x, y, w, h);
    }

    static void Rect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    class SeguirVisible : MonoBehaviour
    {
        public GameObject objetivo;
        Canvas c;
        void Awake() { c = GetComponent<Canvas>(); }
        void Update() { if (objetivo != null) c.enabled = objetivo.activeInHierarchy; }
    }
}
