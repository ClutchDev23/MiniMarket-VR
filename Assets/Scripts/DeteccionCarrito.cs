using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DeteccionCarrito : MonoBehaviour
{
    public AudioSource audioAviso;
    public AudioClip clipAviso;

    readonly HashSet<XRGrabInteractable> dentro = new HashSet<XRGrabInteractable>();

    public int Cantidad
    {
        get
        {
            int n = 0;
            foreach (var g in dentro)
                if (g != null && !g.isSelected)
                    n++;
            return n;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;

        var grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null) return;
        if (grab.GetComponent<Rigidbody>() == null) return;

        // Ignorar el propio carrito y sus hijos.
        if (transform.IsChildOf(grab.transform) || grab.transform.IsChildOf(transform.parent))
            return;

        if (!dentro.Add(grab)) return;

        Debug.Log($"[Carrito] '{NombreCarrito()}' producto dentro: {grab.name} (total={Cantidad})");
        Avisar();
    }

    void OnTriggerExit(Collider other)
    {
        var grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null) return;

        if (dentro.Remove(grab))
            Debug.Log($"[Carrito] '{NombreCarrito()}' producto fuera: {grab.name} (total={Cantidad})");
    }

    void Avisar()
    {
        if (audioAviso != null && clipAviso != null)
            audioAviso.PlayOneShot(clipAviso);
    }

    string NombreCarrito()
    {
        return transform.parent != null ? transform.parent.name : name;
    }
}
