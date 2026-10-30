using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Valor de un billete o moneda 3D (en centimos).</summary>
public class Dinero : MonoBehaviour { public int centimos; }

/// <summary>
/// CAJON DE DINERO (gaveta) de la caja.
///
///   - Caja metalica sobre el mostrador con una gaveta que se DESLIZA hacia el cajero.
///   - Adentro: 5 separadores de billetes (S/10, 20, 50, 100, 200) con su pinza y
///     6 cubetas de monedas (10c, 20c, 50c, S/1, S/2, S/5). Ya trae algo de sencillo.
///   - Se ABRE sola cuando cobras en efectivo (sonido de cajon), el pago del cliente
///     VUELA de la bandeja a su separador, el vuelto SALE de su separador hacia la
///     bandeja, y al terminar la venta la gaveta se CIERRA.
///   - Con tarjeta no se abre.
/// </summary>
public class CajonDinero : MonoBehaviour
{
    public static CajonDinero I { get; private set; }
    public static Vector3 CajonLocal = new Vector3(-0.45f, 0f, -0.62f);

    static readonly int[] Billetes = { 1000, 2000, 5000, 10000, 20000 };
    static readonly int[] Monedas = { 10, 20, 50, 100, 200, 500 };
    const float Ancho = 0.42f, Fondo = 0.40f, Alto = 0.10f, Recorrido = 0.30f;

    CajaRegistradora caja;
    Transform gaveta;
    float apertura, objetivo;     // 0 cerrada .. 1 abierta
    bool abierta;
    CajaRegistradora.Paso pasoAnterior;
    float cerrarEn = -1f;
    AudioSource sfx;
    readonly Dictionary<int, List<GameObject>> pilas = new Dictionary<int, List<GameObject>>();

    public bool Abierta => abierta;

