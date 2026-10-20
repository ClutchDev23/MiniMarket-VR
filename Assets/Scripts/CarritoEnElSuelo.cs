using UnityEngine;

/// <summary>
/// Pega el carrito al SUELO y lo hace rodar como si tuviera RUEDITAS.
///
/// QUE HACE
/// --------
/// Un carrito de verdad se empuja, rueda por el suelo y nunca se queda suspendido
/// en el aire. Aqui el carrito:
///
///   - tiene un Rigidbody NORMAL (no kinematic), asi que se puede empujar con el
///     cuerpo y rueda por el suelo;
///   - NO puede subir ni bajar: el motor de fisica no le deja cambiar la altura
///     (FreezePositionY), asi que es IMPOSIBLE que se levante al agarrarlo, por
///     mucho que subas la mano;
///   - NO se vuelca (rotaciones X y Z bloqueadas), pero SI puede girar sobre su
///     eje vertical, como un carrito con una rueda giratoria;
///   - al arrancarlo, se le busca el suelo con un rayo y se baja a el (algunos
///     estaban flotando en el aire).
///
/// POR QUE NO SE LEVANTA (esto era lo que fallaba)
/// ----------------------------------------------
/// Cuando agarras un objeto, el sistema de agarre de XRI le calcula la velocidad
/// para llevarlo hasta la pose de tu mano y se la asigna al Rigidbody, INCLUDING
/// la parte vertical. Ademas, XRI escribe la posicion final en la fase
/// "OnBeforeRender", que se ejecuta DESPUES de todos los LateUpdate: por eso
/// cualquier correccion que se haga en LateUpdate se quedaba sin efecto y el
/// carrito se veia flotar.
///
/// La solucion no es "arreglarlo despues", es IMPEDIRLO: con FreezePositionY el
/// motor de fisica no puede mover el carrito en vertical, y como el agarre usa
/// velocidades (VelocityTracking) y no posiciones, nunca llega a levantarlo.
///
/// Se puede poner a mano en el Inspector o se puede instalar solo (mira
/// AjustesAutomaticos, que lo anade a todos los carritos de la escena).
/// </summary>
public class CarritoEnElSuelo : MonoBehaviour
{
    [Header("Pegado al suelo")]
    [Tooltip("Mantiene el carrito a la altura del suelo, sin gravedad.")]
    public bool pegarAlSuelo = true;

    [Tooltip("Altura del suelo a la que se pega (se busca con un rayo).")]
    public float alturaSuelo = 0f;

    [Tooltip("Distancia del rayo hacia abajo para buscar el suelo.")]
    public float distanciaRayo = 6f;

    [Tooltip("Capas donde se busca el suelo. Por defecto, todas.")]
    public LayerMask capasSuelo = ~0;

    [Header("Empujable y con ruedas")]
    [Tooltip("Permite que el jugador empuje el carrito con el cuerpo o con la mano.")]
    public bool empujable = true;

    [Tooltip("Si lo activas, el carrito SI se puede levantar al agarrarlo (deja de estar pegado al suelo).")]
    public bool permitirLevantar = false;

    [Tooltip("Masa del carrito. Mas pesado = se mueve menos de golpe.")]
    public float masa = 30f;

    [Tooltip("Frena del carrito. Cuanto mas bajo, mas rueda (como con ruedas).")]
    [Range(0f, 10f)] public float freno = 1.2f;

    [Tooltip("Frena el giro sobre su eje vertical, para que no gire como un trompo.")]
    [Range(0f, 20f)] public float frenoGiro = 6f;

    [Tooltip("Permite que el carrito gire sobre si mismo (como un carrito con rueda giratoria).")]
    public bool puedeGirar = true;

    Rigidbody rb;
    bool sueloConocido;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        // El carrito se puede empujar: Rigidbody normal, sin kinematic.
        rb.isKinematic = !empujable;

        // Sin gravedad: la altura la controlamos nosotros y, sobre todo, el
        // motor de fisica no le deja cambiar de altura.
        rb.useGravity = false;

        rb.constraints = ConstruirConstraints();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.mass = Mathf.Max(0.1f, masa);
        rb.linearDamping = freno;
        rb.angularDamping = frenoGiro;

        // El centro de masa bajo, como si las ruedasaran por debajo: asi el
        // carrito gira sobre su eje vertical sin volcarse.
        rb.centerOfMass = new Vector3(0f, -0.35f, 0f);

        alturaSuelo = transform.position.y;
    }

    /// <summary>
    /// Las restricciones del Rigidbody:
    ///   - FreezePositionY: el carrito NO puede cambiar de altura (no se levanta).
    ///   - FreezeRotationX y FreezeRotationZ: no se vuelca.
    ///   - la rotacion en Y se deja libre (o no) segun "puedeGirar".
    /// </summary>
    RigidbodyConstraints ConstruirConstraints()
    {
        RigidbodyConstraints c = RigidbodyConstraints.FreezePositionY;

        c |= RigidbodyConstraints.FreezeRotationX;
        c |= RigidbodyConstraints.FreezeRotationZ;

        if (!puedeGirar) c |= RigidbodyConstraints.FreezeRotationY;

        if (permitirLevantar) c &= ~RigidbodyConstraints.FreezePositionY;

        return c;
    }

    void Start()
    {
        BuscarSuelo();

        // Si el carrito estaba en el aire (algunos lo estaban), se baja al suelo
        // de golpe. Con FreezePositionY a partir de aqui ya no se movera en alto.
        if (pegarAlSuelo && sueloConocido)
        {
            Vector3 p = rb.position;
            p.y = alturaSuelo;
            rb.position = p;
            transform.position = p;
        }
    }

    void FixedUpdate()
    {
        if (!pegarAlSuelo || permitirLevantar) return;

        if (!sueloConocido) BuscarSuelo();

        // Red de seguridad: si algo externo ha movido el carrito en vertical
        // (por ejemplo, un editor de niveles), se vuelve a poner en el suelo.
        if (sueloConocido && Mathf.Abs(rb.position.y - alturaSuelo) > 0.01f)
        {
            Vector3 p = rb.position;
            p.y = alturaSuelo;
            rb.position = p;
        }
    }

    /// <summary>
    /// Busca el suelo con un rayo hacia abajo y guarda su altura.
    ///
    /// OJO: el rayo sale de dentro del propio carrito, asi que hay que descartar
    /// los impactos con sus propios colisionadores (si no, el "suelo" seria el
    /// propio carrito y no se moveria nunca). Y se busca el impacto MAS BAJO: el
    /// suelo de verdad, no un estante que pase por debajo.
    /// </summary>
    void BuscarSuelo()
    {
        Vector3 origen = transform.position + Vector3.up * 0.5f;

        var impactos = Physics.RaycastAll(origen, Vector3.down, distanciaRayo, capasSuelo, QueryTriggerInteraction.Ignore);

        float masBajo = float.MinValue;
        bool alguno = false;

        foreach (var h in impactos)
        {
            if (h.collider == null) continue;
            if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;
            if (transform.IsChildOf(h.collider.transform)) continue;

            if (!alguno || h.point.y < masBajo)
            {
                masBajo = h.point.y;
                alguno = true;
            }
        }

        if (alguno)
        {
            alturaSuelo = masBajo;
            sueloConocido = true;
        }
        else
        {
            // Sin suelo debajo: se mantiene la altura actual para no teletransportarlo.
            alturaSuelo = transform.position.y;
            sueloConocido = false;
        }
    }
}
