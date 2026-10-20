using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Calibra la longitud de los brazos para el caminar por balanceo.
///
/// QUE HACE
/// --------
/// El caminar con brazos (ArmSwingLocomotion) detecta "brazo estirado" comparando la
/// distancia cabeza-palma con un valor fijo (extendedDistance = 0.35 m). Ese valor es
/// generico y no se ajusta a la persona: con brazos cortos nunca detecta el estirado
/// y con brazos largos lo detecta demasiado pronto.
///
/// Aqui se mide la distancia REAL de la cabeza a la mano y se guarda. Con esa medida
/// el umbral se calcula solo, asi que funciona para cualquiera.
///
/// Se mide con el boton "Calibrar brazos" del menu: el jugador pone las manos
/// estiradas hacia delante (como si agarrara algo) y se pulsa el boton.
///
/// La medida se guarda en PlayerPrefs y se aplica al ArmSwingLocomotion de la escena
/// de juego.
/// </summary>
public class CalibrarBrazos : MonoBehaviour
{
    [Header("Referencias")]
    public ArmSwingLocomotion armSwing;
    public Transform head;

    [Header("Medida")]
    [Tooltip("Distancia minima (m) para considerar que un brazo esta estirado.")]
    public float distanciaMinima = 0.30f;

    [Tooltip("Distancia maxima (m) para considerar que un brazo esta estirado.")]
    public float distanciaMaxima = 0.95f;

    [Header("Altura de la cabeza")]
    [Tooltip("El umbral de 'brazo estirado' se ajusta a la estatura de la persona, que se mide con la altura de la cabeza (ojos) sobre el suelo.")]
    public bool usarAlturaCabeza = true;

    [Tooltip("Altura de cabeza (m) que se considera baja (el umbral de estirado es mas corto).")]
    public float alturaCabezaBaja = 1.45f;

    [Tooltip("Altura de cabeza (m) que se considera alta (el umbral de estirado es mas largo).")]
    public float alturaCabezaAlta = 1.95f;

    [Tooltip("Parte de la longitud del brazo que se considera 'estirado' en una persona baja.")]
    [Range(0.5f, 0.95f)] public float factorEstiradoBajo = 0.72f;

    [Tooltip("Parte de la longitud del brazo que se considera 'estirado' en una persona alta.")]
    [Range(0.5f, 0.95f)] public float factorEstiradoAlto = 0.84f;

    [Tooltip("Medir solo con las manos (no con los mandos).")]
    public bool usarSoloManos = true;

    [Header("Estado")]
    [SerializeField] float longitudMedida = 0f;
    [SerializeField] bool tieneMedida;
    [SerializeField] float alturaCabezaMedida;

    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
    XRHandSubsystem handSub;

    void Awake()
    {
        if (head == null)
        {
            var cam = Camera.main;
            if (cam != null) head = cam.transform;
        }
    }

    void Update()
    {
        // Si aun no hay medida, se toma automaticamente la primera vez que las manos
        // estan estiradas (asi el juego funciona solo sin pasar por el menu).
        if (!tieneMedida) IntentarMedirAutomatico();
    }

    /// <summary>
    /// Mide la distancia cabeza-mano y la guarda. Devuelve true si se pudo medir.
    /// </summary>
    public bool Calibrar()
    {
        if (head == null)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            head = cam.transform;
        }

        FindSubsystem();

        float mejor = 0f;
        bool alguno = false;

        if (!usarSoloManos && handSub == null)
        {
            // Sin manos: se usan los mandos.
            if (armSwing != null)
            {
                float d = DistanciaAlMando(armSwing.leftController);
                if (d > mejor) mejor = d;
                alguno = alguno || d > 0f;

                d = DistanciaAlMando(armSwing.rightController);
                if (d > mejor) mejor = d;
                alguno = alguno || d > 0f;
            }
        }
        else if (handSub != null)
        {
            if (handSub.leftHand.isTracked)
            {
                float d = DistanciaAPalma(handSub.leftHand);
                if (d > mejor) mejor = d;
                alguno = alguno || d > 0f;
            }
            if (handSub.rightHand.isTracked)
            {
                float d = DistanciaAPalma(handSub.rightHand);
                if (d > mejor) mejor = d;
                alguno = alguno || d > 0f;
            }
        }

        if (!alguno || mejor <= 0.01f) return false;

        longitudMedida = mejor;
        tieneMedida = true;

        // Ademas de la longitud del brazo, se guarda la ALTURA DE LA CABEZA: con
        // los dos datos se sabe el tamano de la persona y el umbral de "brazo
        // estirado" se puede ajustar a su estatura.
        alturaCabezaMedida = MedirAlturaCabeza();
        PlayerPrefs.SetFloat("headHeight", alturaCabezaMedida);

        PlayerPrefs.SetFloat("armLength", longitudMedida);
        PlayerPrefs.Save();

