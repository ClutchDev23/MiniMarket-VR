using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Un producto que esta en un estante de la tienda llena.
///
/// Mientras esta en el estante:
///   - Es kinematico (no se cae ni gasta fisica).
///   - Su dibujo lo hace el BloqueProductos (todos juntos en pocas llamadas), asi el
///     Quest aguanta miles de productos.
///
/// Cuando lo agarras:
///   - Se "libera": vuelve a dibujarse solo y el bloque lo quita de su dibujo.
///   - Al soltarlo, tiene gravedad y se cae como un objeto normal.
///
/// Lo crea la herramienta Tools/Minimarket/Productos/Llenar tienda.
/// </summary>
[DisallowMultipleComponent]
public class ProductoEstante : MonoBehaviour
{
    [HideInInspector] public BloqueProductos bloque;
    public bool Liberado { get; private set; }

    XRGrabInteractable grab;
    MeshRenderer rend;
    Rigidbody rb;

    public MeshRenderer Renderer => rend != null ? rend : (rend = GetComponent<MeshRenderer>());

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rend = GetComponent<MeshRenderer>();
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        if (grab == null) return;
        grab.selectEntered.AddListener(AlAgarrar);
        grab.selectExited.AddListener(AlSoltar);
    }

    void OnDisable()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(AlAgarrar);
        grab.selectExited.RemoveListener(AlSoltar);
    }

    void AlAgarrar(SelectEnterEventArgs args)
    {
        Liberar();
        Haptica.Pulso(args.interactorObject, 0.18f, 0.03f);
    }

    // ------------------------------------------------------------------ golpe al caer
    static float ultimoGolpeGlobal;
    float ultimoGolpe;

    void OnCollisionEnter(Collision c)
    {
        if (!Liberado || (grab != null && grab.isSelected)) return;
        float v = c.relativeVelocity.magnitude;
        if (v < 0.9f) return;
        if (Time.time - ultimoGolpe < 0.25f || Time.time - ultimoGolpeGlobal < 0.04f) return;
        ultimoGolpe = ultimoGolpeGlobal = Time.time;
        float vol = Mathf.Clamp01((v - 0.9f) / 3f) * 0.8f + 0.15f;
        AudioSource.PlayClipAtPoint(SonidosProcedurales.Golpe(), c.GetContact(0).point, vol);
    }

    void AlSoltar(SelectExitEventArgs args)
    {
        // XRI devuelve el Rigidbody a como estaba al agarrarlo (kinematico). Al soltarlo
        // en otro lado tiene que caer como un objeto normal.
        if (rb == null) return;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();
    }

    /// <summary>Lo saca del dibujo del bloque y lo vuelve un objeto independiente.</summary>
    public void Liberar()
    {
        if (Liberado) return;
        Liberado = true;
        if (Renderer != null) Renderer.enabled = true;
        if (bloque != null) bloque.Quitar(this);
        bloque = null;
    }

    /// <summary>Prepara una COPIA (la que deja un cliente en la caja): independiente y visible.</summary>
    public static void ComoSuelto(GameObject copia)
    {
        var pe = copia.GetComponent<ProductoEstante>();
        if (pe == null) return;
        pe.Liberado = true;
        pe.bloque = null;
        var r = copia.GetComponent<MeshRenderer>();
        if (r != null) r.enabled = true;
    }
}
