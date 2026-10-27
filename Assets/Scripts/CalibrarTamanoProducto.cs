using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Aplica al producto agarrado el TAMANO calibrated en el menu.
///
/// QUE HACE
/// --------
/// En el menu (Ajustes) hay un control "Tamano de los productos" que va del 50% al
/// 200%. Este script escala el objeto mientras lo tienes en la mano, para que se vea
/// (y se note al agarrarlo) del tamano que el jugador prefiera.
///
/// La escala se aplica sobre la escala ORIGINAL del producto, de forma acumulativa:
/// si el jugador cambia el valor en caliente, el tamano se recalcula desde el valor
/// base y no se va multiplicando sin control.
///
/// Tambien guarda la escala base la primera vez que ve cada producto, para poder
/// volver a ella si hace falta.
/// </summary>
public class CalibrarTamanoProducto : MonoBehaviour
{
    [Header("Tamano (0.5 a 2)")]
    [Range(0.5f, 2f)] public float escala = 1f;

    [Header("Suavizado")]
    public bool suavizar = true;
    public float velocidad = 12f;

    XRGrabInteractable grab;
    Vector3 escalaBase;
    bool tieneBase;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        // Solo trabaja mientras el producto esta en la mano: con miles de productos en
        // la tienda, tenerlos todos activos cada frame gasta CPU en el Quest.
        if (grab != null)
        {
            grab.selectEntered.AddListener(_ => enabled = true);
            grab.selectExited.AddListener(_ => enabled = false);
        }
    }

    void Start()
    {
        enabled = grab != null && grab.isSelected;
    }

    void Update()
    {
        if (grab == null) return;
        if (!grab.isSelected) return;

        if (!tieneBase)
        {
            escalaBase = transform.localScale;
            tieneBase = true;
        }

        Vector3 objetivo = escalaBase * escala;

        if (suavizar)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, objetivo, 1f - Mathf.Exp(-velocidad * Time.deltaTime));
        }
        else
        {
            transform.localScale = objetivo;
        }
    }

    /// <summary>
    /// Restablece el tamano de un producto al original.
    /// </summary>
    public void Restablecer(Transform producto)
    {
        if (producto == null) return;
        var c = producto.GetComponent<CalibrarTamanoProducto>();
        if (c == null) return;
        c.transform.localScale = c.escalaBase;
    }
}
