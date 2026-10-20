using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Corrige el conflicto entre los sockets (huecos de los estantes) y el agarre directo
/// de los productos.
///
/// POR QUE PASABA
/// -------------
/// En la escena hay 642 XRSocketInteractor (los huecos de los estantes) y 320
/// productos. Cada socket esta exactamente en la posicion de su producto y tiene
/// "Socket Active" activado.
///
/// Cuando un socket selecciona un interactable, XRI le anade su propio
/// XRSocketGrabTransformer a la lista de transformers del producto. Esos transformers
/// se procesan en orden de registro y CADA UNO sobrescribe la pose objetivo del
/// siguiente, asi que el ultimo en registrarse (el del socket) gana.
///
/// Como el transformer del socket coloca el producto en la pose del socket (rotacion
/// identidad, porque el socket no tiene rotacion propia), el producto se queda
/// bloqueado en ese angulo y deja de seguir a la mano. De ahi los dos sintomas:
///
///   - "se agarra en un solo angulo"
///   - "no sigue la direccion de las manos"
///
/// QUE HACE ESTE SCRIPT
/// ---------------------
/// 1. Desactiva los sockets que NO tienen ningun producto en su hueco: no sirven para
///    nada (son huecos vacios) y competian con el agarre.
///
/// 2. Para los sockets que SI tienen producto, comprueba en cada frame si el socket es
///    el que esta sujetando el producto mientras el jugador lo agarra con la mano. Si
///    es asi, se le quita el transformer de agarre al producto para que mande la mano.
///
/// Se ejecuta solo en la escena de juego.
/// </summary>
[DefaultExecutionOrder(-40)]
public class CorregirConflictoSockets : MonoBehaviour
{
    [Header("Sockets")]
    [Tooltip("Desactiva los sockets vacios (los que no tienen ningun producto en su hueco).")]
    public bool desactivarSocketsVacios = true;

    [Tooltip("Quita el transformer del socket cuando el jugador agarra el producto con la mano.")]
    public bool liberarProductoDelSocketAlAgarrar = true;

    [Tooltip("Altura sobre el socket a la que se busca el producto (metros).")]
    public float alturaBusquedaProducto = 0.12f;

    [Tooltip("Radio horizontal para buscar el producto de un socket (metros).")]
    public float radioBusquedaProducto = 0.08f;

    readonly List<XRSocketInteractor> sockets = new List<XRSocketInteractor>();
    readonly List<XRGrabInteractable> productos = new List<XRGrabInteractable>();
    readonly Dictionary<XRSocketInteractor, XRGrabInteractable> productoDeSocket =
        new Dictionary<XRSocketInteractor, XRGrabInteractable>();

    void Start()
    {
        Recopilar();
    }

    void Recopilar()
    {
        sockets.Clear();
        productos.Clear();
        productoDeSocket.Clear();

        foreach (var s in FindObjectsByType<XRSocketInteractor>(FindObjectsInactive.Include))
            sockets.Add(s);

        foreach (var g in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include))
            productos.Add(g);

        // Empareja cada socket con el producto que tiene encima.
        foreach (var s in sockets)
        {
            Vector3 pos = s.transform.position;
            XRGrabInteractable mejor = null;
            float mejorDist = radioBusquedaProducto;

            foreach (var g in productos)
            {
                if (g == null) continue;

                Vector3 delta = g.transform.position - pos;
                // El producto esta encima del socket (el socket esta en su base).
                if (delta.y < -0.02f || delta.y > alturaBusquedaProducto) continue;

                float d = new Vector2(delta.x, delta.z).magnitude;
                if (d < mejorDist)
                {
                    mejorDist = d;
                    mejor = g;
                }
            }

            productoDeSocket[s] = mejor;
        }

        int conProducto = 0;
        int vaciosDesactivados = 0;

        foreach (var kv in productoDeSocket)
        {
            if (kv.Value != null) { conProducto++; continue; }

            // Hueco vacio: se desactiva para que no compita con el agarre.
            if (desactivarSocketsVacios && kv.Key != null)
            {
                kv.Key.socketActive = false;
                vaciosDesactivados++;
            }
        }

        Debug.Log("[Sockets] " + sockets.Count + " sockets | " + conProducto + " con producto | " +
                  vaciosDesactivados + " huecos vacios desactivados.");
    }

    void Update()
    {
        if (liberarProductoDelSocketAlAgarrar) LiberarProductosEnAgarrados();
    }

    /// <summary>
    /// Si un producto esta siendo agarrado por la mano (no solo por su socket), se le
    /// quita al socket el transformer de agarre para que la mano mande en la pose.
    /// </summary>
    void LiberarProductosEnAgarrados()
    {
        foreach (var kv in productoDeSocket)
        {
            var s = kv.Key;
            var producto = kv.Value;
            if (s == null || producto == null) continue;

            // El socket solo importa mientras este sujetando ESE producto.
            if (!s.hasSelection) continue;
            if (s.interactablesSelected.Count == 0) continue;
            if (s.interactablesSelected[0] != producto) continue;

            // Si el jugador lo tiene agarrado con la mano, el socket debe soltarlo.
            bool loAgarraLaMano = false;
            foreach (var i in producto.interactorsSelecting)
            {
                if (i is NearFarInteractor) { loAgarraLaMano = true; break; }
            }

            if (!loAgarraLaMano) continue;

            // Se le quita el transformer de agarre al producto. Es la misma llamada
            // que usa el propio XRSocketInteractor para soltarlo.
            QuitarTransformerDelSocket(s, producto);
        }
    }

    /// <summary>
    /// Quita el transformer del socket al producto. Se hace a traves de la API publica
    /// de RemoveSingleGrabTransformer, usando el transformer que el propio socket
    /// registro (se localiza entre los transformers del producto).
    /// </summary>
    static void QuitarTransformerDelSocket(XRSocketInteractor socket, XRGrabInteractable producto)
    {
        // El socket guarda su transformer en la lista de transformers del producto.
        // Se busca el que pertenece a este socket y se elimina.
        var transformers = new List<IXRGrabTransformer>();
        producto.GetSingleGrabTransformers(transformers);

        for (int i = transformers.Count - 1; i >= 0; i--)
        {
            var t = transformers[i];
            if (t == null) continue;

            // El transformer del socket es el que sabe colocar el interactable en la
            // pose del socket. Se identifica porque es el unico que no es el general.
            if (t is XRSocketGrabTransformer)
            {
                producto.RemoveSingleGrabTransformer(t);
            }
        }
    }
}
