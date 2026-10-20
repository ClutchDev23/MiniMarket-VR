using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class GrabPointSwitcher : MonoBehaviour
{
    public Transform leftGrabPoint;
    public Transform rightGrabPoint;

    XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.hoverEntered.AddListener(OnHover);
        grab.selectEntered.AddListener(OnSelect);
    }

    void OnDestroy()
    {
        if (grab == null) return;
        grab.hoverEntered.RemoveListener(OnHover);
        grab.selectEntered.RemoveListener(OnSelect);
    }

    void OnHover(HoverEnterEventArgs args) { SetPoint(args.interactorObject.transform); }
    void OnSelect(SelectEnterEventArgs args) { SetPoint(args.interactorObject.transform); }

    void SetPoint(Transform interactor)
    {
        bool isLeft = false;
        Transform t = interactor;
        while (t != null)
        {
            if (t.name.Contains("Left")) { isLeft = true; break; }
            if (t.name.Contains("Right")) break;
            t = t.parent;
        }
        Transform point = isLeft ? leftGrabPoint : rightGrabPoint;
        if (point != null) grab.attachTransform = point;
    }
}