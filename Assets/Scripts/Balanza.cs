using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BALANZA para productos por peso (frutas y verduras).
/// - Esta en el mostrador, al lado del escaner, con una pantallita que marca kg y precio.
/// - Algunos clientes traen una bolsita de fruta: NO tiene codigo de barras, hay que
///   ponerla sobre el plato de la balanza y se agrega sola a la venta con su peso.
/// Las frutas son modelos simples hechos por codigo (pocos triangulos).
/// </summary>
public class Balanza : MonoBehaviour
{
    public static Balanza I { get; private set; }
    static readonly Vector3 PuntoLocal = new Vector3(0.40f, 0f, -0.52f);

    struct Tipo { public string nombre; public float precioKg; public Color color; public PrimitiveType forma; public Vector3 escala; }
    static readonly Tipo[] Tipos =
    {
        new Tipo { nombre = "Manzana roja", precioKg = 6.90f, color = new Color(0.75f, 0.08f, 0.08f), forma = PrimitiveType.Sphere, escala = new Vector3(0.075f, 0.07f, 0.075f) },
        new Tipo { nombre = "Naranja", precioKg = 3.50f, color = new Color(1f, 0.55f, 0.05f), forma = PrimitiveType.Sphere, escala = Vector3.one * 0.08f },
        new Tipo { nombre = "Platano de seda", precioKg = 3.20f, color = new Color(0.98f, 0.85f, 0.2f), forma = PrimitiveType.Capsule, escala = new Vector3(0.035f, 0.09f, 0.035f) },
        new Tipo { nombre = "Tomate", precioKg = 4.20f, color = new Color(0.9f, 0.15f, 0.1f), forma = PrimitiveType.Sphere, escala = new Vector3(0.07f, 0.06f, 0.07f) },
        new Tipo { nombre = "Papa amarilla", precioKg = 4.80f, color = new Color(0.78f, 0.62f, 0.35f), forma = PrimitiveType.Sphere, escala = new Vector3(0.07f, 0.055f, 0.06f) },
        new Tipo { nombre = "Limon", precioKg = 5.50f, color = new Color(0.45f, 0.75f, 0.15f), forma = PrimitiveType.Sphere, escala = Vector3.one * 0.05f },
    };

    Transform plato;
    TextMesh pantalla;
    CajaRegistradora caja;
    float desde; FrutaPesada candidata;

    public static void Instalar(CajaRegistradora caja, Transform raizMostrador, float h)
    {
        if (I != null || caja == null) return;
        var go = new GameObject("Balanza");
        go.transform.SetParent(raizMostrador, false);
        go.transform.localPosition = PuntoLocal + Vector3.up * h;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // la pantalla mira al cajero
        I = go.AddComponent<Balanza>();
        I.caja = caja;
        I.Construir();
    }

    void OnDestroy() { if (I == this) I = null; }

