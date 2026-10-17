using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Caminar moviendo los brazos, SOLO con manos (hand tracking) y con los brazos estirados.
/// Con controles NO camina: ahi se usa el joystick.
/// Poner en el XR Origin. Asignar Head, Left Controller y Right Controller.
/// </summary>
public class ArmSwingLocomotion : MonoBehaviour
{
    [Header("Referencias")]
    public Transform head;             // Main Camera
    public Transform leftController;   // Left Controller
    public Transform rightController;  // Right Controller

    [Header("Cuando camina")]
    public bool allowControllerSwing = false;   // false: con controles no camina (usa joystick)
    public float extendedDistance = 0.35f;      // distancia cabeza-palma (m) para contar como "brazo estirado"
    public bool requireBothExtended = true;     // true: ambas manos estiradas
    public bool walkWhileExtended = false;      // true: camina solo con estirar los brazos, sin balancear
    public float extendedWalkSpeed = 1.5f;      // velocidad si walkWhileExtended esta activo

    [Header("Ajustes")]
    public float speedMultiplier = 1.5f;
    public float minHandSpeed = 0.3f;
    public float maxSpeed = 3f;
    public float ignoreSpikesAbove = 10f;

    class Side
    {
        public Vector3 lastPos;
        public int lastSource = -1; // 0 nada, 1 mano, 2 control
        public bool extended;
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

    int GetPos(bool useHand, XRHand hand, Transform controller, out Vector3 localPos, out Vector3 worldPos)
    {
        localPos = default;
        worldPos = default;
        if (useHand && hand.isTracked)
        {
            if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose p))
            {
                localPos = p.position;
                worldPos = transform.TransformPoint(p.position);
                return 1;
            }
        }
        if (controller && controller.gameObject.activeInHierarchy)
        {
            worldPos = controller.position;
            localPos = transform.InverseTransformPoint(worldPos);
            return 2;
        }
        return 0;
    }

    float SideSpeed(Side s, bool useHand, XRHand hand, Transform controller, Vector3 headDelta)
    {
        int src = GetPos(useHand, hand, controller, out Vector3 pos, out Vector3 world);

        if (src == 0) { s.lastSource = 0; s.extended = false; return 0f; }

        if (src == 2 && !allowControllerSwing)
        {
            s.lastSource = 2;
            s.extended = false; // con controles no se camina por brazos
            return 0f;
        }

        s.extended = src == 2 || Vector3.Distance(head.position, world) >= extendedDistance;

        if (src != s.lastSource)
        {
            s.lastSource = src;
            s.lastPos = pos;
            return 0f;
        }

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

        bool extendedOk = requireBothExtended
            ? (left.extended && right.extended)
            : (left.extended || right.extended);
        if (!extendedOk) return;

        float speed;
        if (walkWhileExtended)
        {
            speed = extendedWalkSpeed;
        }
        else
        {
            float handSpeed = (l + r) * 0.5f;
            if (handSpeed < minHandSpeed) return;
            speed = Mathf.Min(handSpeed * speedMultiplier, maxSpeed);
        }

        Vector3 dir = head.forward;
        dir.y = 0f;
        dir.Normalize();

        transform.position += dir * speed * Time.deltaTime;
    }
}