    public void Iniciar(CajaRegistradora c, Transform m)
    {
        I = this;
        caja = c;
        transform.position = ObjetosCaja.SobreMostrador(m, CajonLocal);
        // +X local = hacia el cliente; la gaveta sale hacia -X (el cajero).
        transform.rotation = Quaternion.LookRotation(m.forward, Vector3.up);

        var metal = ObjetosCaja.Mat(new Color(0.16f, 0.17f, 0.19f), 0.55f);
        var metalClaro = ObjetosCaja.Mat(new Color(0.42f, 0.44f, 0.47f), 0.75f);
        var bandejaMat = ObjetosCaja.Mat(new Color(0.10f, 0.10f, 0.11f), 0.25f);
        var cromo = ObjetosCaja.Mat(new Color(0.8f, 0.82f, 0.85f), 0.95f);

        // Carcasa (sin frente, para que la gaveta salga).
        P("Techo", transform, new Vector3(Fondo, 0.006f, Ancho), new Vector3(0f, Alto, 0f), metal);
        P("Piso", transform, new Vector3(Fondo, 0.006f, Ancho), new Vector3(0f, 0.003f, 0f), metal);
        P("Fondo", transform, new Vector3(0.006f, Alto, Ancho), new Vector3(Fondo / 2f, Alto / 2f, 0f), metal);
        P("LadoA", transform, new Vector3(Fondo, Alto, 0.006f), new Vector3(0f, Alto / 2f, Ancho / 2f), metal);
        P("LadoB", transform, new Vector3(Fondo, Alto, 0.006f), new Vector3(0f, Alto / 2f, -Ancho / 2f), metal);
        // Pies de goma.
        foreach (var x in new[] { -0.17f, 0.17f }) foreach (var z in new[] { -0.18f, 0.18f })
            P("Pie", transform, new Vector3(0.03f, 0.006f, 0.03f), new Vector3(x, -0.003f, z), bandejaMat);

        // Gaveta.
        gaveta = new GameObject("Gaveta").transform;
        gaveta.SetParent(transform, false);
        float fondoG = Fondo - 0.02f, anchoG = Ancho - 0.02f, altoG = Alto - 0.02f;
        P("Base", gaveta, new Vector3(fondoG, 0.004f, anchoG), new Vector3(0f, 0.012f, 0f), bandejaMat);
        P("Frente", gaveta, new Vector3(0.012f, Alto - 0.004f, Ancho - 0.004f), new Vector3(-Fondo / 2f + 0.004f, Alto / 2f, 0f), metalClaro);
        P("Atras", gaveta, new Vector3(0.004f, altoG, anchoG), new Vector3(fondoG / 2f, 0.012f + altoG / 2f, 0f), bandejaMat);
        P("CostadoA", gaveta, new Vector3(fondoG, altoG, 0.004f), new Vector3(0f, 0.012f + altoG / 2f, anchoG / 2f), bandejaMat);
        P("CostadoB", gaveta, new Vector3(fondoG, altoG, 0.004f), new Vector3(0f, 0.012f + altoG / 2f, -anchoG / 2f), bandejaMat);
        // Manija y cerradura en el frente.
        P("Manija", gaveta, new Vector3(0.014f, 0.012f, 0.12f), new Vector3(-Fondo / 2f - 0.006f, Alto * 0.45f, 0f), cromo);
        var cerr = P("Cerradura", gaveta, new Vector3(0.006f, 0.016f, 0.016f), new Vector3(-Fondo / 2f - 0.004f, Alto * 0.75f, 0.15f), cromo);
        // Separadores: atras billetes (5), adelante monedas (6).
        float zB = anchoG / 5f;
        for (int i = 0; i <= 5; i++)
            P("SepB", gaveta, new Vector3(fondoG * 0.5f, altoG * 0.8f, 0.003f), new Vector3(fondoG * 0.25f, 0.012f + altoG * 0.4f, -anchoG / 2f + i * zB), bandejaMat);
        P("Medio", gaveta, new Vector3(0.003f, altoG * 0.8f, anchoG), new Vector3(0f, 0.012f + altoG * 0.4f, 0f), bandejaMat);
        float zM = anchoG / 6f;
        for (int i = 0; i <= 6; i++)
            P("SepM", gaveta, new Vector3(fondoG * 0.5f, altoG * 0.55f, 0.003f), new Vector3(-fondoG * 0.25f, 0.012f + altoG * 0.27f, -anchoG / 2f + i * zM), bandejaMat);
        // Pinzas cromadas sobre cada separador de billetes + etiqueta.
        for (int i = 0; i < 5; i++)
        {
            float z = -anchoG / 2f + (i + 0.5f) * zB;
            P("Pinza", gaveta, new Vector3(0.10f, 0.004f, 0.012f), new Vector3(fondoG * 0.25f, 0.06f, z), cromo);
            ObjetosCaja.Texto(gaveta, "S/ " + Billetes[i] / 100, new Vector3(fondoG / 2f - 0.012f, altoG * 0.85f, z), Quaternion.Euler(0f, -90f, 0f),
                new Vector2(120, 40), 0.00025f, 26, new Color(1f, 1f, 1f, 0.8f));
        }
        for (int i = 0; i < 6; i++)
        {
            float z = -anchoG / 2f + (i + 0.5f) * zM;
            ObjetosCaja.Texto(gaveta, Monedas[i] >= 100 ? "S/" + Monedas[i] / 100 : Monedas[i] + "c", new Vector3(-fondoG / 2f + 0.012f, altoG * 0.55f, z),
                Quaternion.Euler(0f, -90f, 0f), new Vector2(80, 34), 0.00022f, 24, new Color(1f, 1f, 1f, 0.7f));
        }

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 1f; sfx.minDistance = 0.6f; sfx.maxDistance = 6f; sfx.playOnAwake = false;

        // Sencillo inicial (como una caja de verdad al abrir el turno).
        foreach (var b in Billetes) for (int k = 0; k < (b >= 10000 ? 1 : 3); k++) Poner(BandejaDinero.Billete3D(b), b, false);
        foreach (var mo in Monedas) for (int k = 0; k < 4; k++) Poner(BandejaDinero.Moneda3D(mo), mo, false);
    }

    void OnDestroy() { if (I == this) I = null; }

    Transform P(string n, Transform padre, Vector3 tam, Vector3 pos, Material mat)
    {
        var g = ObjetosCaja.Caja(n, padre, tam, mat, false);
        g.transform.localPosition = pos;
        return g.transform;
    }

    /// <summary>Punto (local de la gaveta) donde va un valor; sube un poco con cada pieza.</summary>
    Vector3 Lugar(int c, int n)
    {
        float fondoG = Fondo - 0.02f, anchoG = Ancho - 0.02f;
        int i = System.Array.IndexOf(Billetes, c);
        if (i >= 0) return new Vector3(fondoG * 0.25f, 0.016f + n * 0.0016f, -anchoG / 2f + (i + 0.5f) * anchoG / 5f);
        int j = Mathf.Max(0, System.Array.IndexOf(Monedas, c));
        float r = 0.008f;
        return new Vector3(-fondoG * 0.25f + ((n % 3) - 1) * r * 2f, 0.016f + (n / 3) * 0.0022f, -anchoG / 2f + (j + 0.5f) * anchoG / 6f + ((n / 3) % 2) * r);
    }

    List<GameObject> Pila(int c)
    {
        if (!pilas.TryGetValue(c, out var l)) pilas[c] = l = new List<GameObject>();
        l.RemoveAll(x => x == null || x.transform.parent != gaveta);
        return l;
    }