    static Material Mat(Color c, float metal = 0f, float liso = 0.4f)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = c };
        m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", liso);
        return m;
    }

    static GameObject Cubo(Transform p, string n, Vector3 pos, Vector3 tam, Material m, bool col = false)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = n;
        if (!col) Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(p, false);
        c.transform.localPosition = pos; c.transform.localScale = tam;
        c.GetComponent<Renderer>().sharedMaterial = m;
        return c;
    }

    void Construir()
    {
        var cuerpo = Mat(new Color(0.92f, 0.92f, 0.94f));
        var acero = Mat(new Color(0.7f, 0.72f, 0.75f), 0.9f, 0.7f);
        Cubo(transform, "Cuerpo", new Vector3(0, 0.03f, 0), new Vector3(0.30f, 0.06f, 0.26f), cuerpo);
        plato = Cubo(transform, "Plato", new Vector3(0, 0.068f, 0.02f), new Vector3(0.27f, 0.012f, 0.20f), acero, true).transform;
        // Torre con pantalla mirando al cajero
        Cubo(transform, "Torre", new Vector3(0, 0.12f, -0.115f), new Vector3(0.12f, 0.18f, 0.03f), cuerpo);
        Cubo(transform, "Display", new Vector3(0, 0.17f, -0.131f), new Vector3(0.11f, 0.05f, 0.004f), Mat(new Color(0.05f, 0.12f, 0.06f), 0f, 0.9f));
        var t = new GameObject("Lectura");
        t.transform.SetParent(transform, false);
        t.transform.localPosition = new Vector3(0, 0.17f, -0.134f);
        t.transform.localRotation = Quaternion.Euler(0, 0, 0);
        pantalla = t.AddComponent<TextMesh>();
        pantalla.anchor = TextAnchor.MiddleCenter; pantalla.alignment = TextAlignment.Center;
        pantalla.characterSize = 0.004f; pantalla.fontSize = 60; pantalla.color = new Color(0.45f, 1f, 0.55f);
        pantalla.text = "0.000 kg";
        var cartel = new GameObject("Etiqueta");
        cartel.transform.SetParent(transform, false);
        cartel.transform.localPosition = new Vector3(0, 0.23f, -0.131f);
        var ct = cartel.AddComponent<TextMesh>();
        ct.anchor = TextAnchor.MiddleCenter; ct.characterSize = 0.0025f; ct.fontSize = 60; ct.color = new Color(0.95f, 0.47f, 0.13f);
        ct.text = "BALANZA - frutas y verduras";
    }

    /// <summary>Un producto "por peso" nuevo (cada bolsita tiene su peso y su precio).</summary>
    public static Producto NuevaFruta(out int tipo, out float kg)
    {
        tipo = Random.Range(0, Tipos.Length);
        kg = Mathf.Round(Random.Range(0.35f, 1.6f) * 1000f) / 1000f;
        var t = Tipos[tipo];
        return new Producto
        {
            codigo = "PESO", clave = "fruta_" + tipo, categoria = "Frutas y verduras",
            nombre = t.nombre + " " + kg.ToString("0.000") + " kg",
            precio = Mathf.Round(t.precioKg * kg * 10f) / 10f,   // redondeado a 10 centimos
            stock = 99,
        };
    }

    /// <summary>Crea la bolsita de fruta del cliente en la zona de productos.</summary>
    public GameObject SoltarFruta(Producto p, int tipo, float kg, Vector3 pos, Transform padre)
    {
        var t = Tipos[tipo];
        var go = new GameObject("Bolsita " + t.nombre);
        go.transform.SetParent(padre, true);
        go.transform.position = pos + Vector3.up * 0.08f;
        // bolsita transparente
        var bolsa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(bolsa.GetComponent<Collider>());
        bolsa.transform.SetParent(go.transform, false);
        bolsa.transform.localScale = new Vector3(0.17f, 0.13f, 0.13f);
        var mb = Mat(new Color(1f, 1f, 1f, 0.25f), 0f, 0.8f);
        mb.SetFloat("_Surface", 1f); mb.SetFloat("_Blend", 0f);
        mb.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mb.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mb.SetInt("_ZWrite", 0); mb.renderQueue = 3000; mb.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        bolsa.GetComponent<Renderer>().sharedMaterial = mb;
        var mf = Mat(t.color, 0f, 0.6f);
        int n = Mathf.Clamp(Mathf.RoundToInt(kg * 5f), 2, 7);
        for (int i = 0; i < n; i++)
        {
            var f = GameObject.CreatePrimitive(t.forma);
            Destroy(f.GetComponent<Collider>());
            f.transform.SetParent(go.transform, false);
            f.transform.localPosition = new Vector3(Random.Range(-0.05f, 0.05f), -0.03f + (i / 3) * 0.04f, Random.Range(-0.035f, 0.035f));
            f.transform.localRotation = t.forma == PrimitiveType.Capsule ? Quaternion.Euler(0, Random.Range(0, 180), 80) : Random.rotation;
            f.transform.localScale = t.escala;
            f.GetComponent<Renderer>().sharedMaterial = mf;
        }
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.17f, 0.13f, 0.13f);
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = kg;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        var grab = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        grab.throwOnDetach = false;
        var fp = go.AddComponent<FrutaPesada>();
        fp.producto = p; fp.kg = kg;
        return go;
    }

    void Update()
    {
        if (plato == null) return;
        // Que hay sobre el plato
        FrutaPesada encima = null;
        foreach (var c in Physics.OverlapBox(plato.position + Vector3.up * 0.08f, new Vector3(0.14f, 0.08f, 0.11f)))
        {
            var f = c.GetComponentInParent<FrutaPesada>();
            if (f != null) { encima = f; break; }
        }
        if (encima == null)
        {
            pantalla.text = "0.000 kg";
            candidata = null;
            return;
        }
        pantalla.text = encima.kg.ToString("0.000") + " kg\nS/ " + encima.producto.precio.ToString("0.00");
        if (encima.pesada) return;
        if (candidata != encima) { candidata = encima; desde = Time.time; return; }
        if (Time.time - desde < 0.6f) return;   // que se estabilice
        encima.pesada = true;
        caja.AgregarProducto(encima.producto, 1);
        Haptica.Ambos(0.25f, 0.06f);
        Debug.Log("[Balanza] " + encima.producto.nombre + " -> S/ " + encima.producto.precio.ToString("0.00"));
    }
}

/// <summary>Bolsita de fruta que se cobra por peso en la balanza.</summary>
public class FrutaPesada : MonoBehaviour
{
    public Producto producto;
    public float kg;
    public bool pesada;
}
