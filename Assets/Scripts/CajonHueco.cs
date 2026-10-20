using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// El CESTO del carrito: una caja HUECA con paredes, piso y repisas, con la cara de
/// arriba abierta.
///
/// QUE ES
/// ------
/// El carrito es un cesto. Si su colisionador fuera una caja maciza, los
/// productos no se podrian meter dentro (quedarian dentro de un cubo solido) y
/// ademas el rayo del mando pegaria a un sitio vacio.
///
/// Con esta caja hueca:
///   - el producto baja al MEDIO del cesto y se queda ahí;
///   - los BORDES quedan arriba, así que no se escapa;
///   - hay un piso intermedio (2 pisos en total).
///
/// TODO SE EDITA AQUI
/// -----------------
/// Los parametros del cajon (centro, tamaño, grosor y repisas) se ajustan desde
/// este componente en el Inspector, y las cajas se regeneran solas.
///
/// La caja maestra NO es un BoxCollider: es un componente, para que no aparezca
/// ningun colisionador "desmarcado" en el Inspector.
/// </summary>
public class CajonHueco : MonoBehaviour
{
    [Header("El hueco del cesto (editable aqui)")]
    [Tooltip("Centro del hueco, en el espacio local del carrito.")]
    public Vector3 centro = new Vector3(0f, 0.54f, -0.11f);

    [Tooltip("Tamano del hueco (x = ancho, y = alto, z = fondo).")]
    public Vector3 tamano = new Vector3(0.65f, 1.07f, 1.13f);

    [Header("Paredes")]
    [Tooltip("Grosor de las paredes, en metros.")]
    public float grosor = 0.04f;

    [Tooltip("Repisas intermedias. 1 = dos pisos (el piso y el medio cesto).")]
    [Range(0, 4)] public int repisas = 1;

    [Tooltip("Deja la cara de ARRIBA abierta, para poder meter cosas dentro.")]
    public bool abiertaArriba = true;

    [Header("Avanzado")]
    [Tooltip("Regenera las cajas al empezar. Si lo quitas, solo se regeneran cuando se llama a Generar() a mano.")]
    public bool generarAlEmpezar = true;

    const string NombreContenedor = "Colliders";

    void Awake()
    {
        if (generarAlEmpezar) Generar();
    }

    /// <summary>
    /// Crea (o rehace) las cajas del cajon: piso, paredes y repisas.
    /// </summary>
    [ContextMenu("Generar el cajon hueco")]
    public void Generar()
    {
        var contenedor = BuscarContenedor();
        if (contenedor == null)
        {
            var go = new GameObject(NombreContenedor);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            contenedor = go.transform;
        }
        else
        {
            // Se borran las cajas anteriores.
            var viejas = new List<GameObject>();
            foreach (Transform h in contenedor) viejas.Add(h.gameObject);
            for (int i = 0; i < viejas.Count; i++)
            {
                if (viejas[i] == null) continue;

                // En el editor hay que usar DestroyImmediate: Destroy no borra nada
                // hasta el final del frame y las cajas se acumulaban.
                if (Application.isPlaying) Destroy(viejas[i]);
                else DestroyImmediate(viejas[i]);
            }
        }

        // El grosor, en el espacio local del carrito.
        Vector3 escala = transform.lossyScale;
        Vector3 g = new Vector3(
            grosor / Mathf.Max(0.0001f, Mathf.Abs(escala.x)),
            grosor / Mathf.Max(0.0001f, Mathf.Abs(escala.y)),
            grosor / Mathf.Max(0.0001f, Mathf.Abs(escala.z)));

        float yMin = centro.y - tamano.y * 0.5f;
        float yMax = centro.y + tamano.y * 0.5f;

        // Piso
        AddCaja(contenedor, new Vector3(centro.x, yMin + g.y * 0.5f, centro.z), new Vector3(tamano.x, g.y, tamano.z));

        // Techo (si no esta abierta por arriba)
        if (!abiertaArriba)
            AddCaja(contenedor, new Vector3(centro.x, yMax - g.y * 0.5f, centro.z), new Vector3(tamano.x, g.y, tamano.z));

        // Paredes: los bordes quedan arriba
        AddCaja(contenedor, new Vector3(centro.x + tamano.x * 0.5f - g.x * 0.5f, centro.y, centro.z), new Vector3(g.x, tamano.y, tamano.z));
        AddCaja(contenedor, new Vector3(centro.x - tamano.x * 0.5f + g.x * 0.5f, centro.y, centro.z), new Vector3(g.x, tamano.y, tamano.z));
        AddCaja(contenedor, new Vector3(centro.x, centro.y, centro.z + tamano.z * 0.5f - g.z * 0.5f), new Vector3(tamano.x, tamano.y, g.z));
        AddCaja(contenedor, new Vector3(centro.x, centro.y, centro.z - tamano.z * 0.5f + g.z * 0.5f), new Vector3(tamano.x, tamano.y, g.z));

        // Repisas intermedias
        for (int i = 1; i <= repisas; i++)
        {
            float y = Mathf.Lerp(yMin + g.y * 0.5f, yMax - g.y * 0.5f, i / (float)(repisas + 1));
            AddCaja(contenedor, new Vector3(centro.x, y, centro.z), new Vector3(tamano.x, g.y, tamano.z));
        }
    }

    /// <summary>
    /// Busca el contenedor de las cajas. Si hay varios (por si se acumularon
    /// alguna vez), se quedan con el primero y se borran los demas.
    /// </summary>
    Transform BuscarContenedor()
    {
        Transform primero = null;
        var sobrantes = new List<Transform>();

        foreach (Transform h in transform)
        {
            if (h.name != NombreContenedor) continue;
            if (primero == null) primero = h;
            else sobrantes.Add(h);
        }

        for (int i = 0; i < sobrantes.Count; i++)
        {
            if (sobrantes[i] == null) continue;
            if (Application.isPlaying) Destroy(sobrantes[i].gameObject);
            else DestroyImmediate(sobrantes[i].gameObject);
        }

        return primero;
    }

    static void AddCaja(Transform padre, Vector3 centro, Vector3 tam)
    {
        var go = new GameObject("Caja");
        go.transform.SetParent(padre, false);
        go.transform.localPosition = centro;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var b = go.AddComponent<BoxCollider>();
        b.center = Vector3.zero;
        b.size = tam;
        b.isTrigger = false;
    }
}
