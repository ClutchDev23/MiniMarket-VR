using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class LectorCodigos : MonoBehaviour
{
    public float distanciaLectura = 0.10f;
    public float enfriamiento = 0.30f;
    public AudioClip clipBip;
    public AudioSource audioLector;
    public Collider lectorCollider;
    public XRGrabInteractable grab;

    public UnityEvent<string> OnProductoEscaneado;

    Collider[] productos;
    float ultimaBip;

    void Start()
    {
        if (audioLector == null) audioLector = GetComponent<AudioSource>();
        if (lectorCollider == null) lectorCollider = GetComponent<Collider>();
        if (grab == null) grab = GetComponent<XRGrabInteractable>();
        RecolectarProductos();

        if (grab != null)
            grab.activated.AddListener(Escanear);
    }

    void OnDestroy()
    {
        if (grab != null)
            grab.activated.RemoveListener(Escanear);
    }

    void Escanear(ActivateEventArgs args)
    {
        if (Time.time - ultimaBip < enfriamiento) return;

        Collider detectado = ProductoEnRango();
        if (detectado == null) return;

        ultimaBip = Time.time;
        Debug.Log("[Lector] BIP producto: " + detectado.name);
        if (audioLector != null && clipBip != null)
            audioLector.PlayOneShot(clipBip);

        if (OnProductoEscaneado != null)
            OnProductoEscaneado.Invoke(detectado.name);
    }

    void RecolectarProductos()
    {
        productos = new Collider[0];

        var raiz = GameObject.Find("Productos");
        if (raiz == null)
        {
            Debug.LogWarning("[Lector] no se encontro la raiz 'Productos'");
            return;
        }

        var encontrados = raiz.GetComponentsInChildren<Collider>(true);
        var validos = new List<Collider>(encontrados.Length);
        foreach (var c in encontrados)
            if (c != null && !c.isTrigger)
                validos.Add(c);

        productos = validos.ToArray();
        Debug.Log("[Lector] productos listos para lectura: " + productos.Length);
    }

    Collider ProductoEnRango()
    {
        if (productos == null || productos.Length == 0) return null;

        Bounds bLector = lectorCollider != null ? lectorCollider.bounds : BoundsDeRenderers();
        float limite2 = distanciaLectura * distanciaLectura;

        for (int i = 0; i < productos.Length; i++)
        {
            var c = productos[i];
            if (c == null) continue;
            if (Distancia2(bLector, c.bounds) <= limite2)
                return c;
        }
        return null;
    }

    Bounds BoundsDeRenderers()
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        bool primero = true;
        var b = new Bounds();
        foreach (var r in rends)
        {
            if (primero) { b = r.bounds; primero = false; }
            else b.Encapsulate(r.bounds);
        }
        if (primero) b = new Bounds(transform.position, Vector3.one * 0.2f);
        return b;
    }

    static float Distancia2(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(b.min.x - a.max.x, a.min.x - b.max.x));
        float dy = Mathf.Max(0f, Mathf.Max(b.min.y - a.max.y, a.min.y - b.max.y));
        float dz = Mathf.Max(0f, Mathf.Max(b.min.z - a.max.z, a.min.z - b.max.z));
        return dx * dx + dy * dy + dz * dz;
    }
}
