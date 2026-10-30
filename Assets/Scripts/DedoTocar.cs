using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

/// <summary>
/// TOCAR PANTALLAS CON EL DEDO (manos de VR).
///
/// Mantienes el GATILLO (R2) sin el grip y la punta del dedo indice de tu mano de VR
/// aprieta el boton que toque en cualquier panel del mundo (la pantalla de la caja,
/// el menu de pausa...). Vibra al apretar. Se instala solo en las escenas del minimarket.
/// </summary>
public class DedoTocar : MonoBehaviour
{
    /// <summary>Cuantas veces se toco un boton (lo usa el tutorial).</summary>
    public static int Toques { get; private set; }

    readonly List<Button> botones = new List<Button>();
    readonly Dictionary<XRHandTrackingEvents, Transform> puntas = new Dictionary<XRHandTrackingEvents, Transform>();
    float buscarEn, proximoToque;
    readonly Vector3[] esquinas = new Vector3[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (esc, m) => Asegurar(esc);
        Asegurar(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void Asegurar(UnityEngine.SceneManagement.Scene esc)
    {
        if (!esc.IsValid() || !esc.name.StartsWith("MinimarketXR")) return;
        if (FindAnyObjectByType<DedoTocar>() != null) return;
        var go = new GameObject("Dedo para tocar pantallas");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, esc);
        go.AddComponent<DedoTocar>();
    }

    static float Eje(bool izq, string nombre)
    {
        foreach (var dev in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (!(dev is UnityEngine.InputSystem.XR.XRController)) continue;
            bool es = false;
            foreach (var u in dev.usages) if (u == (izq ? UnityEngine.InputSystem.CommonUsages.LeftHand : UnityEngine.InputSystem.CommonUsages.RightHand)) es = true;
            if (es && dev.TryGetChildControl(nombre) is UnityEngine.InputSystem.Controls.AxisControl a) return a.ReadValue();
        }
        return 0f;
    }

    void Update()
    {
        if (Time.time > buscarEn)
        {
            botones.Clear();
            foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                var cv = b.GetComponentInParent<Canvas>();
                if (cv != null && cv.renderMode == RenderMode.WorldSpace) botones.Add(b);
            }
            buscarEn = Time.time + 1f;
        }
        if (Time.time < proximoToque) return;
        foreach (var e in FindObjectsByType<XRHandTrackingEvents>(FindObjectsSortMode.None))
        {
            if (!e.isActiveAndEnabled || !e.handIsTracked) continue;
            bool izq = e.handedness == Handedness.Left;
            if (Eje(izq, "trigger") < 0.5f || Eje(izq, "grip") > 0.5f) continue;
            if (!puntas.TryGetValue(e, out var punta) || punta == null)
            {
                punta = null;
                foreach (var t in e.GetComponentsInChildren<Transform>(true))
                    if (t.name.Contains("IndexTip")) { punta = t; break; }
                puntas[e] = punta;
            }
            if (punta != null && Tocar(punta.position)) return;
        }
    }

    bool Tocar(Vector3 p)
    {
        foreach (var b in botones)
        {
            if (b == null || !b.isActiveAndEnabled || !b.interactable) continue;
            var rt = (RectTransform)b.transform;
            rt.GetWorldCorners(esquinas);
            if (Mathf.Abs(new Plane(esquinas[0], esquinas[1], esquinas[2]).GetDistanceToPoint(p)) > 0.025f) continue;
            var l = rt.InverseTransformPoint(p);
            if (!rt.rect.Contains(new Vector2(l.x, l.y))) continue;
            b.onClick.Invoke();
            Toques++;
            proximoToque = Time.time + 0.6f;
            Haptica.Ambos(0.35f, 0.05f);
            Debug.Log("[Dedo] Toco: " + b.name);
            return true;
        }
        return false;
    }
}
