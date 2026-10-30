using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// CUERPO DEL JUGADOR (primera persona)
///
/// Al mirar hacia abajo te ves el cuerpo: brazos, torso, piernas y zapatillas.
///   - Ropa: polo blanco, baggy negro deslavado, zapatillas blancas y un reloj en la
///     muneca izquierda. Piel trigueña (tambien las manos de VR).
///   - Los BRAZOS siguen a tus manos/mandos (IK de dos huesos: hombro-codo-muneca),
///     y terminan justo donde empieza la mano de VR.
///   - El CUERPO se pone debajo de tu cabeza y gira cuando giras (o cuando caminas).
///   - Las PIERNAS caminan cuando te mueves y se quedan quietas cuando paras.
///   - La cabeza del personaje no se dibuja (seria verse por dentro).
///
/// Usa el personaje masculino de los clientes (Remy) con otra ropa. Se instala solo en
/// las escenas del minimarket. Se apaga desde la pausa con PlayerPrefs "cuerpo" = 0.
/// </summary>
[DefaultExecutionOrder(300)]   // despues de ManosVerticales (200): las manos ya estan en su sitio
public class AvatarJugador : MonoBehaviour
{
    public static readonly Color Piel = new Color(0.78f, 0.58f, 0.45f);      // trigueño
    static readonly Color PielTinte = new Color(0.86f, 0.68f, 0.56f);        // multiplica la piel de Remy
    static readonly Color Polo = new Color(0.96f, 0.96f, 0.95f);
    static readonly Color Baggy = new Color(0.17f, 0.17f, 0.19f);             // negro deslavado
    static readonly Color Zapatilla = new Color(0.97f, 0.97f, 0.97f);

    public static AvatarJugador I { get; private set; }

    Animator anim;
    Transform raiz, cuello, cabeza;
    Transform hombroI, codoI, manoI, hombroD, codoD, manoD;
    float largoBrazoI, largoAntebrazoI, largoBrazoD, largoAntebrazoD;
    Unity.XR.CoreUtils.XROrigin origen;
    float yaw;
    Vector3 camAnterior;
    float velocidad;
    string estado = "";
    float escala = 1f;
    float calibrarEn;
    bool calibrado;
    float tintarManosEn;
    bool avisoManos;

    // ------------------------------------------------------------------ arranque
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (esc, modo) => Asegurar(esc);
        Asegurar(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void Asegurar(UnityEngine.SceneManagement.Scene esc)
    {
        if (!esc.IsValid() || !esc.name.StartsWith("MinimarketXR")) return;
        if (PlayerPrefs.GetInt("cuerpo", 1) == 0) return;
        if (I != null) return;
        var go = new GameObject("Cuerpo del jugador");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, esc);
        go.AddComponent<AvatarJugador>();
    }

