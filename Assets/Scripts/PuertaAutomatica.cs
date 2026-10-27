using UnityEngine;

public class PuertaAutomatica : MonoBehaviour
{
    /// <summary>Otros que tambien abren la puerta (los clientes se registran aqui).</summary>
    public static readonly System.Collections.Generic.List<Transform> Extras = new System.Collections.Generic.List<Transform>();

    public Transform panelIzq;
    public Transform panelDer;

    public float radio = 2.6f;
    public float velocidad = 1.4f;
    public float apertura = 1.5f;

    Vector3 izqCerrada, derCerrada, izqAbierta, derAbierta;
    Transform jugador;
    Transform[] carritos;
    bool iniciado;

    void Start()
    {
        if (panelIzq != null)
        {
            izqCerrada = panelIzq.localPosition;
            izqAbierta = izqCerrada + Vector3.left * apertura;
        }
        if (panelDer != null)
        {
            derCerrada = panelDer.localPosition;
            derAbierta = derCerrada + Vector3.right * apertura;
        }

        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (cam.CompareTag("MainCamera")) { jugador = cam.transform; break; }
        if (jugador == null)
        {
            var cs = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (cs.Length > 0) jugador = cs[0].transform;
        }

        var todos = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int n = 0;
        foreach (var t in todos)
            if (t.name.StartsWith("Pref_Trolley01_Sm")) n++;
        carritos = new Transform[n];
        int i = 0;
        foreach (var t in todos)
            if (t.name.StartsWith("Pref_Trolley01_Sm") && i < n) carritos[i++] = t;

        iniciado = true;
        Debug.Log("[Puerta] activa, radio=" + radio + " carritos=" + n + " jugador=" + (jugador != null));
    }

    void Update()
    {
        if (!iniciado) return;

        bool cerca = Cerca(jugador);
        if (!cerca && carritos != null)
            foreach (var c in carritos)
                if (Cerca(c)) { cerca = true; break; }
        if (!cerca)
            foreach (var e in Extras)
                if (Cerca(e)) { cerca = true; break; }

        if (panelIzq != null)
            panelIzq.localPosition = Vector3.MoveTowards(
                panelIzq.localPosition, cerca ? izqAbierta : izqCerrada, velocidad * Time.deltaTime);
        if (panelDer != null)
            panelDer.localPosition = Vector3.MoveTowards(
                panelDer.localPosition, cerca ? derAbierta : derCerrada, velocidad * Time.deltaTime);
    }

    bool Cerca(Transform t)
    {
        if (t == null) return false;
        var a = t.position;
        var b = transform.position;
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz <= radio * radio;
    }
}
