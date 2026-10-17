using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Locomocion por balanceo de brazos (estilo Creed) para OpenXR.
/// Con manos usa la palma (XR Hands); con mandos usa el transform del control.
/// Poner en el XR Origin. Asignar Head, Left Controller y Right Controller.
/// </summary>
public class ArmSwingLocomotion : MonoBehaviour
{
    [Header("Referencias")]
    public Transform head;             // Main Camera
    public Transform leftController;   // Left Controller
    public Transform rightController;  // Right Controller

    [Header("Ajustes")]
    public float speedMultiplier = 1.5f;   // Que tan rapido avanzas
    public float minHandSpeed = 0.3f;      // Ignora movimientos pequenos (m/s)
    public float maxSpeed = 3f;            // Velocidad maxima (m/s)
    public float ignoreSpikesAbove = 10f;  // Ignora saltos al cambiar mando/mano
    public bool requireBothArms = false;   // true: solo avanza si mueves ambos brazos

    class Side
    {
        public Vector3 lastPos;
        public int lastSource = -1; // 0 nada, 1 mano, 2 control
    }

    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
    XRHandSubsystem handSub;
    readonly Side left = new Side();
    readonly Side right = new Side();
    Vector3 lastHead;
    bool hasLastHead;

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

    // Devuelve la fuente usada: 0 nada, 1 mano, 2 control. pos queda en espacio local del XR Origin.
    int GetPos(bool useHand, XRHand hand, Transform controller, out Vector3 pos)
    {
        pos = default;
        if (useHand && hand.isTracked)
        {
            if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose p))
            {
                pos = p.position;
                return 1;
            }
        }
        if (controller && controller.gameObject.activeInHierarchy)
        {
            pos = transform.InverseTransformPoint(controller.position);
            return 2;
        }
        return 0;
    }

    float SideSpeed(Side s, bool useHand, XRHand hand, Transform controller, Vector3 headDelta)
    {
        int src = GetPos(useHand, hand, controller, out Vector3 pos);
        if (src == 0) { s.lastSource = 0; return 0f; }
        if (src != s.lastSource)
        {
            s.lastSource = src;
            s.lastPos = pos;
            return 0f;
        }
        // Se resta el movimiento de la cabeza para no contar el caminar real
        Vector3 d = (pos - s.lastPos) - headDelta;
        s.lastPos = pos;
        float speed = d.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        return speed > ignoreSpikesAbove ? 0f : speed;
    }

    void Update()
    {
        if (!head) return;

        FindSubsystem();
        bool useHand = handSub != null;

        Vector3 headLocal = transform.InverseTransformPoint(head.position);
        Vector3 headDelta = hasLastHead ? headLocal - lastHead : Vector3.zero;
        lastHead = headLocal;
        hasLastHead = true;

        XRHand lh = useHand ? handSub.leftHand : default;
        XRHand rh = useHand ? handSub.rightHand : default;

        float l = SideSpeed(left, useHand, lh, leftController, headDelta);
        float r = SideSpeed(right, useHand, rh, rightController, headDelta);

        float handSpeed = requireBothArms ? Mathf.Min(l, r) : (l + r) * 0.5f;
        if (handSpeed < minHandSpeed) return;

        Vector3 dir = head.forward;
        dir.y = 0f;
        dir.Normalize();

        float speed = Mathf.Min(handSpeed * speedMultiplier, maxSpeed);
        transform.position += dir * speed * Time.deltaTime;
    }
}
