using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Anima el cierre de los dedos de la mano segun el input y, cuando hay un objeto
/// agarrado, adapta el cierre al tamano de ese objeto para que los dedos lo rodeen
/// sin atravesarlo.
///
/// POR QUE HABIA QUE ARREGLARLO
/// ----------------------------
/// Antes todas las articulaciones de un dedo se doblaban el mismo angulo
/// (proximal + intermedia + distal = 3 x 80 grados = 240 de enrollamiento). El dedo
/// distal se metia en la palma y el objeto lo atravesaba. Ahora el angulo se reparte
/// entre las articulaciones (mas suave en la base, mas fuerza en la punta) de modo que
/// el total del dedo sea el angulo configurado.
///
/// Ademas el cierre ya no es fijo: si el objeto agarrado tiene un GrabPose se usa ese,
/// y si no se calcula a partir del tamano real del collider, para que un producto
/// pequeño se agarre con un pellizco y uno grande se envuelva con la mano entera.
///
/// Ejes: cada articulacion dobla sobre su eje X local, que es el eje de flexion
/// del rig de XR Hands. Los ejes son globales de la articulacion (no del rig), asi que
/// el mismo valor sirve para las dos manos.
///
/// "mano" = los mandos: el cierre se dispara con Grip (dedos) y Trigger (indice).
/// </summary>
public class HandFingerCurl : MonoBehaviour
{
    [Header("Botones")]
    public InputActionProperty gripAction;
    public InputActionProperty triggerAction;

    [Header("Agarre")]
    public NearFarInteractor interactor;

    [Header("Ejes y angulos")]
    public Vector3 fingerAxis = new Vector3(1, 0, 0);
    public Vector3 thumbAxis = new Vector3(1, 0, 0);
    public float fingerMaxAngle = 80f;
    public float thumbMaxAngle = 35f;
    public float speed = 15f;

    [Header("Reparto del cierre entre articulaciones (proximo, intermedio, distal)")]
    [Range(0f, 1f)] public float fingerProximalWeight = 0.5f;
    [Range(0f, 1f)] public float fingerIntermediateWeight = 0.3f;
    [Range(0f, 1f)] public float fingerDistalWeight = 0.2f;
    [Range(0f, 1f)] public float thumbProximalWeight = 0.6f;
    [Range(0f, 1f)] public float thumbIntermediateWeight = 0.25f;
    [Range(0f, 1f)] public float thumbDistalWeight = 0.15f;

    [Header("Cierre automatico segun tamano del objeto (sin GrabPose)")]
    public bool autoCurlBySize = true;
    public float autoCurlMinWidth = 0.02f;
    public float autoCurlMaxWidth = 0.12f;
    public float autoCurlMinAmount = 0.3f;
    public float autoCurlMaxAmount = 0.95f;

    [Header("Prueba manual (sin control)")]
    public bool useTest = false;
    [Range(0, 1)] public float testGrip = 0f;
    [Range(0, 1)] public float testTrigger = 0f;

    // Articulaciones con su peso de cierre.
    struct Joint
    {
        public Transform t;
        public Quaternion start;
        public float weight;
    }

    Joint[] gripJoints = new Joint[0];
    Joint[] thumbJoints = new Joint[0];
    Joint[] indexJoints = new Joint[0];
    float grip, trigger;

    void OnEnable()
    {
        if (gripAction.action != null) gripAction.action.Enable();
        if (triggerAction.action != null) triggerAction.action.Enable();
    }

    void Start()
    {
        var grip = new System.Collections.Generic.List<Joint>();
        var thumb = new System.Collections.Generic.List<Joint>();
        var index = new System.Collections.Generic.List<Joint>();

        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            string n = tr.name;

            // Las puntas no se doblan (son el punto de contacto) ni los metacarpianos
            // (son la base de la mano, no forman parte del gesto de pinza).
            if (n.Contains("Tip") || n.Contains("Metacarpal")) continue;

            bool esPulgar = n.Contains("Thumb");
            bool esIndice = n.Contains("Index");
            bool esMedio = n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little");
            if (!esPulgar && !esIndice && !esMedio) continue;

            float weight;
            if (n.Contains("Proximal")) weight = esPulgar ? thumbProximalWeight : fingerProximalWeight;
            else if (n.Contains("Intermediate")) weight = esPulgar ? thumbIntermediateWeight : fingerIntermediateWeight;
            else if (n.Contains("Distal")) weight = esPulgar ? thumbDistalWeight : fingerDistalWeight;
            else weight = 1f / 3f; // articulation sin sufijo conocido: reparto plano

            var j = new Joint { t = tr, start = tr.localRotation, weight = weight };

            if (esPulgar) thumb.Add(j);
            else if (esIndice) index.Add(j);
            else grip.Add(j);
        }

        gripJoints = grip.ToArray();
        thumbJoints = thumb.ToArray();
        indexJoints = index.ToArray();
    }

    void LateUpdate()
    {
        float g = useTest ? testGrip : (gripAction.action != null ? gripAction.action.ReadValue<float>() : 0f);
        float t = useTest ? testTrigger : (triggerAction.action != null ? triggerAction.action.ReadValue<float>() : 0f);

        // Si hay un objeto agarrado, el cierre lo manda el objeto (GrabPose) o su tamano.
        if (interactor != null && interactor.interactablesSelected.Count > 0)
        {
            var obj = interactor.interactablesSelected[0].transform;
            var pose = obj.GetComponentInParent<GrabPose>();

            if (pose != null)
            {
                g = pose.fingers;
                t = pose.index;
            }
            else if (autoCurlBySize)
            {
                CalcularCurlPorTamano(obj, out g, out t);
            }
        }

        grip = Mathf.Lerp(grip, g, Time.deltaTime * speed);
        trigger = Mathf.Lerp(trigger, t, Time.deltaTime * speed);

        Apply(gripJoints, grip, fingerMaxAngle, fingerAxis);
        Apply(thumbJoints, grip, thumbMaxAngle, thumbAxis);
        Apply(indexJoints, trigger, fingerMaxAngle, fingerAxis);
    }

    void Apply(Joint[] joints, float value, float maxAngle, Vector3 axis)
    {
        for (int k = 0; k < joints.Length; k++)
        {
            if (joints[k].t == null) continue;
            joints[k].t.localRotation = joints[k].start *
                Quaternion.AngleAxis(maxAngle * value * joints[k].weight, axis.normalized);
        }
    }

    /// <summary>
    /// Deduce el cierre de dedos a partir del tamano del objeto agarrado.
    /// Un objeto estrecho se agarra con un pellizco (indice y pulgar juntos) y uno
    /// ancho se envuelve con la mano, de modo que los dedos cierran justo sobre la
    /// superficie en vez de atravesarla.
    /// </summary>
    void CalcularCurlPorTamano(Transform obj, out float fingers, out float index)
    {
        fingers = 0.6f;
        index = 0.6f;

        var col = obj.GetComponentInChildren<Collider>();
        if (col == null) return;

        Vector3 s = col.bounds.size;
        // El ancho que se agarra con la mano es la menor dimension horizontal.
        float ancho = Mathf.Min(s.x, s.z);
        float k = Mathf.InverseLerp(autoCurlMinWidth, autoCurlMaxWidth, ancho);

        fingers = Mathf.Lerp(autoCurlMinAmount, autoCurlMaxAmount, k);
        // El indice acompaña al resto pero cierra algo menos (queda el pulgar enfrente).
        index = Mathf.Lerp(autoCurlMinAmount * 0.85f, autoCurlMaxAmount * 0.9f, k);
    }
}
