using Unity.XR.CoreUtils;
using UnityEngine;

public class ManosXR : MonoBehaviour
{
    public GameObject manoIzquierda;
    public GameObject manoDerecha;

    void Awake()
    {
        XROrigin origin = FindAnyObjectByType<XROrigin>();
        if (origin == null || origin.CameraFloorOffsetObject == null)
        {
            Debug.LogError("[ManosXR] No se encontro XROrigin o CameraFloorOffset");
            return;
        }

        Transform parent = origin.CameraFloorOffsetObject.transform;
        Instanciar(manoIzquierda, parent, "Mano Izquierda");
        Instanciar(manoDerecha, parent, "Mano Derecha");
    }

    void Instanciar(GameObject prefab, Transform parent, string nombre)
    {
        if (prefab == null)
        {
            Debug.LogError("[ManosXR] Prefab sin asignar: " + nombre);
            return;
        }

        GameObject go = Instantiate(prefab, parent);
        go.name = nombre;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
    }
}
