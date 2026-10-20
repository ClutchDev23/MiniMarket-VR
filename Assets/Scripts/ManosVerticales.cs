using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Pone las MANOS en la pose "LISTO PARA AGARRAR": verticales, con las palmas
/// mirando hacia dentro (hacia el centro del cuerpo), como en los juegos de VR.
///
/// QUE HACE
/// --------
/// En el rig de manos (XR Origin Hands) lo que se ve son las manos, no los mandos:
/// el "Hand Visualizer" dibuja la mano a partir del esqueleto que sigue el
/// dispositivo. Esa mano sale en la posicion "de agarre" (los dedos hacia el
/// mando, tumbada), que es comodo para pulsar pero deja la mano cruzada y
/// ptraba ver por donde se agarra.
///
/// Aqui la mano se pone en pose de "agarrar":
///
///   - los DEDOS hacia ARRIBA (vertical), y
///   - la PALMA hacia DENTRO (hacia el centro del cuerpo).
///
/// COMO SE HACE (sin adivinar angulos)
/// -----------------------------------
/// La convencion de XR Hands (la misma que usa el propio codigo del paquete para
/// calcular la orientacion de la mano) es:
///
///   - los dedos extendidos van hacia +Z del esqueleto, y
///   - la palma mira hacia -Z... no, hacia -Y del esqueleto.
///
/// O sea: con la rotacion actual de la mano, los dedos son (rotacion * adelante) y
/// la palma mira a (rotacion * abajo). Con eso se monta una base de la mano y otra
/// con la pose que se quiere, y la rotacion que lleva de una a la otra se aplica.
///
/// Se aplica en el MUNDO, asi que las manos quedan verticales de verdad, y solo
/// se gira la MANO: los puntos de agarre (pinza) se mueven con ella para que
/// sigan falling en los dedos.
///
/// La correccion se puede ajustar a mano con los campos de este script.
/// </summary>
[DefaultExecutionOrder(200)]
public class ManosVerticales : MonoBehaviour
{
    [Header("Pose")]
    [Tooltip("Pone los dedos hacia arriba (mano en vertical).")]
    public bool vertical = true;

    [Tooltip("Pone la palma mirando hacia dentro (hacia el centro del cuerpo).")]
    public bool palmaHaciaDentro = true;

    [Header("Puntos de agarre")]
    [Tooltip("Gira tambien el punto de pinza, para que siga falling en los dedos.")]
    public bool girarPuntoDePinza = true;

    [Tooltip("Gira el punto de apuntado. OJO: esto es la direccion hacia la que se apunta con el mando, asi que si se gira, el rayo tambien.")]
    public bool girarPuntoDeApuntado = false;

    [Header("Correcciones manuales (grados)")]
    public Vector3 correccionIzquierda = Vector3.zero;
    public Vector3 correccionDerecha = Vector3.zero;

    [Header("Suavizado")]
    [Tooltip("Velocidad con la que se aplica la correccion. 0 = inmediata.")]
    [Range(0f, 30f)] public float suavizado = 12f;

