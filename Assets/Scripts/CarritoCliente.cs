using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// El carrito que empuja un cliente.
///
///   - El cliente toma un carrito libre cerca de la entrada.
///   - Mientras camina, el carrito va delante de el (no se usa la fisica: asi no
///     empuja estantes ni a otros clientes).
///   - Lo que elige se VE dentro del carrito.
///   - En la caja lo deja estacionado detras, saca sus productos uno por uno y los
///     pone en la mesa.
///   - Al irse se lo lleva, y cuando sale de la tienda el carrito vuelve a su sitio.
/// </summary>
public class CarritoCliente : MonoBehaviour
{
    [Tooltip("Gira el carrito 180 grados si el asa queda del lado equivocado.")]
    public static float GiroExtra = 0f;
    public static float DistanciaDelante = 0.72f;

    static readonly HashSet<Transform> enUso = new HashSet<Transform>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { enUso.Clear(); }

    public Transform Carrito { get; private set; }
    public bool Empujando { get; private set; }
    public int Cantidad => visibles.Count;

    Transform cliente;
    Vector3 posOriginal;
    Quaternion rotOriginal;
    float alturaSobreSuelo;
    Quaternion giro = Quaternion.identity;   // del eje largo del carrito al "adelante" del cliente
    Vector3 cestoMin, cestoMax;               // zona del cesto, en local del carrito

    Rigidbody rb;
    bool rbKinematic;
    readonly List<Collider> colliders = new List<Collider>();
    readonly List<Behaviour> apagados = new List<Behaviour>();
    readonly List<GameObject> visibles = new List<GameObject>();