        Aplicar();
        return true;
    }

    void IntentarMedirAutomatico()
    {
        // Solo se mide solo si el jugador ya ha estirado los brazos de forma clara
        // (por encima del minimo), para no pillar una medida falsa.
        FindSubsystem();
        if (head == null) return;

        float d = 0f;
        if (handSub != null)
        {
            if (handSub.leftHand.isTracked) d = Mathf.Max(d, DistanciaAPalma(handSub.leftHand));
            if (handSub.rightHand.isTracked) d = Mathf.Max(d, DistanciaAPalma(handSub.rightHand));
        }
        else if (armSwing != null)
        {
            d = Mathf.Max(d, DistanciaAlMando(armSwing.leftController));
            d = Mathf.Max(d, DistanciaAlMando(armSwing.rightController));
        }

        if (d < distanciaMinima) return;

        longitudMedida = d;
        tieneMedida = true;
        alturaCabezaMedida = MedirAlturaCabeza();
        PlayerPrefs.SetFloat("headHeight", alturaCabezaMedida);
        PlayerPrefs.SetFloat("armLength", longitudMedida);
        PlayerPrefs.Save();
        Aplicar();
    }

    /// <summary>
    /// Mide la altura de la cabeza (los ojos) sobre el suelo, en metros.
    ///
    /// Se mide respecto al XR Origin (que es el suelo), no respecto al origen de
    /// Unity, para que la cuenta sea correcta aunque la escena este inclinada o
    /// el jugador este en otro punto.
    /// </summary>
    float MedirAlturaCabeza()
    {
        if (head == null) return AlturaCabezaPorDefecto;

        var origin = head.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
            return Mathf.Max(0.5f, head.position.y - origin.transform.position.y);

        return Mathf.Max(0.5f, head.position.y);
    }

    void Aplicar()
    {
        if (armSwing == null) return;

        // El umbral de "brazo estirado" se calcula a partir de la medida real:
        // entre el 70% y el 95% de la longitud del brazo. Y se ajusta a la
        // estatura (altura de la cabeza): una persona alta tiene los brazos mas
        // largos, asi que su brazo se estira mas y el umbral tiene que subir.
        float factor = 0.78f;

        if (usarAlturaCabeza && alturaCabezaMedida > 0.1f)
        {
            float estatura = Mathf.InverseLerp(alturaCabezaBaja, alturaCabezaAlta, alturaCabezaMedida);
            factor = Mathf.Lerp(factorEstiradoBajo, factorEstiradoAlto, estatura);
        }

        float umbral = longitudMedida * factor;
        umbral = Mathf.Clamp(umbral, distanciaMinima, distanciaMaxima);
        armSwing.extendedDistance = umbral;
    }

    /// <summary>
    /// Carga la medida guardada (al entrar en la escena de juego) y la aplica.
    /// </summary>
    public void CargarMedidaGuardada()
    {
        // La altura de la cabeza se guarda aparte y tambien se carga: aunque no se
        // haya calibrado el brazo, sirve para colocar los menus a la altura del
        // jugador.
        if (PlayerPrefs.HasKey("headHeight"))
            alturaCabezaMedida = PlayerPrefs.GetFloat("headHeight", AlturaCabezaPorDefecto);

        if (!PlayerPrefs.HasKey("armLength")) return;

        longitudMedida = PlayerPrefs.GetFloat("armLength", 0f);
        if (longitudMedida > 0.01f)
        {
            tieneMedida = true;
            Aplicar();
        }
    }

    /// <summary>
    /// Mide la longitud de los brazos y la guarda. Funciona aunque este componente no
    /// este en la escena (por ejemplo, desde el menu principal): si no encuentra nada
    ///ashing con el que medir, mide igualmente con las manos de XR Hands.
    ///
    /// Se puede llamar desde cualquier sitio.
    /// </summary>
    public static bool CalibrarEstatico()
    {
        var cal = Object.FindFirstObjectByType<CalibrarBrazos>();
        if (cal != null) return cal.Calibrar();

        // No hay componente en la escena: se mide igualmente y se guarda.
        var cal2 = CrearTemporal();
        if (cal2 == null) return false;
        bool ok = cal2.Calibrar();
        Object.DestroyImmediate(cal2.gameObject);
        return ok;
    }

    static CalibrarBrazos CrearTemporal()
    {
        var go = new GameObject("[CalibrarBrazos temporal]");
        return go.AddComponent<CalibrarBrazos>();
    }

    public bool tieneCalibracion => tieneMedida;
    public float longitud => longitudMedida;

    /// <summary>
    /// Altura de la cabeza (ojos) sobre el suelo, en metros. Se mide al calibrar y
    /// se guarda, asi que sirve para saber el tamano de la persona.
    /// </summary>
    public float alturaCabeza => alturaCabezaMedida;

    /// <summary>
    /// Altura de cabeza que se supone si no se ha calibrado nunca.
    /// </summary>
    public const float AlturaCabezaPorDefecto = 1.70f;

    float DistanciaAPalma(XRHand mano)
    {
        if (mano == null || !mano.isTracked) return 0f;
        if (!mano.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose p)) return 0f;
        return Vector3.Distance(head.position, p.position);
    }

    float DistanciaAlMando(Transform mando)
    {
        if (mando == null || !mando.gameObject.activeInHierarchy) return 0f;
        return Vector3.Distance(head.position, mando.position);
    }

    void FindSubsystem()
    {
        if (handSub != null && handSub.running) return;
        handSub = null;
        SubsystemManager.GetSubsystems(subs);
        foreach (var s in subs)
        {
            if (s.running) { handSub = s; break; }
        }
    }
}
