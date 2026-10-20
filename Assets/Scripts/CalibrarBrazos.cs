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

    [Tooltip("Medir solo con las manos (no con los mandos).")]
    public bool usarSoloManos = true;

    [Header("Estado")]
    [SerializeField] float longitudMedida = 0f;
    [SerializeField] bool tieneMedida;

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
        PlayerPrefs.SetFloat("armLength", longitudMedida);
        PlayerPrefs.Save();
        Aplicar();
    }

    void Aplicar()
    {
        if (armSwing == null) return;

        // El umbral de "brazo estirado" se calcula a partir de la medida real:
        // entre el 70% y el 95% de la longitud del brazo.
        float umbral = longitudMedida * 0.78f;
        umbral = Mathf.Clamp(umbral, distanciaMinima, distanciaMaxima);
        armSwing.extendedDistance = umbral;
    }

    /// <summary>
    /// Carga la medida guardada (al entrar en la escena de juego) y la aplica.
    /// </summary>
    public void CargarMedidaGuardada()
    {
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