    /// <summary>Muestra u oculta el cuerpo (se recuerda).</summary>
    public static void Mostrar(bool si)
    {
        PlayerPrefs.SetInt("cuerpo", si ? 1 : 0);
        if (!si && I != null) Destroy(I.gameObject);
        if (si && I == null) Asegurar(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    void Start()
    {
        I = this;
        origen = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        var cfg = Resources.Load<ConfigClientes>("ConfigClientes");
        GameObject modelo = null;
        if (cfg != null && cfg.personajes != null)
            foreach (var p in cfg.personajes)
                if (p != null && p.name.ToLowerInvariant().Contains("remy")) modelo = p;
        if (modelo == null || cfg.animador == null)
        {
            Debug.LogWarning("[Cuerpo] No encontre el personaje (Remy) en ConfigClientes.");
            Destroy(gameObject);
            return;
        }

        var go = Instantiate(modelo, transform);
        go.name = "Cuerpo";
        raiz = go.transform;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        anim = go.GetComponentInChildren<Animator>();
        if (anim == null || anim.avatar == null || !anim.avatar.isHuman)
        {
            Debug.LogWarning("[Cuerpo] El personaje no es Humanoid.");
            Destroy(gameObject);
            return;
        }
        anim.runtimeAnimatorController = cfg.animador;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        cuello = anim.GetBoneTransform(HumanBodyBones.Neck);
        cabeza = anim.GetBoneTransform(HumanBodyBones.Head);
        hombroI = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        codoI = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        manoI = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        hombroD = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        codoD = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        manoD = anim.GetBoneTransform(HumanBodyBones.RightHand);

        Vestir(go);
        Holgado();
        Reloj();

        var cam = Camera.main;
        if (cam != null)
        {
            camAnterior = cam.transform.position;
            var f = cam.transform.forward; f.y = 0f;
            yaw = f.sqrMagnitude > 0.01f ? Quaternion.LookRotation(f).eulerAngles.y : 0f;
        }
        calibrarEn = Time.time + 1.2f;
        Debug.Log("[Cuerpo] Listo: " + modelo.name + " con polo blanco, baggy negro y zapatillas blancas.");
    }

    void OnDestroy() { if (I == this) I = null; }

    // ------------------------------------------------------------------ ropa
    // Texturas hechas para el jugador (Resources/Cuerpo): piel trigueña con las piernas
    // de jean negro deslavado (asi el short de Remy queda como un baggy largo), polo
    // blanco con su tejido y el short en el mismo jean.
    void Vestir(GameObject go)
    {
        var tPiel = Resources.Load<Texture2D>("Cuerpo/Cuerpo_Piel");
        var tPolo = Resources.Load<Texture2D>("Cuerpo/Cuerpo_Polo");
        var tBaggy = Resources.Load<Texture2D>("Cuerpo/Cuerpo_Baggy");
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var mats = r.materials;   // copias: los clientes Remy no cambian
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                string n = ((m.mainTexture != null ? m.mainTexture.name : "") + " " + m.name + " " + r.name).ToLowerInvariant();
                if (n.Contains("hair")) continue;
                if (n.Contains("body")) { if (tPiel != null) Textura(m, tPiel); else Tenir(m, PielTinte, false); }
                else if (n.Contains("top")) { if (tPolo != null) Textura(m, tPolo); else Tenir(m, Polo, true); }
                else if (n.Contains("bottom")) { if (tBaggy != null) Textura(m, tBaggy); else Tenir(m, Baggy, true); Brillo(m, 0.12f); }
                else if (n.Contains("shoe")) { Tenir(m, Zapatilla, true); Brillo(m, 0.45f); }
            }
            r.materials = mats;
        }
    }

    static void Textura(Material m, Texture2D t)
    {
        m.mainTexture = t;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
        m.color = UnityEngine.Color.white;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", UnityEngine.Color.white);
    }

    static void Tenir(Material m, Color c, bool sinTextura)
    {
        if (sinTextura)
        {
            // Se quita el dibujo (chaqueta, jean azul...) pero se deja el relieve (normal map).
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            m.mainTexture = null;
        }
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
    }

