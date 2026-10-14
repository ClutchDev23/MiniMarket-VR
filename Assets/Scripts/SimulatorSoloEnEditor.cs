using UnityEngine;
using UnityEngine.XR;

[DefaultExecutionOrder(-10000)]
public class SimulatorSoloEnEditor : MonoBehaviour
{
    void Awake()
    {
        if (Application.isEditor)
            return;

        if (Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive)
            gameObject.SetActive(false);
    }
}
