using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOLSAS al final de la faja (zona de embolsado, al lado del escaner).
/// - Hay un porta-bolsas con un taco de bolsas dobladas y una bolsa abierta.
/// - Cuando el cliente se va (sin carrito), sus productos saltan a la bolsa,
///   la bolsa se cierra y el cliente se la lleva en la mano.
/// Todo hecho con primitivas (pocos triangulos, va bien en Quest).
/// </summary>
public class Bolsas : MonoBehaviour
{
    public static Bolsas I { get; private set; }
    static readonly Vector3 PuntoLocal = new Vector3(0.66f, 0f, -0.40f);
    Transform abierta;
    Material plastico, asa;
    float h;

    public static void Instalar(Transform raizMostrador, float alturaMesa)
    {
        if (I != null) return;
        var go = new GameObject("Bolsas (embolsado)");
        go.transform.SetParent(raizMostrador, false);
        go.transform.localPosition = PuntoLocal + Vector3.up * alturaMesa;
        I = go.AddComponent<Bolsas>();
        I.h = alturaMesa;
        I.Construir();
    }

    void OnDestroy() { if (I == this) I = null; }

    void Construir()
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        plastico = new Material(sh) { color = new Color(0.96f, 0.95f, 0.92f) };
        plastico.SetFloat("_Smoothness", 0.55f);
        asa = new Material(sh) { color = new Color(0.95f, 0.47f, 0.13f) };   // naranja de la tienda

        // Soporte metalico
        var acero = new Material(sh) { color = new Color(0.55f, 0.57f, 0.6f) };
        acero.SetFloat("_Metallic", 0.8f);
        Cubo(transform, "Base", new Vector3(0, 0.005f, 0), new Vector3(0.30f, 0.01f, 0.22f), acero);
        Cubo(transform, "Poste", new Vector3(0.13f, 0.17f, 0), new Vector3(0.015f, 0.33f, 0.015f), acero);
        Cubo(transform, "Brazo", new Vector3(0.03f, 0.33f, 0), new Vector3(0.20f, 0.012f, 0.012f), acero);

        // Taco de bolsas dobladas
        for (int i = 0; i < 6; i++)
            Cubo(transform, "Doblada", new Vector3(0.07f, 0.013f + i * 0.006f, 0.06f), new Vector3(0.12f, 0.005f, 0.09f), plastico);

        abierta = NuevaBolsa(transform, new Vector3(-0.05f, 0.01f, -0.02f)).transform;
    }

    GameObject NuevaBolsa(Transform padre, Vector3 pos)
    {
        var b = new GameObject("Bolsa");
        b.transform.SetParent(padre, false);
        b.transform.localPosition = pos;
        // cuerpo (abierto arriba): 4 paredes + fondo
        float w = 0.20f, d = 0.12f, alto = 0.24f, e = 0.004f;
        Cubo(b.transform, "Fondo", new Vector3(0, e / 2, 0), new Vector3(w, e, d), plastico);
        Cubo(b.transform, "Pared", new Vector3(0, alto / 2, d / 2), new Vector3(w, alto, e), plastico);
        Cubo(b.transform, "Pared", new Vector3(0, alto / 2, -d / 2), new Vector3(w, alto, e), plastico);
        Cubo(b.transform, "Pared", new Vector3(w / 2, alto / 2, 0), new Vector3(e, alto, d), plastico);
        Cubo(b.transform, "Pared", new Vector3(-w / 2, alto / 2, 0), new Vector3(e, alto, d), plastico);
        Cubo(b.transform, "Franja", new Vector3(0, alto * 0.55f, d / 2 + e), new Vector3(w * 0.7f, 0.04f, 0.002f), asa);
        // asas
        foreach (float z in new[] { d / 2, -d / 2 })
        {
            Cubo(b.transform, "Asa", new Vector3(-0.04f, alto + 0.04f, z), new Vector3(0.008f, 0.08f, 0.006f), asa);
            Cubo(b.transform, "Asa", new Vector3(0.04f, alto + 0.04f, z), new Vector3(0.008f, 0.08f, 0.006f), asa);
            Cubo(b.transform, "Asa", new Vector3(0, alto + 0.08f, z), new Vector3(0.088f, 0.008f, 0.006f), asa);
        }
        return b;
    }

    static void Cubo(Transform p, string n, Vector3 pos, Vector3 tam, Material m)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = n;
        Object.Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(p, false);
        c.transform.localPosition = pos;
        c.transform.localScale = tam;
        var r = c.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Embolsa los productos del cliente y le da la bolsa. Se queda con la lista.</summary>
    public void Empacar(Transform cliente, List<GameObject> productos, float alturaCliente)
    {
        var lista = new List<GameObject>(productos);
        productos.Clear();
        StartCoroutine(EmpacarRutina(cliente, lista, alturaCliente));
    }

    IEnumerator EmpacarRutina(Transform cliente, List<GameObject> lista, float altura)
    {
        Vector3 boca = abierta.position + Vector3.up * 0.22f;
        foreach (var p in lista)
        {
            if (p == null) continue;
            foreach (var rb in p.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
            foreach (var c in p.GetComponentsInChildren<Collider>()) c.enabled = false;
            Vector3 a = p.transform.position; Vector3 esc = p.transform.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
            {
                if (p == null) break;
                p.transform.position = Vector3.Lerp(a, boca, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.15f;
                p.transform.localScale = esc * Mathf.Lerp(1f, 0.35f, t);
                yield return null;
            }
            if (p != null) Destroy(p);
        }

        // La bolsa llena pasa a la mano del cliente; en el soporte aparece otra vacia.
        var llena = abierta;
        abierta = NuevaBolsa(transform, new Vector3(-0.05f, 0.01f, -0.02f)).transform;
        if (cliente == null) { Destroy(llena.gameObject); yield break; }
        llena.SetParent(null, true);
        Vector3 ini = llena.position;
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)
        {
            if (cliente == null) break;
            llena.position = Vector3.Lerp(ini, Mano(cliente, altura), t);
            yield return null;
        }
        if (cliente == null) { Destroy(llena.gameObject); yield break; }
        llena.SetParent(cliente, true);
        llena.localPosition = cliente.InverseTransformPoint(Mano(cliente, altura));
        llena.localRotation = Quaternion.identity;
    }

    static Vector3 Mano(Transform c, float altura)
    {
        var anim = c.GetComponentInChildren<Animator>();
        var mano = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (mano != null) return mano.position - Vector3.up * 0.30f;
        return c.position + c.right * 0.25f + Vector3.up * altura * 0.40f;
    }
}