    static void Brillo(Material m, float s)
    {
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", s);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", s);
    }

    /// <summary>Pantalon ancho: piernas un poco mas gruesas (las zapatillas quedan igual).</summary>
    void Holgado()
    {
        foreach (var (pierna, pie) in new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot) })
        {
            var p = anim.GetBoneTransform(pierna);
            var f = anim.GetBoneTransform(pie);
            if (p == null || f == null) continue;
            p.localScale = new Vector3(1.22f, 1f, 1.22f);
            var rodilla = anim.GetBoneTransform(pierna == HumanBodyBones.LeftUpperLeg ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);   // el baggy cae ancho
            float total = 1.22f;
            if (rodilla != null && rodilla != f) { rodilla.localScale = new Vector3(1.35f, 1f, 1.35f); total *= 1.35f; }
            f.localScale = new Vector3(1f / total, 1f, 1f / total);
        }
    }

    void Reloj()
    {
        if (codoI == null || manoI == null) return;
        var antebrazo = manoI.position - codoI.position;
        var reloj = new GameObject("Reloj").transform;
        reloj.SetParent(codoI, true);
        reloj.position = codoI.position + antebrazo * 0.86f;
        reloj.rotation = Quaternion.FromToRotation(Vector3.up, antebrazo.normalized);
        var correa = ObjetosCaja.Mat(new Color(0.08f, 0.08f, 0.09f), 0.4f);
        var caja = ObjetosCaja.Mat(new Color(0.75f, 0.76f, 0.78f), 0.85f);
        var vidrio = ObjetosCaja.Mat(new Color(0.05f, 0.07f, 0.1f), 0.95f);
        float esc = 1f / Mathf.Max(0.001f, codoI.lossyScale.x);
        Pieza(reloj, PrimitiveType.Cylinder, Vector3.zero, Quaternion.identity, new Vector3(0.066f, 0.011f, 0.066f) * esc, correa);
        // La esfera sobre el dorso (hacia afuera del cuerpo en la pose inicial).
        // En la pose T (al crearse) el dorso de la mano mira hacia arriba.
        var afuera = reloj.InverseTransformDirection(raiz.up).normalized;
        var rotEsfera = Quaternion.FromToRotation(Vector3.up, afuera);
        Pieza(reloj, PrimitiveType.Cylinder, afuera * 0.033f * esc, rotEsfera, new Vector3(0.036f, 0.006f, 0.036f) * esc, caja);
        Pieza(reloj, PrimitiveType.Cylinder, afuera * 0.036f * esc, rotEsfera, new Vector3(0.029f, 0.002f, 0.029f) * esc, vidrio);
    }

    static void Pieza(Transform padre, PrimitiveType tipo, Vector3 pos, Quaternion rot, Vector3 esc, Material mat)
    {
        var go = GameObject.CreatePrimitive(tipo);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = esc;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ------------------------------------------------------------------ cada frame
    void Update()
    {
        if (anim == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        var c = cam.transform.position;

        // Velocidad (solo horizontal) para saber si camina.
        var dv = c - camAnterior; dv.y = 0f;
        camAnterior = c;
        float v = Time.deltaTime > 0f ? dv.magnitude / Time.deltaTime : 0f;
        velocidad = Mathf.Lerp(velocidad, Mathf.Min(v, 4f), 1f - Mathf.Exp(-8f * Time.deltaTime));

        // El cuerpo gira con la cabeza si te giras bastante o si caminas.
        var f = cam.transform.forward; f.y = 0f;
        if (f.sqrMagnitude > 0.01f)
        {
            float yawCabeza = Quaternion.LookRotation(f).eulerAngles.y;
            float delta = Mathf.DeltaAngle(yaw, yawCabeza);
            if (Mathf.Abs(delta) > 40f || velocidad > 0.3f)
                yaw += delta * (1f - Mathf.Exp(-(velocidad > 0.3f ? 6f : 3f) * Time.deltaTime));
        }

        float suelo = origen != null ? origen.transform.position.y : c.y - 1.6f;
        raiz.rotation = Quaternion.Euler(0f, yaw, 0f);
        raiz.localScale = Vector3.one * escala;
        raiz.position = new Vector3(raiz.position.x, suelo, raiz.position.z);

        string quiero = velocidad > 0.35f ? "Walking" : "Idle";
        if (quiero != estado) { anim.CrossFadeInFixedTime(quiero, 0.25f); estado = quiero; }
    }

    void LateUpdate()
    {
        if (anim == null) return;
        var cam = Camera.main;
        if (cam == null || cuello == null) return;

        // Altura: el cuello del personaje a la altura de tu cuello (una vez, al empezar).
        if (!calibrado && Time.time > calibrarEn)
        {
            float suelo = raiz.position.y;
            float hCuello = (cuello.position.y - suelo) / Mathf.Max(0.01f, escala);
            float quiero = cam.transform.position.y - suelo - 0.13f;
            if (hCuello > 0.5f && quiero > 0.8f) escala = Mathf.Clamp(quiero / hCuello, 0.65f, 1.2f);
            calibrado = true;
            raiz.localScale = Vector3.one * escala;
            Debug.Log("[Cuerpo] Escala " + escala.ToString("0.00") + " (tu cuello a " + quiero.ToString("0.00") + " m, cuello del personaje a " + hCuello.ToString("0.00") + " m, raiz " + raiz.lossyScale.y.ToString("0.00") + ")");
        }

        // El cuello justo debajo de la camara, un poco atras.
        var atras = raiz.forward;
        var deseado = cam.transform.position - atras * 0.17f;   // los ojos quedan sobre el pecho, no dentro
        var d = deseado - cuello.position; d.y = 0f;
        raiz.position += d;

        // No dibujar la cabeza ni las manos del personaje (las manos son las de VR).
        if (cabeza != null) cabeza.localScale = Vector3.one * 0.001f;
        if (manoI != null) manoI.localScale = Vector3.one * 0.001f;
        if (manoD != null) manoD.localScale = Vector3.one * 0.001f;

        // Brazos hacia las manos de VR.
        Transform objI, objD;
        BuscarManos(out objI, out objD);
        if (!avisoManos && Time.time > calibrarEn)
        {
            avisoManos = true;
            Debug.Log("[Cuerpo] Brazos -> izq: " + (objI != null ? objI.name : "(nada)") + " | der: " + (objD != null ? objD.name : "(nada)"));
            if ((objI == null || objD == null) && origen != null)
            {
                var sb = new System.Text.StringBuilder("[Cuerpo] Rig: ");
                foreach (var t in origen.GetComponentsInChildren<Transform>(true))
                {
                    int prof = 0; for (var x = t; x != null && x != origen.transform; x = x.parent) prof++;
                    if (prof <= 3) sb.Append(t.name).Append(t.gameObject.activeInHierarchy ? "" : "(off)").Append(" / ");
                }
                Debug.Log(sb.ToString());
            }
        }
        if (objI != null) Brazo(hombroI, codoI, manoI, objI.position, -1f);
        if (objD != null) Brazo(hombroD, codoD, manoD, objD.position, 1f);

        if (Time.time > tintarManosEn) { TintarManosVR(); tintarManosEn = Time.time + 3f; }
    }

    /// <summary>IK de dos huesos: el hombro y el codo giran para que la muneca llegue al objetivo.</summary>
    void Brazo(Transform a, Transform b, Transform c, Vector3 objetivo, float lado)
    {
        if (a == null || b == null || c == null) return;
        float ab = Vector3.Distance(a.position, b.position);
        float bc = Vector3.Distance(b.position, c.position);
        var aPos = a.position;
        var aT = objetivo - aPos;
        float dist = Mathf.Clamp(aT.magnitude, 0.05f, ab + bc - 0.002f);
        var dir = aT.normalized;

        // El codo hacia abajo y un poco hacia afuera y atras (como un brazo de verdad).
        var pista = -raiz.up * 1f + raiz.right * lado * 0.6f - raiz.forward * 0.3f;
        var normal = Vector3.Cross(dir, pista);
        if (normal.sqrMagnitude < 1e-6f) normal = raiz.right;
        var doblez = Vector3.Cross(normal.normalized, dir).normalized;

        float x = (ab * ab - bc * bc + dist * dist) / (2f * dist);
        float y = Mathf.Sqrt(Mathf.Max(0f, ab * ab - x * x));
        var codo = aPos + dir * x + doblez * y;

        a.rotation = Quaternion.FromToRotation(b.position - aPos, codo - aPos) * a.rotation;
        var muneca = aPos + dir * dist;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, muneca - b.position) * b.rotation;
    }

    /// <summary>Las munecas de las manos de VR (o los mandos si no hay manos).</summary>
    void BuscarManos(out Transform izq, out Transform der)
    {
        izq = der = null;
        foreach (var e in FindObjectsByType<XRHandTrackingEvents>(FindObjectsSortMode.None))
        {
            if (!e.isActiveAndEnabled || !e.handIsTracked) continue;
            var drv = e.GetComponent<XRHandSkeletonDriver>();
            var t = drv != null && drv.rootTransform != null ? drv.rootTransform : e.transform;
            if (e.handedness == Handedness.Left) izq = t; else if (e.handedness == Handedness.Right) der = t;
        }
        if ((izq != null && der != null) || origen == null) return;
        // Sin manos seguidas: el objeto del mando ("Left Controller", "Left Hand"...) que
        // este mas arriba en la jerarquia del rig.
        int pI = int.MaxValue, pD = int.MaxValue;
        foreach (var t in origen.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLowerInvariant();
            if (!(n.Contains("controller") || n.Contains("hand"))) continue;
            if (n.Contains("stabilized") || n.Contains("visual") || n.Contains("interactor") || n.Contains("pose") || n.Contains("attach") || n.Contains("ray")) continue;
            int prof = 0; for (var x = t; x != null && x != origen.transform; x = x.parent) prof++;
            if (izq == null || pI > prof) { if (n.Contains("left")) { izq = t; pI = prof; } }
            if (der == null || pD > prof) { if (n.Contains("right")) { der = t; pD = prof; } }
        }
    }

    /// <summary>Las manos de VR del mismo color de piel.</summary>
    readonly HashSet<Renderer> tintadas = new HashSet<Renderer>();
    void TintarManosVR()
    {
        foreach (var e in FindObjectsByType<XRHandTrackingEvents>(FindObjectsSortMode.None))
            foreach (var r in e.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!tintadas.Add(r)) continue;
                var m = r.material;
                foreach (var p in new[] { "_BaseColor", "_Color", "_MainColor", "_HandColor" })
                    if (m.HasProperty(p)) m.SetColor(p, Piel);
            }
    }
}
