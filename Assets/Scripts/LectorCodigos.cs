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

    /// <summary>
    /// Se dispara con el objeto escaneado y su ficha de la base de datos
    /// (la ficha es null si el producto no tiene precio registrado).
    /// La CajaRegistradora escucha este evento.
    /// </summary>
    public event System.Action<GameObject, Producto> ProductoEscaneado;

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

        // El collider puede estar en un hijo: el producto es el objeto agarrable.
        var grabProducto = detectado.GetComponentInParent<XRGrabInteractable>();
        var objProducto = grabProducto != null ? grabProducto.gameObject : detectado.gameObject;

        var info = BaseDatosProductos.Buscar(objProducto.name);
        Debug.Log("[Lector] BIP " + objProducto.name + (info != null
            ? " -> " + info.nombre + " " + BaseDatosProductos.Formatear(info.precio)
            : " (SIN PRECIO en la base de datos)"));

        if (audioLector != null && clipBip != null)
            audioLector.PlayOneShot(clipBip);

        if (OnProductoEscaneado != null)
            OnProductoEscaneado.Invoke(objProducto.name);

        ProductoEscaneado?.Invoke(objProducto, info);
    }

    /// <summary>Vuelve a buscar productos (los clientes dejan productos nuevos en la caja).</summary>
    public void ActualizarProductos() { RecolectarProductos(); }

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

        // Devuelve el producto MAS CERCANO (antes era el primero de la lista, y con
        // varios productos juntos en el mostrador podia leer el equivocado).
        Collider mejor = null;
        float mejorD = float.MaxValue;
        Vector3 centro = bLector.center;
        for (int i = 0; i < productos.Length; i++)
        {
            var c = productos[i];
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            if (Distancia2(bLector, c.bounds) > limite2) continue;

            float d = (c.bounds.center - centro).sqrMagnitude;
            if (d < mejorD) { mejorD = d; mejor = c; }
        }
        return mejor;
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