    void Poner(GameObject go, int c, bool volando)
    {
        var l = Pila(c);
        if (l.Count >= 10) { Destroy(l[0]); l.RemoveAt(0); }
        var destino = Lugar(c, l.Count);
        l.Add(go);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
        if (!volando)
        {
            go.transform.SetParent(gaveta, false);
            go.transform.localPosition = destino;
            go.transform.localRotation = Quaternion.Euler(0f, Random.Range(-4f, 4f), 0f);
        }
        else StartCoroutine(Volar(go, destino));
    }

    IEnumerator Volar(GameObject go, Vector3 destinoLocal)
    {
        var desde = go.transform.position; var rotDesde = go.transform.rotation;
        float t = 0f;
        while (t < 1f && go != null)
        {
            t += Time.deltaTime / 0.5f;
            float k = Mathf.SmoothStep(0f, 1f, t);
            var hasta = gaveta.TransformPoint(destinoLocal);
            go.transform.position = Vector3.Lerp(desde, hasta, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.12f;
            go.transform.rotation = Quaternion.Slerp(rotDesde, gaveta.rotation, k);
            yield return null;
        }
        if (go == null) yield break;
        go.transform.SetParent(gaveta, true);
        go.transform.localPosition = destinoLocal;
        if (sfx != null) sfx.PlayOneShot(destinoLocal.x > 0 ? SonidosProcedurales.Billete() : SonidosProcedurales.Moneda(), 0.6f);
    }

    /// <summary>El pago del cliente entra a la gaveta (cada pieza a su separador).</summary>
    public void Guardar(GameObject go)
    {
        if (go == null) return;
        var d = go.GetComponent<Dinero>();
        Poner(go, d != null ? d.centimos : 1000, true);
    }

    /// <summary>Una pieza del vuelto sale de su separador hasta donde ya esta puesta (la bandeja).</summary>
    public void Sacar(GameObject go, int c)
    {
        if (go == null) return;
        var l = Pila(c);
        if (l.Count > 0) { var arriba = l[l.Count - 1]; l.RemoveAt(l.Count - 1); Destroy(arriba); }
        StartCoroutine(SalirHacia(go, gaveta.TransformPoint(Lugar(c, l.Count))));
    }

    IEnumerator SalirHacia(GameObject go, Vector3 desde)
    {
        var hasta = go.transform.position;
        float t = 0f;
        while (t < 1f && go != null)
        {
            t += Time.deltaTime / 0.5f;
            float k = Mathf.SmoothStep(0f, 1f, t);
            go.transform.position = Vector3.Lerp(desde, hasta, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.12f;
            yield return null;
        }
        if (go != null) go.transform.position = hasta;
    }

    public void Abrir()
    {
        if (abierta) return;
        abierta = true; objetivo = 1f; cerrarEn = -1f;
        if (sfx != null) sfx.PlayOneShot(SonidosProcedurales.Cajon());
        Haptica.Ambos(0.3f, 0.06f);
    }

    public void Cerrar()
    {
        if (!abierta) return;
        abierta = false; objetivo = 0f;
        if (sfx != null) sfx.PlayOneShot(SonidosProcedurales.Golpe(), 0.7f);
    }

    void Update()
    {
        // Animacion: sale rapido (resorte) y entra mas suave.
        apertura = Mathf.MoveTowards(apertura, objetivo, Time.deltaTime * (objetivo > apertura ? 4.5f : 2.5f));
        float e = objetivo > 0.5f ? 1f - (1f - apertura) * (1f - apertura) : apertura * apertura;
        gaveta.localPosition = new Vector3(-Recorrido * e, 0f, 0f);

        if (caja == null) return;
        var paso = caja.PasoActual;
        if (paso != pasoAnterior)
        {
            if (paso == CajaRegistradora.Paso.RecibirEfectivo || paso == CajaRegistradora.Paso.EntregarVuelto) Abrir();
            if (paso == CajaRegistradora.Paso.EntregarVuelto || (paso == CajaRegistradora.Paso.Completada && abierta))
                if (BandejaDinero.I != null) BandejaDinero.I.PagoAlCajon(this);
            if (paso == CajaRegistradora.Paso.Completada && abierta) cerrarEn = Time.time + 2.5f;
            if (paso == CajaRegistradora.Paso.Escaneando || paso == CajaRegistradora.Paso.Tarjeta) Cerrar();
            pasoAnterior = paso;
        }
        if (cerrarEn > 0f && Time.time > cerrarEn) { cerrarEn = -1f; Cerrar(); }
    }
}
