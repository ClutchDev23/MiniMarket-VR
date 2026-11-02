using UnityEngine;

/// <summary>
/// RELOJ DE PARED encima del monitor de la caja: agujas y hora digital en formato
/// 24 horas. Sigue la hora del juego (AlternarDiaNoche.Hora): en 5 minutos reales
/// pasa de 12:00 a 20:00 y anochece.
/// </summary>
public class RelojPared : MonoBehaviour
{
    Transform horario, minutero;
    TextMesh digital;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Auto()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (e, m) => Crear();
        Crear();
    }

    static void Crear()
    {
        if (FindAnyObjectByType<RelojPared>() != null) return;
        if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("MinimarketXR")) return;
        new GameObject("Reloj de pared").AddComponent<RelojPared>();
    }

    bool listo;
    void Update()
    {
        if (!listo)
        {
            var pantalla = GameObject.Find("Pantalla Caja");
            if (pantalla == null) return;
            Construir(pantalla.transform);
            listo = true;
        }
        float h = AlternarDiaNoche.Hora;
        int hh = Mathf.FloorToInt(h), mm = Mathf.FloorToInt((h - hh) * 60f);
        horario.localRotation = Quaternion.Euler(0, 0, -((h % 12f) / 12f) * 360f);
        minutero.localRotation = Quaternion.Euler(0, 0, -((h - hh) * 360f));
        digital.text = hh.ToString("00") + ":" + mm.ToString("00");
    }

    static Material Mat(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = c };
        m.SetFloat("_Smoothness", 0.5f);
        return m;
    }

    static Transform Pieza(PrimitiveType t, Transform p, Vector3 pos, Vector3 esc, Quaternion rot, Material m)
    {
        var g = GameObject.CreatePrimitive(t);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos; g.transform.localScale = esc; g.transform.localRotation = rot;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    void Construir(Transform pantalla)
    {
        // Mismo giro que la pantalla de la caja (la ve el cajero), 60 cm mas arriba.
        transform.SetPositionAndRotation(pantalla.position + Vector3.up * 0.62f, pantalla.rotation);
        var negro = Mat(new Color(0.08f, 0.08f, 0.1f));
        var blanco = Mat(new Color(0.97f, 0.97f, 0.95f));
        var naranja = Mat(new Color(0.95f, 0.47f, 0.13f));
        float R = 0.17f;
        // El frente del reloj mira hacia -z local (hacia el cajero).
        Pieza(PrimitiveType.Cylinder, transform, new Vector3(0, 0, 0.012f), new Vector3(R * 2.15f, 0.012f, R * 2.15f), Quaternion.Euler(90, 0, 0), naranja);
        Pieza(PrimitiveType.Cylinder, transform, Vector3.zero, new Vector3(R * 2f, 0.01f, R * 2f), Quaternion.Euler(90, 0, 0), blanco);
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            bool grande = i % 3 == 0;
            var pos = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0) * R * 0.85f + new Vector3(0, 0, -0.012f);
            Pieza(PrimitiveType.Cube, transform, pos, new Vector3(grande ? 0.012f : 0.007f, grande ? 0.035f : 0.02f, 0.004f), Quaternion.Euler(0, 0, -i * 30f), negro);
        }
        horario = new GameObject("Horario").transform; horario.SetParent(transform, false); horario.localPosition = new Vector3(0, 0, -0.016f);
        Pieza(PrimitiveType.Cube, horario, new Vector3(0, R * 0.25f, 0), new Vector3(0.014f, R * 0.55f, 0.004f), Quaternion.identity, negro);
        minutero = new GameObject("Minutero").transform; minutero.SetParent(transform, false); minutero.localPosition = new Vector3(0, 0, -0.02f);
        Pieza(PrimitiveType.Cube, minutero, new Vector3(0, R * 0.38f, 0), new Vector3(0.009f, R * 0.8f, 0.004f), Quaternion.identity, naranja);
        Pieza(PrimitiveType.Cylinder, transform, new Vector3(0, 0, -0.024f), new Vector3(0.02f, 0.004f, 0.02f), Quaternion.Euler(90, 0, 0), negro);

        // Hora digital 24 h debajo de las agujas
        var dg = new GameObject("Digital");
        dg.transform.SetParent(transform, false);
        dg.transform.localPosition = new Vector3(0, -R * 0.42f, -0.016f);
        digital = dg.AddComponent<TextMesh>();
        digital.anchor = TextAnchor.MiddleCenter; digital.alignment = TextAlignment.Center;
        digital.characterSize = 0.006f; digital.fontSize = 60; digital.fontStyle = FontStyle.Bold;
        digital.color = new Color(0.1f, 0.1f, 0.12f);
        digital.GetComponent<MeshRenderer>().sortingOrder = 5;
    }
}
