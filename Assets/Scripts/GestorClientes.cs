using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// GESTOR DE CLIENTES
/// ------------------
/// Hace aparecer clientes (NPC) que:
///   entran por la puerta -> miran estantes -> hacen fila -> dejan sus productos en la
///   caja -> te pagan con un billete -> esperan que cobres -> se van.
///
/// Se instala solo en MinimarketXR_Full y MinimarketXR_Night.
/// Necesita Assets/Resources/ConfigClientes.asset (lo crea la herramienta
/// Tools/Minimarket/Clientes/1 - Configurar personajes y animaciones).
///
/// El "camino" (NavMesh) por donde caminan se hornea con
/// Tools/Minimarket/Clientes/2 - Hornear camino de clientes (y se guarda en
/// Assets/Clientes/CaminoClientes.asset). Hay que hornearlo en el editor porque en el
/// Quest no se puede construir mientras se juega.
/// </summary>
public class GestorClientes : MonoBehaviour
{
    public static GestorClientes I { get; private set; }

    public ConfigClientes Config { get; private set; }
    public Transform Mostrador { get; private set; }
    public Transform RaizProductos { get; private set; }
    public int AgentTypeID { get; private set; } = 0;
    public bool Listo { get; private set; }

    public Vector3 PuntoCaja { get; private set; }
    public Vector3 ZonaProductos { get; private set; }
    public Vector3 DireccionFila { get; private set; }
    public Vector3 EntradaDentro { get; private set; }
    public Vector3 EntradaFuera { get; private set; }
    public bool EntranDesdeFuera { get; private set; }

    public int Atendidos { get; private set; }
    public int AtendidosBien { get; private set; }
    public int SeFueronMolestos { get; private set; }

    readonly List<Cliente> clientes = new List<Cliente>();
    readonly List<Cliente> fila = new List<Cliente>();
    readonly List<Transform> productosEstante = new List<Transform>();

    CajaRegistradora caja;
    public CajaRegistradora Caja => caja != null ? caja : (caja = FindAnyObjectByType<CajaRegistradora>());

    NavMeshDataInstance navInstancia;
    float proximoCliente;
    int indicePersonaje;

    // ================================================================== auto-instalacion

    static bool suscrito;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { suscrito = false; I = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        if (!suscrito)
        {
            SceneManager.sceneLoaded += (esc, modo) => Asegurar(esc);
            suscrito = true;
        }
        Asegurar(SceneManager.GetActiveScene());
    }

    static void Asegurar(Scene esc)
    {
        if (!esc.IsValid() || !esc.name.StartsWith("MinimarketXR")) return;
        if (FindAnyObjectByType<GestorClientes>() != null) return;
        var go = new GameObject("Gestor de Clientes");
        SceneManager.MoveGameObjectToScene(go, esc);
        go.AddComponent<GestorClientes>();
    }

    // ================================================================== inicio