    void LateUpdate()
    {
        var origen = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();

        // La referencia de "arriba" y "derecha" es la del XR Origin (asi el
        // jugador puede girar el espacio sin que las manos se tuerzan).
        Vector3 arriba = origen != null ? origen.transform.up : Vector3.up;
        Vector3 derecha = origen != null ? origen.transform.right : Vector3.right;

        foreach (var eventos in FindObjectsByType<XRHandTrackingEvents>(FindObjectsInactive.Include))
        {
            if (!eventos.gameObject.activeInHierarchy) continue;

            var driver = eventos.GetComponent<XRHandSkeletonDriver>();
            if (driver == null || driver.rootTransform == null) continue;

            bool izquierda = eventos.handedness == Handedness.Left;

            // ------------------------------------------------------ estado actual
            // Convencion de XR Hands: los dedos van a +Z y la palma mira a -Y.
            Quaternion rotMano = driver.rootTransform.rotation;
            Vector3 dedos = rotMano * Vector3.forward;
            Vector3 palma = rotMano * Vector3.down;

            // ------------------------------------------------------ pose objetivo
            Vector3 objetivoDedos = vertical ? arriba : dedos;

            // La palma mira hacia dentro: la mano derecha mira a la izquierda del
            // jugador y la izquierda a la derecha (palmas enfrentadas).
            Vector3 objetivoPalma = palmaHaciaDentro
                ? (izquierda ? derecha : -derecha)
                : palma;

            Quaternion correccion = CalcularCorreccion(dedos, palma, objetivoDedos, objetivoPalma);

            // La correccion manual de cada mano.
            Vector3 extra = izquierda ? correccionIzquierda : correccionDerecha;
            if (extra != Vector3.zero)
                correccion = correccion * Quaternion.Euler(extra);

            // ------------------------------------------------------ aplicacion
            Aplicar(driver.rootTransform, correccion, suavizado);

            if (girarPuntoDePinza) GirarPunto("Pinch Grab Pose", correccion, izquierda, suavizado);
            if (girarPuntoDeApuntado) GirarPunto("Aim Pose", correccion, izquierda, suavizado);
        }
    }

    /// <summary>
    /// Gira el transform indicado con la correccion, en el mundo.
    /// </summary>
    void Aplicar(Transform t, Quaternion correccion, float suave)
    {
        if (t == null) return;

        Quaternion objetivo = correccion * t.rotation;

        if (suave <= 0.01f)
        {
            t.rotation = objetivo;
            return;
        }

        t.rotation = Quaternion.Slerp(t.rotation, objetivo, 1f - Mathf.Exp(-suave * Time.deltaTime));
    }

    /// <summary>
    /// Gira un punto concreto del rig (por nombre) con la misma correccion.
    /// </summary>
    void GirarPunto(string nombre, Quaternion correccion, bool izquierda, float suave)
    {
        var origen = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origen == null) return;

        foreach (var t in origen.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != nombre) continue;

            // Solo el de esta mano: se mira que sea hijo del mismo "Left Hand" /
            // "Right Hand" que la mano que se esta corrigiendo.
            Transform raiz = t.parent;
            bool manoIzquierda = raiz != null &&
                                 (raiz.name.Contains("Left") || raiz.name.Contains("Izquierd"));
            if (manoIzquierda != izquierda) continue;

            Aplicar(t, correccion, suave);
            break;
        }
    }

    /// <summary>
    /// La rotacion que lleva los ejes de la mano (dedos y palma) a los ejes
    /// objetivo (dedos arriba y palma hacia dentro).
    /// </summary>
    static Quaternion CalcularCorreccion(Vector3 dedos, Vector3 palma, Vector3 objetivoDedos, Vector3 objetivoPalma)
    {
        // Base actual de la mano.
        Vector3 e0 = dedos.normalized;
        Vector3 e1 = Vector3.ProjectOnPlane(palma, e0);
        if (e1.sqrMagnitude < 0.000001f) return Quaternion.identity;
        e1 = e1.normalized;
        Vector3 e2 = Vector3.Cross(e0, e1);

        // Base objetivo.
        Vector3 f0 = objetivoDedos.normalized;
        Vector3 f1 = Vector3.ProjectOnPlane(objetivoPalma, f0);
        if (f1.sqrMagnitude < 0.000001f) return Quaternion.identity;
        f1 = f1.normalized;
        Vector3 f2 = Vector3.Cross(f0, f1);

        Matrix4x4 baseActual = new Matrix4x4(
            new Vector4(e0.x, e0.y, e0.z, 0f),
            new Vector4(e1.x, e1.y, e1.z, 0f),
            new Vector4(e2.x, e2.y, e2.z, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        Matrix4x4 baseObjetivo = new Matrix4x4(
            new Vector4(f0.x, f0.y, f0.z, 0f),
            new Vector4(f1.x, f1.y, f1.z, 0f),
            new Vector4(f2.x, f2.y, f2.z, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        return (baseObjetivo * baseActual.transpose).rotation;
    }
}
