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

        izquierda = PrepararMano(true);
        derecha = PrepararMano(false);
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
            p.localScale = new Vector3(1.3f, 1f, 1.3f);
            var rodilla = anim.GetBoneTransform(pierna == HumanBodyBones.LeftUpperLeg ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);   // el baggy cae ancho
            float total = 1.3f;
            if (rodilla != null && rodilla != f) { rodilla.localScale = new Vector3(1.6f, 1f, 1.6f); total *= 1.6f; }
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
            // El modelo no viene en metros (Remy mide ~3.5 al importar): la escala sale de
            // comparar alturas. Tu cuello se toma entre 1.1 y 1.6 m (sentado / muy alto).
            quiero = Mathf.Clamp(quiero, 1.1f, 1.6f);
            if (hCuello > 0.1f) escala = quiero / hCuello;
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
        if (objI != null) { Brazo(hombroI, codoI, manoI, objI.position, -1f); Mano(izquierda, objI, -1f); }
        if (objD != null) { Brazo(hombroD, codoD, manoD, objD.position, 1f); Mano(derecha, objD, 1f); }
        // Las manos que se ven son las del personaje (brazo y mano de una sola pieza):
        // las manos de VR se esconden (siguen funcionando para agarrar).
        foreach (var r in manosVR) if (r != null && r.enabled) r.enabled = false;

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

    readonly List<Renderer> manosVR = new List<Renderer>();
    readonly HashSet<Renderer> vistas = new HashSet<Renderer>();
    /// <summary>Junta las mallas de las manos de VR del rig (para esconderlas).</summary>
    void TintarManosVR()
    {
        if (origen == null) return;
        foreach (var r in origen.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (vistas.Add(r)) manosVR.Add(r);
    }

    // ================================================================== manos
    /// <summary>Cuantas veces se toco un boton con el dedo (lo usa el tutorial).</summary>
    public static int Toques { get; private set; }

    class Mano_
    {
        public bool izq;
        public Transform mano;
        public Vector3 dedosLocal, palmaLocal;
        public Transform[][] dedos;          // [pulgar, indice, medio, anular, menique][prox, inter, distal]
        public float cierre, indice;         // 0 abierta .. 1 cerrada (suave)
        public float proximoToque;
        public Transform punta;
    }
    Mano_ izquierda, derecha;
    readonly List<UnityEngine.UI.Button> botones = new List<UnityEngine.UI.Button>();
    float buscarBotonesEn;

    Mano_ PrepararMano(bool izq)
    {
        var h = new Mano_ { izq = izq, mano = izq ? manoI : manoD };
        if (h.mano == null) return null;
        HumanBodyBones B(string n) => (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), (izq ? "Left" : "Right") + n);
        string[] nombres = { "Thumb", "Index", "Middle", "Ring", "Little" };
        string[] partes = { "Proximal", "Intermediate", "Distal" };
        h.dedos = new Transform[5][];
        for (int d = 0; d < 5; d++)
        {
            h.dedos[d] = new Transform[3];
            for (int k = 0; k < 3; k++) h.dedos[d][k] = anim.GetBoneTransform(B(nombres[d] + partes[k]));
        }
        var medio = h.dedos[2][0]; var pulgar = h.dedos[0][0];
        if (medio == null || pulgar == null) return h;
        // En la pose de inicio: hacia donde apuntan los dedos y hacia donde mira la palma.
        var f = (medio.position - h.mano.position).normalized;
        var t = (pulgar.position - h.mano.position).normalized;
        var palma = Vector3.Cross(f, t).normalized * (izq ? -1f : 1f);
        h.dedosLocal = Quaternion.Inverse(h.mano.rotation) * f;
        h.palmaLocal = Quaternion.Inverse(h.mano.rotation) * palma;
        // Punta del indice (un poco mas alla del ultimo hueso).
        var d1 = h.dedos[1][2]; var d0 = h.dedos[1][1];
        if (d1 != null && d0 != null)
        {
            h.punta = new GameObject("Punta indice").transform;
            h.punta.SetParent(d1, false);
            h.punta.position = d1.position + (d1.position - d0.position) * 0.9f;
        }
        return h;
    }

    static float Eje(bool izq, string nombre)
    {
        foreach (var dev in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (!(dev is UnityEngine.InputSystem.XR.XRController)) continue;
            bool es = false;
            foreach (var u in dev.usages) if (u == (izq ? UnityEngine.InputSystem.CommonUsages.LeftHand : UnityEngine.InputSystem.CommonUsages.RightHand)) es = true;
            if (!es) continue;
            if (dev.TryGetChildControl(nombre) is UnityEngine.InputSystem.Controls.AxisControl a) return a.ReadValue();
        }
        return 0f;
    }

    /// <summary>
    /// Gira la mano del personaje como tu mano/mando y mueve los dedos:
    ///   GRIP = puño (agarrar) · GATILLO (R2) = dedo indice estirado para tocar pantallas.
    /// Con el indice estirado, tocar un boton de un panel (la caja) lo aprieta.
    /// </summary>
    void Mano(Mano_ h, Transform objetivo, float lado)
    {
        if (h == null || h.mano == null || h.dedos == null) return;
        bool deVR = objetivo.GetComponentInParent<XRHandTrackingEvents>() != null;
        // Mano de VR (XR Hands): dedos a +Z, palma a -Y. Mando: dedos adelante, palma hacia adentro.
        var fD = objetivo.forward;
        var pD = deVR ? -objetivo.up : (lado > 0 ? -objetivo.right : objetivo.right);
        if (h.dedosLocal.sqrMagnitude > 0.5f)
            h.mano.rotation = Quaternion.LookRotation(fD, -pD) * Quaternion.Inverse(Quaternion.LookRotation(h.dedosLocal, -h.palmaLocal));

        float grip = Eje(h.izq, "grip"), gatillo = Eje(h.izq, "trigger");
        bool apuntar = gatillo > 0.5f && grip < 0.5f;
        float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
        float cierreQuiero = Mathf.Max(grip, apuntar ? 1f : 0.15f);
        float indiceQuiero = apuntar ? 0f : Mathf.Max(grip, 0.15f + 0.4f * gatillo);
        h.cierre = Mathf.Lerp(h.cierre, cierreQuiero, k);
        h.indice = Mathf.Lerp(h.indice, indiceQuiero, k);

        var manoF = h.mano.rotation * h.dedosLocal;
        var manoP = h.mano.rotation * h.palmaLocal;
        var eje = Vector3.Cross(manoF, manoP).normalized;
        float[] grados = { 70f, 95f, 65f };
        for (int d = 0; d < 5; d++)
        {
            float c = d == 1 ? h.indice : h.cierre;
            float factor = d == 0 ? 0.45f : 1f;
            for (int j = 0; j < 3; j++)
            {
                var b = h.dedos[d][j];
                if (b == null) continue;
                b.rotation = Quaternion.AngleAxis(grados[j] * c * factor, eje) * b.rotation;
            }
        }

        if (apuntar && h.punta != null) Tocar(h);
    }

    void Tocar(Mano_ h)
    {
        if (Time.time < h.proximoToque) return;
        if (Time.time > buscarBotonesEn)
        {
            botones.Clear();
            foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                var cv = b.GetComponentInParent<Canvas>();
                if (cv != null && cv.renderMode == RenderMode.WorldSpace) botones.Add(b);
            }
            buscarBotonesEn = Time.time + 1f;
        }
        var p = h.punta.position;
        var esquinas = new Vector3[4];
        foreach (var b in botones)
        {
            if (b == null || !b.isActiveAndEnabled || !b.interactable) continue;
            var rt = (RectTransform)b.transform;
            rt.GetWorldCorners(esquinas);
            var plano = new Plane(esquinas[0], esquinas[1], esquinas[2]);
            if (Mathf.Abs(plano.GetDistanceToPoint(p)) > 0.025f) continue;
            var local = rt.InverseTransformPoint(p);
            if (!rt.rect.Contains(new Vector2(local.x, local.y))) continue;
            b.onClick.Invoke();
            Toques++;
            h.proximoToque = Time.time + 0.6f;
            Haptica.Ambos(0.35f, 0.05f);
            Debug.Log("[Cuerpo] Dedo toco: " + b.name);
            return;
        }
    }

}