    IEnumerator Start()
    {
        I = this;
        Config = Resources.Load<ConfigClientes>("ConfigClientes");
        if (Config == null || Config.personajes == null || Config.personajes.Length == 0 || Config.animador == null)
        {
            Debug.LogWarning("[Clientes] Falta configurar. Usa Tools/Minimarket/Clientes/1 - Configurar personajes y animaciones.");
            enabled = false;
            yield break;
        }

        var raiz = GameObject.Find("Productos");
        RaizProductos = raiz != null ? raiz.transform : null;

        // La caja "de verdad" es la que tiene el lector encima.
        var lector = FindAnyObjectByType<LectorCodigos>();
        Mostrador = BuscarMostrador(lector != null ? lector.transform.position : Vector3.zero);
        if (Mostrador == null)
        {
            Debug.LogWarning("[Clientes] No encontre la caja (Pref_CashCounter).");
            enabled = false;
            yield break;
        }

        // Espera un frame para que la escena termine de acomodarse.
        yield return null;
        IniciarLog();
        if (Config.camino != null)
        {
            navInstancia = NavMesh.AddNavMeshData(Config.camino);
            Log("Camino horneado cargado.");
        }
        else
        {
#if UNITY_EDITOR
            Debug.LogWarning("[Clientes] No hay camino horneado: lo construyo ahora (solo funciona en el editor). " +
                             "Usa Tools/Minimarket/Clientes/2 - Hornear camino de clientes antes de hacer el build.");
            var data = ConstruirCamino();
            navInstancia = NavMesh.AddNavMeshData(data);
#else
            Debug.LogWarning("[Clientes] Falta hornear el camino (Tools/Minimarket/Clientes/2). Sin clientes.");
            enabled = false;
            yield break;
#endif
        }
        CalcularPuntos();
        RecolectarEstantes();

        Listo = true;
        proximoCliente = Time.time + Config.primerClienteEn;
        Log("Listo. Personajes=" + Config.personajes.Length +
                  " | productos en estantes=" + productosEstante.Count +
                  " | entran desde " + (EntranDesdeFuera ? "afuera" : "detras de la puerta") +
                  " | caja=" + Mostrador.name);
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        if (navInstancia.valid) NavMesh.RemoveNavMeshData(navInstancia);
    }

    void Update()
    {
        if (!Listo) return;
        clientes.RemoveAll(c => c == null);
        fila.RemoveAll(c => c == null);

        if (Time.time >= proximoCliente)
        {
            if (clientes.Count < Config.maxClientes) TraerCliente();
            proximoCliente = Time.time + Random.Range(Config.intervalo.x, Config.intervalo.y);
        }
    }

    // ================================================================== clientes

    /// <summary>Hace entrar un cliente ahora mismo (tambien desde el menu de pruebas).</summary>
    public Cliente TraerCliente()
    {
        if (!Listo) return null;
        var modelo = Config.personajes[indicePersonaje % Config.personajes.Length];
        indicePersonaje++;
        if (modelo == null) return null;

        Vector3 inicio = EntranDesdeFuera ? EntradaFuera : EntradaDentro;
        var mira = EntradaDentro - EntradaFuera; mira.y = 0;
        var go = Instantiate(modelo, inicio, mira.sqrMagnitude > 0.01f ? Quaternion.LookRotation(mira) : Quaternion.identity);
        // Inactivo mientras se arma: el NavMeshAgent se crea recien al activarlo,
        // ya con el tipo y la posicion correctos.
        go.SetActive(false);
        go.name = "Cliente " + modelo.name;
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);

