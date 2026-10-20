using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class HandFingerCurl : MonoBehaviour
{
    [Header("Botones")]
    public InputActionProperty gripAction;
    public InputActionProperty triggerAction;

    [Header("Agarre")]
    public NearFarInteractor interactor;

    [Header("Ejes y ángulos")]
    public Vector3 fingerAxis = new Vector3(1, 0, 0);
    public Vector3 thumbAxis = new Vector3(1, 0, 0);
    public float fingerMaxAngle = 80f;
    public float thumbMaxAngle = 35f;
    public float speed = 15f;

    [Header("Prueba manual (sin control)")]
    public bool useTest = false;
    [Range(0, 1)] public float testGrip = 0f;
    [Range(0, 1)] public float testTrigger = 0f;

    Transform[] gripJoints, thumbJoints, indexJoints;
    Quaternion[] gripRot, thumbRot, indexRot;
    float grip, trigger;

    void OnEnable()
    {
        if (gripAction.action != null) gripAction.action.Enable();
        if (triggerAction.action != null) triggerAction.action.Enable();
    }

    void Start()
    {
        var g = new System.Collections.Generic.List<Transform>();
        var t = new System.Collections.Generic.List<Transform>();
        var i = new System.Collections.Generic.List<Transform>();
        foreach (var tr in GetComponentsInChildren<Transform>())
        {
            string n = tr.name;
            if (n.Contains("Tip") || n.Contains("Metacarpal")) continue;
            if (n.Contains("Index")) i.Add(tr);
            else if (n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little")) g.Add(tr);
            else if (n.Contains("Thumb")) t.Add(tr);
        }
        gripJoints = g.ToArray(); thumbJoints = t.ToArray(); indexJoints = i.ToArray();
        gripRot = Save(gripJoints); thumbRot = Save(thumbJoints); indexRot = Save(indexJoints);
    }

    Quaternion[] Save(Transform[] a)
    {
        var r = new Quaternion[a.Length];
        for (int k = 0; k < a.Length; k++) r[k] = a[k].localRotation;
        return r;
    }

    void Apply(Transform[] j, Quaternion[] start, float v, float max, Vector3 axis)
    {
        for (int k = 0; k < j.Length; k++)
            j[k].localRotation = start[k] * Quaternion.AngleAxis(max * v, axis.normalized);
    }

    void LateUpdate()
    {
        float g = useTest ? testGrip : (gripAction.action != null ? gripAction.action.ReadValue<float>() : 0f);
        float t = useTest ? testTrigger : (triggerAction.action != null ? triggerAction.action.ReadValue<float>() : 0f);

        // Si hay un objeto agarrado con GrabPose, usa ese cierre
        if (interactor != null && interactor.interactablesSelected.Count > 0)
        {
            var obj = interactor.interactablesSelected[0].transform;
            var pose = obj.GetComponentInParent<GrabPose>();
            if (pose != null) { g = pose.fingers; t = pose.index; }
        }

        grip = Mathf.Lerp(grip, g, Time.deltaTime * speed);
        trigger = Mathf.Lerp(trigger, t, Time.deltaTime * speed);
        Apply(gripJoints, gripRot, grip, fingerMaxAngle, fingerAxis);
        Apply(thumbJoints, thumbRot, grip, thumbMaxAngle, thumbAxis);
        Apply(indexJoints, indexRot, trigger, fingerMaxAngle, fingerAxis);
    }
}