    /// <summary>Un carrito que nadie usa, a menos de "radio" metros.</summary>
    public static Transform BuscarLibre(Vector3 cerca, float radio)
    {
        Transform mejor = null; float d = radio * radio;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Pref_Trolley") || enUso.Contains(t)) continue;
            if (t.parent != null && t.parent.name.StartsWith("Pref_Trolley")) continue;   // solo la raiz
            var rbx = t.GetComponentInChildren<XRGrabInteractable>();
            if (rbx != null && rbx.isSelected) continue;                               // lo tiene el jugador
            float dd = (t.position - cerca).sqrMagnitude;
            if (dd < d) { d = dd; mejor = t; }
        }
        return mejor;
    }

    public bool Tomar(Transform carrito, Transform quien)
    {
        if (carrito == null || enUso.Contains(carrito)) return false;
        enUso.Add(carrito);
        Carrito = carrito;
        cliente = quien;
        posOriginal = carrito.position;
        rotOriginal = carrito.rotation;

        if (Physics.Raycast(carrito.position + Vector3.up * 0.5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            alturaSobreSuelo = carrito.position.y - hit.point.y;

        // Fuera la fisica y el agarre mientras lo empuja el cliente.
        rb = carrito.GetComponentInChildren<Rigidbody>();
        if (rb != null) { rbKinematic = rb.isKinematic; rb.isKinematic = true; }
        foreach (var c in carrito.GetComponentsInChildren<Collider>()) if (c.enabled) { c.enabled = false; colliders.Add(c); }
        foreach (var b in carrito.GetComponentsInChildren<MonoBehaviour>())
            if (b.enabled && (b is CarritoEnElSuelo || b is XRGrabInteractable)) { b.enabled = false; apagados.Add(b); }

        // Eje largo del carrito y zona del cesto (en local).
        var rs = carrito.GetComponentsInChildren<Renderer>();
        var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
        foreach (var r in rs)
        {
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var p = carrito.InverseTransformPoint(new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
        }
        var tam = max - min;
        giro = Quaternion.Euler(0f, (tam.x > tam.z ? 90f : 0f) + GiroExtra, 0f);
        var centro = (min + max) * 0.5f;
        cestoMin = new Vector3(centro.x - tam.x * 0.22f, min.y + tam.y * 0.52f, centro.z - tam.z * 0.22f);
        cestoMax = new Vector3(centro.x + tam.x * 0.22f, min.y + tam.y * 0.60f, centro.z + tam.z * 0.22f);

        Empujando = true;
        return true;
    }

    /// <summary>Lo vuelve a empujar (despues de pagar).</summary>
    public void Empujar() { if (Carrito != null) Empujando = true; }

    /// <summary>Lo deja quieto en ese punto (por ejemplo detras de el en la caja).</summary>
    public void Estacionar(Vector3 pos, Quaternion rotCliente)
    {
        if (Carrito == null) return;
        Empujando = false;
        Carrito.position = new Vector3(pos.x, pos.y + alturaSobreSuelo, pos.z);
        Carrito.rotation = rotCliente * giro;
    }

    void LateUpdate()
    {
        if (!Empujando || Carrito == null || cliente == null) return;
        var adelante = cliente.forward; adelante.y = 0f;
        if (adelante.sqrMagnitude < 0.001f) return;
        adelante.Normalize();
        var objetivo = cliente.position + adelante * DistanciaDelante;
        objetivo.y = cliente.position.y + alturaSobreSuelo;
        var rot = Quaternion.LookRotation(adelante, Vector3.up) * giro;
        // Suave, como un carrito de verdad (no gira de golpe).
        Carrito.position = Vector3.Lerp(Carrito.position, objetivo, Time.deltaTime * 10f);
        Carrito.rotation = Quaternion.Slerp(Carrito.rotation, rot, Time.deltaTime * 6f);
    }

    /// <summary>Pone dentro del cesto una copia (solo visual) del producto que eligio.</summary>
    public void AgregarVisual(GameObject original)
    {
        if (Carrito == null || original == null) return;
        var mf = original.GetComponent<MeshFilter>();
        var mr = original.GetComponent<MeshRenderer>();
        if (mf == null || mr == null || mf.sharedMesh == null) return;

        var go = new GameObject(original.name + " (en carrito)");
        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = mr.sharedMaterials;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.transform.SetParent(Carrito, false);
        var ls = Carrito.lossyScale;
        var os = original.transform.lossyScale;
        go.transform.localScale = new Vector3(os.x / Mathf.Max(1e-4f, ls.x), os.y / Mathf.Max(1e-4f, ls.y), os.z / Mathf.Max(1e-4f, ls.z));
        int i = visibles.Count;
        // Se van acomodando en el cesto (y un poco mas arriba si ya hay muchos).
        float u = (i % 3) / 2f, v = ((i / 3) % 3) / 2f, capa = i / 9;
        go.transform.localPosition = new Vector3(
            Mathf.Lerp(cestoMin.x, cestoMax.x, u),
            cestoMin.y + capa * (cestoMax.y - cestoMin.y),
            Mathf.Lerp(cestoMin.z, cestoMax.z, v));
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * original.transform.rotation;
        visibles.Add(go);
    }

    /// <summary>Saca el ultimo producto del cesto (el cliente lo va a poner en la mesa).</summary>
    public GameObject SacarVisual()
    {
        if (visibles.Count == 0) return null;
        var go = visibles[visibles.Count - 1];
        visibles.RemoveAt(visibles.Count - 1);
        if (go != null) go.transform.SetParent(null, true);
        return go;
    }

    /// <summary>Deja el carrito como estaba (en su sitio, con su fisica).</summary>
    public void Devolver()
    {
        if (Carrito == null) return;
        foreach (var v in visibles) if (v != null) Destroy(v);
        visibles.Clear();
        Carrito.position = posOriginal;
        Carrito.rotation = rotOriginal;
        foreach (var c in colliders) if (c != null) c.enabled = true;
        foreach (var b in apagados) if (b != null) b.enabled = true;
        if (rb != null) { rb.isKinematic = rbKinematic; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        colliders.Clear(); apagados.Clear();
        enUso.Remove(Carrito);
        Carrito = null;
        Empujando = false;
    }

    void OnDestroy() { Devolver(); }
}