        var c = go.AddComponent<Cliente>();
        c.Iniciar(this, inicio);   // lo activa al final
        clientes.Add(c);
        Log("Entra " + go.name + " (" + clientes.Count + " en la tienda)");
        return c;
    }

    public int UnirseAFila(Cliente c)
    {
        if (!fila.Contains(c)) fila.Add(c);
        return fila.IndexOf(c);
    }

    public int PosicionEnFila(Cliente c) => fila.IndexOf(c);

    public void SalirDeFila(Cliente c) => fila.Remove(c);

    public void Quitar(Cliente c)
    {
        clientes.Remove(c);
        fila.Remove(c);
    }

    public Cliente ClienteEnCaja => fila.Count > 0 ? fila[0] : null;

    /// <summary>
    /// Punto detras de la fila (hacia dentro de la tienda). Los clientes pasan por aqui
    /// antes de formarse, para entrar a la fila POR DETRAS y no chocar con el que esta
    /// pagando en el pasillo angosto junto a la caja.
    /// </summary>
    public Vector3 InicioFila =>
        Muestrear(PuntoCaja + DireccionFila * (Config.separacionFila * (Config.maxClientes + 2)), 1.5f, PuntoCaja);

    public Vector3 PuntoFila(int i)
    {
        if (i <= 0) return PuntoCaja;
        // +0.8 m: espacio para el carrito que deja estacionado el que esta pagando.
        return Muestrear(PuntoCaja + DireccionFila * (Config.separacionFila * i + 0.8f), 1.2f, PuntoCaja);
    }

    public void RegistrarResultado(bool bien, bool molesto)
    {
        Atendidos++;
        if (bien) AtendidosBien++;
        if (molesto) SeFueronMolestos++;
        Log("Atendidos=" + Atendidos + " bien=" + AtendidosBien + " molestos=" + SeFueronMolestos);
    }

    /// <summary>Avisa a los lectores que hay productos nuevos (los que deja el cliente).</summary>
    public void ActualizarLectores()
    {
        foreach (var l in FindObjectsByType<LectorCodigos>(FindObjectsInactive.Exclude))
            l.ActualizarProductos();
    }

    public Transform ProductoAlAzar()
    {
        productosEstante.RemoveAll(t => t == null);
        if (productosEstante.Count == 0) return null;
        return productosEstante[Random.Range(0, productosEstante.Count)];
    }

    public List<Transform> ProductosCerca(Vector3 punto, float radio)
    {
        var r = new List<Transform>();
        float r2 = radio * radio;
        foreach (var t in productosEstante)
            if (t != null && (t.position - punto).sqrMagnitude <= r2) r.Add(t);
        return r;
    }

    static readonly float[] Radios = { 0f, 0.6f, 1.0f, 1.4f, 1.8f };

    /// <summary>
    /// Punto del suelo frente a un producto del estante AL QUE SE PUEDA LLEGAR
    /// desde la caja (si no, el cliente se queda atascado). null = no hay.
    /// </summary>
    public Vector3? PuntoFrenteA(Vector3 producto)
    {
        var suelo = new Vector3(producto.x, PuntoCaja.y, producto.z);
        var camino = new NavMeshPath();
        foreach (var r in Radios)
        {
            int pasos = r == 0f ? 1 : 8;
            for (int i = 0; i < pasos; i++)
            {
                float a = i * Mathf.PI * 2f / pasos;
                var p = suelo + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!NavMesh.SamplePosition(p, out var hit, 0.45f, Filtro())) continue;
                if (Mathf.Abs(hit.position.y - PuntoCaja.y) > 0.3f) continue;   // encima de un estante, no
                if (NavMesh.CalculatePath(PuntoCaja, hit.position, Filtro(), camino) &&
                    camino.status == NavMeshPathStatus.PathComplete)
                    return hit.position;
            }
        }
        return null;
    }

    // ================================================================== navmesh

    /// <summary>
    /// Construye el camino (NavMesh) de la escena abierta. Lo usa la herramienta de
    /// hornear en el editor. Tipo de agente 0 (Humanoid) con medidas de persona delgada.
    /// </summary>
    public static NavMeshData ConstruirCamino()
    {
        var s = NavMesh.GetSettingsByID(0);
        s.agentRadius = 0.28f;
        s.agentHeight = 1.8f;
        s.agentClimb = 0.35f;
        s.agentSlope = 40f;

        var raizGO = GameObject.Find("Productos");
        var raiz = raizGO != null ? raizGO.transform : null;
        var puerta = FindAnyObjectByType<PuertaAutomatica>();

        // Zona: la tienda + la entrada.
        var edificio = GameObject.Find("MarketBuilding");
        Bounds b = edificio != null ? Limites(edificio.transform) : new Bounds(Vector3.zero, new Vector3(40, 10, 40));
        if (puerta != null) b.Encapsulate(puerta.transform.position + puerta.transform.forward * 7f);
        b.Expand(new Vector3(2f, 0f, 2f));

        // SOLO a la altura del suelo: hasta 0.5 m sobre el piso de la tienda. Asi no se
        // genera camino encima de estantes, mostradores o cajas (los clientes no se suben).
        var piso = GameObject.Find("Floor");
        float pisoY = piso != null ? Limites(piso.transform).max.y : 0.6f;
        float minY = pisoY - 1.2f, maxY = pisoY + 0.5f;
        b.SetMinMax(new Vector3(b.min.x, minY, b.min.z), new Vector3(b.max.x, maxY, b.max.z));

        var fuentes = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(b, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                                      new List<NavMeshBuildMarkup>(), fuentes);
        int antes = fuentes.Count;
        fuentes.RemoveAll(f => Excluir(f.component, raiz, puerta));

        var data = NavMeshBuilder.BuildNavMeshData(s, fuentes, b, Vector3.zero, Quaternion.identity);
        Debug.Log("[Clientes] Camino construido con " + fuentes.Count + "/" + antes + " colisionadores.");
        return data;
    }

    static bool Excluir(Component c, Transform raizProductos, PuertaAutomatica puerta)
    {
        if (c == null) return false;
        var col = c as Collider;
        if (col != null && col.isTrigger) return true;
        var t = c.transform;
        if (raizProductos != null && t.IsChildOf(raizProductos)) return true;       // productos
        if (puerta != null && t.IsChildOf(puerta.transform)) return true;          // la puerta se abre
        if (c.GetComponentInParent<CharacterController>() != null) return true;     // jugador
        if (c.GetComponentInParent<Cliente>() != null) return true;
        var rb = col != null ? col.attachedRigidbody : null;
        if (rb != null && !rb.isKinematic) return true;                             // carritos, cosas sueltas
        if (c.GetComponentInParent<XRGrabInteractable>() != null) return true;      // lector, objetos agarrables
        return false;
    }

    // ================================================================== log (para pruebas)

    static string rutaLog;

    static void IniciarLog()
    {
#if UNITY_EDITOR
        rutaLog = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs", "clientes_log.txt");
        try { System.IO.File.WriteAllText(rutaLog, "== " + System.DateTime.Now + "\n"); } catch { rutaLog = null; }
#endif
    }

    /// <summary>Debug.Log + (en el editor) Logs/clientes_log.txt</summary>
    public static void Log(string msg)
    {
        Debug.Log("[Clientes] " + msg);
#if UNITY_EDITOR
        if (rutaLog != null)
            try { System.IO.File.AppendAllText(rutaLog, Time.time.ToString("0.0") + "  " + msg + "\n"); } catch { }
#endif
    }

    // ================================================================== puntos

    void CalcularPuntos()
    {
        var m = Mostrador;
        // El suelo junto a la caja (no encima: ahi el rayo choca con el mostrador).
        float pisoY = SueloEn(m.TransformPoint(Config.puntoClienteLocal));

        PuntoCaja = Muestrear(Plano(m.TransformPoint(Config.puntoClienteLocal), pisoY), 1.5f, m.position);
        ZonaProductos = m.TransformPoint(Config.zonaProductosLocal);
        var dir = m.TransformDirection(Config.direccionFilaLocal); dir.y = 0f;
        DireccionFila = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.back;

        var puerta = FindAnyObjectByType<PuertaAutomatica>();
        if (puerta != null)
        {
            var p = puerta.transform;
            var adelante = p.forward; adelante.y = 0; adelante.Normalize();
            // "forward" de la puerta apunta hacia afuera: la tienda esta detras.
            var hacia = (m.position - p.position); hacia.y = 0;
            if (Vector3.Dot(adelante, hacia) > 0) adelante = -adelante;

            EntradaDentro = Muestrear(Plano(p.position - adelante * 1.6f, pisoY), 2f, PuntoCaja);
            var fuera = p.position + adelante * 4.5f;
            EntradaFuera = Muestrear(Plano(fuera, p.position.y), 2f, EntradaDentro);
        }
        else
        {
            EntradaDentro = EntradaFuera = Muestrear(PuntoCaja + Vector3.forward * 3f, 3f, PuntoCaja);
        }

        // Si desde afuera no se llega a la caja (escalones, etc.), aparecen detras de la puerta.
        var camino = new NavMeshPath();
        EntranDesdeFuera = NavMesh.CalculatePath(EntradaFuera, PuntoCaja, Filtro(), camino)
                           && camino.status == NavMeshPathStatus.PathComplete
                           && (EntradaFuera - EntradaDentro).sqrMagnitude > 1f;

        Log("Caja=" + PuntoCaja + " Productos=" + ZonaProductos + " Fila=" + DireccionFila +
                  " EntradaDentro=" + EntradaDentro + " EntradaFuera=" + EntradaFuera);
    }

    void RecolectarEstantes()
    {
        productosEstante.Clear();
        if (RaizProductos == null) return;
        foreach (var g in RaizProductos.GetComponentsInChildren<XRGrabInteractable>(false))
        {
            if (BaseDatosProductos.Buscar(g.name) == null) continue;
            // Lejos de la caja (si no, cogeria los que deja otro cliente).
            if ((g.transform.position - Mostrador.position).sqrMagnitude < 9f) continue;
            productosEstante.Add(g.transform);
        }
    }

    public NavMeshQueryFilter Filtro()
    {
        return new NavMeshQueryFilter { agentTypeID = AgentTypeID, areaMask = NavMesh.AllAreas };
    }

    public Vector3 Muestrear(Vector3 p, float radio, Vector3 siFalla)
    {
        if (NavMesh.SamplePosition(p, out var hit, radio, Filtro())) return hit.position;
        if (NavMesh.SamplePosition(p, out hit, radio * 3f, Filtro())) return hit.position;
        return siFalla;
    }

    float SueloEn(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return p.y;
    }

    static Vector3 Plano(Vector3 p, float y) => new Vector3(p.x, y, p.z);

    static Bounds Limites(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 30f);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Transform BuscarMostrador(Vector3 cerca)
    {
        Transform mejor = null; float d = float.MaxValue;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!t.name.StartsWith("Pref_CashCounter")) continue;
            float dd = (t.position - cerca).sqrMagnitude;
            if (dd < d) { d = dd; mejor = t; }
        }
        return mejor;
    }

    // ================================================================== pruebas (sin visor)

    /// <summary>
    /// Hace de cajero (pruebas sin visor): escanea lo del cliente en caja y, si cobrar es
    /// true, sigue los pasos de la pantalla con la forma de pago que pide el cliente.
    /// </summary>
    public IEnumerator SimularCajero() { return SimularCajero(true); }

    public IEnumerator SimularCajero(bool cobrar)
    {
        var c = ClienteEnCaja;
        var cj = Caja;
        if (c == null || cj == null || !c.EnCaja) { Log("No hay cliente esperando en la caja."); yield break; }
        foreach (var p in c.ProductosQueLleva)
        {
            cj.AgregarProducto(p, 1);
            yield return new WaitForSeconds(0.4f);
        }
        if (!cobrar) yield break;

        float t = Time.time;
        while (c != null && !c.YaDijoComoPaga && Time.time - t < 20f) yield return null;
        if (c == null || !c.YaDijoComoPaga) yield break;
        yield return new WaitForSeconds(1f);

        cj.IrACobrar();
        yield return new WaitForSeconds(0.5f);
        if (c.QuiereTarjeta)
        {
            cj.ElegirTarjeta();
            yield return new WaitForSeconds(0.5f);
            cj.EnviarPOS();
            yield break;   // el POS termina solo
        }
        cj.ElegirEfectivo();
        yield return new WaitForSeconds(0.3f);
        foreach (var d in CajaRegistradora.DesgloseLista(c.PagoCent)) cj.ClienteEntrega(d);
        cj.ConfirmarRecibido();
        yield return new WaitForSeconds(0.8f);
        if (cj.PasoActual == CajaRegistradora.Paso.EntregarVuelto)
        {
            foreach (var d in CajaRegistradora.DesgloseLista(cj.VueltoCent))
            {
                cj.EntregarDenominacion(d);
                yield return new WaitForSeconds(0.2f);
            }
            cj.ConfirmarVuelto();
        }
    }
}
