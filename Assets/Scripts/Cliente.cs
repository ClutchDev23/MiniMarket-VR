using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Un cliente del minimarket (lo crea GestorClientes; no hace falta ponerlo a mano).
///
///   1. Entra por la puerta.
///   2. Visita 1-2 estantes y elige productos.
///   3. Hace fila en la caja.
///   4. Deja sus productos en la banda (copias que se pueden escanear).
///   5. Cuando escaneaste todo, dice con cuanto paga (ej. "Aqui tienes S/ 50").
///   6. Cuando cobras, revisa que todo este bien y se va (llevandose sus productos).
///
/// Si le cobras de mas, de menos, o registras un pago distinto al que te dio, te lo dice.
/// </summary>
[DisallowMultipleComponent]
public class Cliente : MonoBehaviour
{
    // ------------------------------------------------------------------ publico
    public bool EnCaja { get; private set; }
    public int PagoCent { get; private set; }
    /// <summary>true = quiere pagar con tarjeta (POS). Lo decide al entrar.</summary>
    public bool QuiereTarjeta { get; private set; }
    /// <summary>Ya dijo como paga ("Aqui tienes S/ 50" o "Con tarjeta").</summary>
    public bool YaDijoComoPaga { get; private set; }
    public IReadOnlyList<Producto> ProductosQueLleva => lista;

    // ------------------------------------------------------------------ privado
    GestorClientes g;
    NavMeshAgent agente;
    Animator anim;
    string animActual = "";
    bool hablando;
    bool mirando;

    readonly List<Producto> lista = new List<Producto>();
    readonly List<GameObject> originales = new List<GameObject>();
    readonly List<GameObject> copias = new List<GameObject>();
    int totalEsperadoCent;

    CajaRegistradora.Venta ventaCobrada;

    // burbuja de dialogo
    Transform burbuja;
    Text txtBurbuja;
    float burbujaHasta;
    float hablarHasta;
    float altura = 1.8f;

    CarritoCliente carrito;

    // voz
    AudioSource voz;
    bool esMujer;
    int totalAlDecir;

    // ================================================================== inicio

    public void Iniciar(GestorClientes gestor, Vector3 inicio)
    {
        g = gestor;

        anim = GetComponentInChildren<Animator>(true);
        if (anim == null) anim = gameObject.AddComponent<Animator>();
        anim.runtimeAnimatorController = g.Config.animador;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // solo son 3-4 clientes: asi nunca se ven en pose T

        QuiereTarjeta = Random.value < g.Config.probTarjeta;

        agente = gameObject.AddComponent<NavMeshAgent>();
        agente.agentTypeID = g.AgentTypeID;
        agente.radius = 0.28f;
        agente.height = 1.8f;
        agente.speed = g.Config.velocidad * Random.Range(0.9f, 1.1f);
        agente.angularSpeed = 300f;
        agente.acceleration = 6f;
        agente.stoppingDistance = 0.08f;
        agente.autoBraking = true;
        agente.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        agente.avoidancePriority = Random.Range(40, 60);

        transform.position = inicio;
        gameObject.SetActive(true);          // aqui se crea el agente, ya en el camino

        // TODOS DEL MISMO TAMANO: se mide el modelo (en pose T) y se escala a la altura
        // de la configuracion. El agente se mide en unidades locales, asi que se corrige.
        float alto = MedirAltura();
        if (alto > 0.5f)
        {
            float f = g.Config.alturaClientes / alto;
            transform.localScale = transform.localScale * f;
            float s = Mathf.Max(0.01f, transform.lossyScale.y);
            agente.radius = 0.28f / s;
            agente.height = 1.8f / s;
            altura = g.Config.alturaClientes;
        }
        CrearBurbuja();
        CrearVoz();
        if (agente.isOnNavMesh) agente.Warp(inicio);
        else GestorClientes.Log(name + ": no quedo sobre el camino en " + inicio);

        PuertaAutomatica.Extras.Add(transform);
        StartCoroutine(Vida());
    }

    float MedirAltura()
    {
        var rs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (rs.Length == 0) return 0f;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.max.y - transform.position.y;
    }

    void OnDestroy()
    {
        PuertaAutomatica.Extras.Remove(transform);
        foreach (var c in copias) if (c != null) Destroy(c);
        if (g != null) g.Quitar(this);
        var cj = g != null ? g.Caja : null;
        if (cj != null) cj.VentaCobrada -= AlCobrar;
    }

    // ================================================================== vida del cliente

    IEnumerator Vida()
    {
        // 1. entrar (y, si hay uno libre cerca, tomar un carrito)
        yield return IrA(g.EntradaDentro);
        if (Random.value < g.Config.probCarrito) yield return TomarCarrito();

        // 2. comprar
        int paradas = Random.Range(1, 3);
        int objetivo = Random.Range(g.Config.productosPorCliente.x, g.Config.productosPorCliente.y + 1);
        for (int i = 0; i < paradas && lista.Count < objetivo; i++)
        {
            // Busca un producto al que se pueda llegar caminando.
            Transform prod = null; Vector3? frente = null;
            for (int intento = 0; intento < 10 && frente == null; intento++)
            {
                prod = g.ProductoAlAzar();
                if (prod != null) frente = g.PuntoFrenteA(prod.position);
            }
            if (prod == null || frente == null) break;
            yield return IrA(frente.Value);
            yield return MirarHacia(prod.position);
            mirando = true;
            yield return new WaitForSeconds(Random.Range(2.5f, 5f));
            mirando = false;

            // Elige ese producto (a veces 2 iguales) y quiza otro de al lado.
            Agregar(prod.gameObject, Random.value < 0.3f ? 2 : 1, objetivo);
            var cerca = g.ProductosCerca(prod.position, 1.2f);
            if (cerca.Count > 0 && Random.value < 0.7f)
                Agregar(cerca[Random.Range(0, cerca.Count)].gameObject, 1, objetivo);
        }
        if (lista.Count == 0)
        {
            var p = g.ProductoAlAzar();
            if (p != null) Agregar(p.gameObject, 1, 1);
        }
        if (lista.Count == 0) { yield return Irse(); yield break; }

        foreach (var p in lista) totalEsperadoCent += CajaRegistradora.ACentimos(p.precio);
        GestorClientes.Log(name + " eligio " + lista.Count + " producto(s), total esperado " + CajaRegistradora.Dinero(totalEsperadoCent));

        // 3. hacer fila (se llega por detras de la fila)
        yield return IrA(g.InicioFila);
        g.UnirseAFila(this);
        while (true)
        {
            int pos = g.PosicionEnFila(this);
            if (pos < 0) pos = g.UnirseAFila(this);
            yield return IrA(g.PuntoFila(pos), () => g.PosicionEnFila(this) != pos);
            if (g.PosicionEnFila(this) != pos) continue;   // avanzo la fila mientras caminaba

            yield return MirarHacia(g.ZonaProductos);
            if (pos == 0)
            {
                // Deja el carrito detras de el, para no chocar con el mostrador.
                if (carrito != null && carrito.Carrito != null)
                {
                    carrito.Estacionar(transform.position + g.DireccionFila * 0.95f, Quaternion.LookRotation(-g.DireccionFila));
                }
                break;
            }
            while (g.PosicionEnFila(this) == pos) yield return null;
        }

        // 4. en la caja
        EnCaja = true;
        var caja = g.Caja;
        if (caja != null) caja.VentaCobrada += AlCobrar;   // antes de nada: asi no se pierde un cobro rapido
        Decir(Saludo(out var claveSaludo), 4f, claveSaludo);
        yield return PonerProductos();
        GestorClientes.Log(name + " llego a la caja y dejo " + copias.Count + " producto(s) en " + g.ZonaProductos +
                           (carrito != null && carrito.Carrito != null ? " (los saco del carrito)" : ""));
        yield return new WaitForSeconds(0.5f);

        // 5. esperar a que escanees todo
        float t0 = Time.time, ultimoCambio = Time.time;
        int ultimoTotal = -1;
        bool listoParaPagar = false;
        while (!listoParaPagar && ventaCobrada == null)
        {
            caja = g.Caja;
            if (caja != null && caja.EstadoActual == CajaRegistradora.Estado.Vendiendo)
            {
                if (caja.TotalCent != ultimoTotal) { ultimoTotal = caja.TotalCent; ultimoCambio = Time.time; }
                if (TodoEscaneado(caja)) listoParaPagar = true;
                // El cajero ya pulso COBRAR.
                else if (caja.PasoActual != CajaRegistradora.Paso.Escaneando && caja.TotalCent > 0) listoParaPagar = true;
                // Si dejaste de escanear un rato, pregunta igual.
                else if (caja.TotalCent > 0 && Time.time - ultimoCambio > 12f) listoParaPagar = true;
            }
            if (Time.time - t0 > g.Config.paciencia)
            {
                Decir("Ya me cansé de esperar... me voy.", 4f, "cansado");
                g.RegistrarResultado(false, true);
                if (g.Caja != null) g.Caja.ClienteSeFue();
                var bj = BandejaDinero.I; if (bj != null) bj.RetirarPago();
                yield return new WaitForSeconds(2.5f);
                yield return Irse();
                yield break;
            }
            yield return null;
        }

        // 6. pagar
        if (ventaCobrada == null)
        {
            Decir("¿Cuánto es?", 2f, "cuanto");
            yield return new WaitForSeconds(2f);
            caja = g.Caja;
            int total = caja != null ? caja.TotalCent : totalEsperadoCent;
            if (ventaCobrada == null)
            {
                if (QuiereTarjeta)
                {
                    PagoCent = total;
                    Decir("Con tarjeta, por favor.", 9999f, "tarjeta");
                    GestorClientes.Log(name + " paga con TARJETA (total en caja " + CajaRegistradora.Dinero(total) + ")");
                }
                else
                {
                    PagoCent = ElegirPago(total);
                    DecirPago(total);
                    GestorClientes.Log(name + " paga en EFECTIVO con " + CajaRegistradora.Dinero(PagoCent) + " (total en caja " + CajaRegistradora.Dinero(total) + ")");
                }
                YaDijoComoPaga = true;
                totalAlDecir = total;
            }
        }

        // 7. esperar a que cobres
        t0 = Time.time;
        while (ventaCobrada == null)
        {
            // El cajero siguio escaneando (o quito algo) DESPUES de que dije con cuanto
            // pago: si ahora no me alcanza, saco otro billete. Antes la venta se trababa.
            caja = g.Caja;
            if (caja != null && YaDijoComoPaga && caja.TotalCent > 0 && caja.TotalCent != totalAlDecir &&
                (caja.PasoActual == CajaRegistradora.Paso.Escaneando || caja.PasoActual == CajaRegistradora.Paso.Metodo ||
                 (caja.PasoActual == CajaRegistradora.Paso.RecibirEfectivo && caja.RecibidoCent == 0)))
            {
                int nuevo = caja.TotalCent;
                totalAlDecir = nuevo;
                if (QuiereTarjeta) PagoCent = nuevo;
                else if (PagoCent < nuevo)
                {
                    PagoCent = ElegirPago(nuevo);
                    Decir("Ah, ¿" + CajaRegistradora.Dinero(nuevo) + "?\nEntonces toma " + CajaRegistradora.Dinero(PagoCent) + ".", 9999f, "otro_billete");
                    GestorClientes.Log(name + " cambia su pago a " + CajaRegistradora.Dinero(PagoCent) + " (total nuevo " + CajaRegistradora.Dinero(nuevo) + ")");
                    var bandeja = BandejaDinero.I;
                    if (bandeja != null) bandeja.ClienteDeja(PagoCent);
                }
            }
            if (Time.time - t0 > g.Config.paciencia)
            {
                Decir("¿Hola? Ya me voy...", 4f, "me_voy");
                g.RegistrarResultado(false, true);
                if (g.Caja != null) g.Caja.ClienteSeFue();
                var bj = BandejaDinero.I; if (bj != null) bj.RetirarPago();
                yield return new WaitForSeconds(2.5f);
                yield return Irse();
                yield break;
            }
            yield return null;
        }

        // 8. revisar
        var v = ventaCobrada;
        string msg, clave; bool bien = false, queja = true;
        if (QuiereTarjeta && v.metodo != MetodoPago.Tarjeta)
        { msg = "¡Pero yo te dije\nque pagaba con tarjeta!"; clave = "queja_tarjeta"; }
        else if (!QuiereTarjeta && v.metodo == MetodoPago.Tarjeta)
        { msg = "¿Tarjeta? Yo te pagué\nen efectivo..."; clave = "queja_efectivo"; }
        else if (v.metodo == MetodoPago.Efectivo && v.recibidoCent != PagoCent)
        { msg = "¡Oye! Te di " + CajaRegistradora.Dinero(PagoCent) + ",\nno " + CajaRegistradora.Dinero(v.recibidoCent) + "."; clave = "queja_recibido"; }
        else if (v.totalCent > totalEsperadoCent)
        { msg = "Mmm... me cobraste " + CajaRegistradora.Dinero(v.totalCent - totalEsperadoCent) + " de más."; clave = "queja_demas"; }
        else if (v.totalCent < totalEsperadoCent)
        { msg = "Creo que no me cobraste todo...\n(" + CajaRegistradora.Dinero(totalEsperadoCent - v.totalCent) + " menos)"; clave = "queja_menos"; }
        else if (!MismosProductos(v))
        { msg = "El total esta bien, pero eso\nno es lo que llevo..."; clave = "queja_productos"; }
        else
        {
            queja = false;
            bien = v.erroresVuelto == 0;
            if (v.metodo == MetodoPago.Tarjeta) { msg = "¡Gracias! ¡Hasta luego!"; clave = "gracias"; }
            else if (v.erroresVuelto > 0) { msg = "Gracias... aunque te costó\nel vuelto. ¡Hasta luego!"; clave = "gracias_costo"; }
            else if (v.vueltoCent > 0) { msg = "¡Gracias! Vuelto " + CajaRegistradora.Dinero(v.vueltoCent) + ".\n¡Hasta luego!"; clave = "gracias_vuelto"; }
            else { msg = "¡Gracias! ¡Hasta luego!"; clave = "gracias"; }
        }
        Decir(msg, 4f, clave);
        if (g.Caja != null) g.Caja.RegistrarOpinion(v, !queja, msg.Replace("\n", " "));
        g.RegistrarResultado(bien, false);
        GestorClientes.Log(name + ": " + msg.Replace("\n", " ") + " (esperado " + CajaRegistradora.Dinero(totalEsperadoCent) + ")");

        yield return new WaitForSeconds(3f);
        yield return Irse();
    }

    void AlCobrar(CajaRegistradora.Venta v)
    {
        if (!EnCaja || ventaCobrada != null) return;
        // Si cobraste antes de que dijera con cuanto paga, se adapta a lo que registraste.
        if (PagoCent <= 0)
        {
            PagoCent = v.recibidoCent;
            QuiereTarjeta = v.metodo == MetodoPago.Tarjeta;
        }
        ventaCobrada = v;
    }

    IEnumerator Irse()
    {
        if (EnCaja && ventaCobrada != null)
        {
            // Se lleva su vuelto y su boleta.
            var b = BandejaDinero.I; if (b != null) b.ClienteRecoge();
            var imp = ImpresoraBoletas.I; if (imp != null) imp.ClienteSeLlevaBoleta();
        }
        EnCaja = false;
        if (carrito != null && carrito.Carrito != null) carrito.Empujar();   // se va con su carrito
        var caja = g.Caja;
        if (caja != null) caja.VentaCobrada -= AlCobrar;
        g.SalirDeFila(this);

        // Se lleva sus productos.
        foreach (var c in copias) if (c != null) Destroy(c);
        copias.Clear();

        yield return IrA(g.EntradaDentro);
        if (g.EntranDesdeFuera) yield return IrA(g.EntradaFuera);
        Destroy(gameObject);
    }

    // ================================================================== productos

    void Agregar(GameObject original, int cantidad, int maximo)
    {
        var p = BaseDatosProductos.Buscar(original.name);
        if (p == null) return;
        for (int i = 0; i < cantidad && lista.Count < maximo; i++)
        {
            lista.Add(p);
            originales.Add(original);
            if (carrito != null && carrito.Carrito != null) carrito.AgregarVisual(original);
        }
    }

    IEnumerator TomarCarrito()
    {
        var c = CarritoCliente.BuscarLibre(transform.position, 9f);
        if (c == null) yield break;
        var hacia = transform.position - c.position; hacia.y = 0f;
        var junto = g.Muestrear(c.position + (hacia.sqrMagnitude > 0.01f ? hacia.normalized : Vector3.forward) * 0.9f, 1.2f, transform.position);
        yield return IrA(junto);
        yield return MirarHacia(c.position);
        carrito = gameObject.AddComponent<CarritoCliente>();
        if (!carrito.Tomar(c, transform)) { Destroy(carrito); carrito = null; yield break; }
        GestorClientes.Log(name + " toma el carrito " + c.name);
        yield return new WaitForSeconds(0.4f);
    }

    /// <summary>Pone copias de sus productos en la banda de la caja, para que los escanees.
    /// Si trae carrito, los saca uno por uno del cesto.</summary>
    IEnumerator PonerProductos()
    {
        var m = g.Mostrador;
        Vector3 centro = g.ZonaProductos;
        Vector3 der = m.right, adel = m.forward;

        // Altura de la superficie de la caja en ese punto.
        float y = centro.y + 1f;
        if (Physics.Raycast(centro + Vector3.up * 2f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            y = hit.point.y;

        int cols = 3;
        for (int i = 0; i < originales.Count; i++)
        {
            var o = originales[i];
            if (o == null) continue;
            int f = i / cols, c = i % cols;
            Vector3 pos = centro + der * ((c - 1) * 0.14f) + adel * ((f - 0.5f) * 0.16f);
            pos.y = y + 0.12f;

            var visual = carrito != null ? carrito.SacarVisual() : null;
            if (visual != null)
            {
                // Del carrito a la mesa, en un arco (como si lo levantara con la mano).
                var desde = visual.transform.position;
                float k = 0f;
                while (k < 1f)
                {
                    k += Time.deltaTime / 0.5f;
                    var p = Vector3.Lerp(desde, pos, Mathf.SmoothStep(0f, 1f, k));
                    p.y += Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.35f;
                    visual.transform.position = p;
                    yield return null;
                }
                Destroy(visual);
            }
            var copia = Instantiate(o, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), o.transform.parent);
            copia.name = o.name;   // mismo nombre = mismo precio en la base de datos
            ProductoEstante.ComoSuelto(copia);   // visible y suelto (no lo dibuja el bloque)
            copia.transform.localScale = o.transform.localScale;
            var rb = copia.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            copias.Add(copia);
            if (visual != null) yield return new WaitForSeconds(0.15f);
        }
        g.ActualizarLectores();
    }

    bool TodoEscaneado(CajaRegistradora caja)
    {
        var esperado = new Dictionary<Producto, int>();
        foreach (var p in lista) esperado[p] = esperado.TryGetValue(p, out var n) ? n + 1 : 1;

        if (caja.Lineas.Count != esperado.Count) return false;
        foreach (var l in caja.Lineas)
            if (!esperado.TryGetValue(l.producto, out var n) || n != l.cantidad) return false;
        return true;
    }

    /// <summary>¿Le cobraron exactamente lo que lleva? (mismo total pero otro producto = error)</summary>
    bool MismosProductos(CajaRegistradora.Venta v)
    {
        var esperado = new Dictionary<Producto, int>();
        foreach (var p in lista) esperado[p] = esperado.TryGetValue(p, out var n) ? n + 1 : 1;
        int lineas = 0;
        foreach (var l in v.lineas)
        {
            lineas++;
            if (!esperado.TryGetValue(l.producto, out var n) || n != l.cantidad) return false;
        }
        return lineas == esperado.Count;
    }

    void DecirPago(int total)
    {
        string monto = CajaRegistradora.Dinero(PagoCent);
        string clave = PagoCent == total ? "efectivo_exacto" : "efectivo_" + (PagoCent / 100);
        Decir("En efectivo.\nAquí tienes: " + monto, 9999f, clave);
        GestorClientes.Log(name + " paga en EFECTIVO con " + monto + " (total en caja " + CajaRegistradora.Dinero(total) + ")");
        var bandeja = BandejaDinero.I;
        if (bandeja != null) bandeja.ClienteDeja(PagoCent);
    }

    int ElegirPago(int total)
    {
        if (total <= 0) return 0;
        if (Random.value < g.Config.probPagoExacto) return total;

        int[] billetes = { 1000, 2000, 5000, 10000, 20000 };
        int i = Array.FindIndex(billetes, b => b >= total);
        if (i < 0) return ((total / 20000) + 1) * 20000;
        // A veces paga con un billete mas grande (como en la vida real).
        if (i + 1 < billetes.Length && Random.value < 0.3f) i++;
        return billetes[i];
    }

    string Saludo(out string clave)
    {
        bool noche = gameObject.scene.name.Contains("Night");
        int h = DateTime.Now.Hour;
        string s;
        if (noche || h >= 19) { s = "¡Buenas noches!"; clave = "saludo_noche"; }
        else if (h < 12) { s = "¡Buenos días!"; clave = "saludo_dia"; }
        else { s = "¡Buenas tardes!"; clave = "saludo_tarde"; }
        return s + "\nSolo esto, por favor.";
    }

    // ================================================================== movimiento

    IEnumerator IrA(Vector3 destino, Func<bool> cancelar = null)
    {
        if (agente == null || !agente.isOnNavMesh) { transform.position = destino; yield break; }
        agente.isStopped = false;
        agente.updateRotation = true;
        agente.SetDestination(destino);

        float t0 = Time.time;
        Vector3 ultimaPos = transform.position;
        float ultimoControl = Time.time;
        float quieto = 0f;
        while (true)
        {
            if (cancelar != null && cancelar()) yield break;
            if (!agente.pathPending && agente.remainingDistance <= agente.stoppingDistance + 0.05f) break;

            // Atascado: cada 1.5 s mira cuanto avanzo (no por frame: a muchos FPS
            // cada frame avanza muy poco y parecia quieto).
            if (Time.time - ultimoControl >= 1.5f)
            {
                quieto = (transform.position - ultimaPos).sqrMagnitude < 0.04f ? quieto + (Time.time - ultimoControl) : 0f;
                ultimaPos = transform.position;
                ultimoControl = Time.time;
            }
            if (quieto >= 6f || Time.time - t0 > 90f)
            {
                GestorClientes.Log(name + " atascado en " + transform.position + " -> salto a " + destino);
                agente.Warp(destino);
                break;
            }
            yield return null;
        }
        agente.ResetPath();
    }

    IEnumerator MirarHacia(Vector3 punto)
    {
        var dir = punto - transform.position; dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) yield break;
        agente.updateRotation = false;
        var obj = Quaternion.LookRotation(dir.normalized);
        float t = 0;
        var ini = transform.rotation;
        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            transform.rotation = Quaternion.Slerp(ini, obj, t);
            yield return null;
        }
    }

    // ================================================================== animacion + burbuja

    void Update()
    {
        if (anim == null) return;
        string quiero;
        float vel = agente != null ? agente.velocity.magnitude : 0f;
        if (vel > 0.12f) quiero = "Walking";
        else if (hablando) quiero = "Talking";
        else if (mirando) quiero = "Looking";
        else quiero = "Idle";
        if (quiero != animActual)
        {
            anim.CrossFadeInFixedTime(quiero, 0.25f);
            animActual = quiero;
        }
        // Mientras habla quieto, mira al jugador (como una persona de verdad).
        if (hablando && vel < 0.1f && Camera.main != null && agente != null && !agente.hasPath)
        {
            var d = Camera.main.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f && d.sqrMagnitude < 16f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), Time.deltaTime * 3f);
        }

        // La animacion avanza al ritmo real del paso (si no, los pies "patinan").
        anim.speed = quiero == "Walking" ? Mathf.Clamp(vel / g.Config.velocidadAnimacion, 0.6f, 1.4f) : 1f;
    }

    void LateUpdate()
    {
        if (burbuja == null) return;
        bool visible = Time.time < burbujaHasta;
        if (burbuja.gameObject.activeSelf != visible) burbuja.gameObject.SetActive(visible);
        hablando = Time.time < hablarHasta;
        if (!visible) return;
        burbuja.position = transform.position + Vector3.up * (altura + 0.35f);
        var cam = Camera.main;
        if (cam != null)
        {
            var d = burbuja.position - cam.transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.001f) burbuja.rotation = Quaternion.LookRotation(d.normalized);
        }
    }

    /// <summary>El cliente acerca su tarjeta al POS.</summary>
    public void AcercarTarjeta()
    {
        Decir("Aquí está mi tarjeta.", 3f, "acerca_tarjeta");
        var pos = POS3D.I;
        if (pos != null) pos.AnimarTarjeta(transform.position + Vector3.up * (altura * 0.6f) + transform.forward * 0.35f);
    }

    /// <summary>El jugador le hablo (VozJugador): responde algo corto con su voz.</summary>
    public void Responder(VozJugador.Intencion i)
    {
        string clave;
        switch (i)
        {
            case VozJugador.Intencion.Saludo: clave = "hola"; break;
            case VozJugador.Intencion.ComoPaga: clave = QuiereTarjeta ? "pago_tarjeta" : "pago_efectivo"; break;
            case VozJugador.Intencion.AlgoMas: clave = "eso_es_todo"; break;
            case VozJugador.Intencion.Sencillo: clave = "sin_sencillo"; break;
            case VozJugador.Intencion.Bolsa: clave = "si_bolsa"; break;
            case VozJugador.Intencion.Boleta: clave = "boleta"; break;
            case VozJugador.Intencion.Gracias: clave = "a_ti"; break;
            case VozJugador.Intencion.Despedida: clave = "igualmente"; break;
            case VozJugador.Intencion.Espere: clave = "espera"; break;
            default: clave = "no_entendi"; break;
        }
        // Si ya habia dicho con cuanto paga, lo vuelve a mostrar despues de responder.
        string antes = txtBurbuja != null && burbujaHasta > Time.time + 60f ? txtBurbuja.text : null;
        Decir(VocesClientes.Frases.TryGetValue(clave, out var t) ? t : "...", 3f, clave);
        if (antes != null) StartCoroutine(VolverADecir(antes, 3f));
    }

    IEnumerator VolverADecir(string texto, float espera)
    {
        yield return new WaitForSeconds(espera);
        if (ventaCobrada == null && txtBurbuja != null) { txtBurbuja.text = texto; burbujaHasta = Time.time + 9999f; }
    }

    public void Decir(string texto, float segundos) { Decir(texto, segundos, null); }

    /// <summary>Muestra la burbuja y, si hay audio grabado para esa frase, la dice en voz alta.</summary>
    public void Decir(string texto, float segundos, string claveVoz)
    {
        if (txtBurbuja == null) return;
        txtBurbuja.text = texto;
        burbujaHasta = Time.time + segundos;
        float habla = 2.5f;
        var clip = VocesClientes.Clip(claveVoz);
        if (clip != null && voz != null)
        {
            voz.Stop();
            voz.clip = clip;
            voz.Play();
            habla = clip.length / Mathf.Max(0.5f, voz.pitch);
        }
        hablarHasta = Time.time + Mathf.Min(segundos, habla);   // gesticula mientras habla
    }

    void CrearVoz()
    {
        string n = name.ToLowerInvariant();
        esMujer = n.Contains("sophie") || n.Contains("suzie") || n.Contains("girl") || n.Contains("woman");
        var go = new GameObject("Voz");
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position + Vector3.up * (altura * 0.92f);
        voz = go.AddComponent<AudioSource>();
        voz.playOnAwake = false;
        voz.spatialBlend = 1f;                 // sale de la boca del cliente
        voz.rolloffMode = AudioRolloffMode.Linear;
        voz.minDistance = 1.2f;
        voz.maxDistance = 14f;
        voz.volume = 0.95f;
        // Las voces del sistema suelen ser de mujer: a los hombres se les baja el tono.
        voz.pitch = esMujer ? Random.Range(1.0f, 1.08f) : (VocesClientes.VozEsDeMujer ? Random.Range(0.78f, 0.84f) : Random.Range(0.95f, 1.03f));
    }

    void CrearBurbuja()
    {
        var go = new GameObject("Burbuja", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(560, 150);
        float escalaPadre = Mathf.Max(0.0001f, transform.lossyScale.x);
        rt.localScale = Vector3.one * (0.0016f / escalaPadre);
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        fondo.transform.SetParent(go.transform, false);
        var img = fondo.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.93f);
        img.raycastTarget = false;
        var frt = (RectTransform)fondo.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;

        var tgo = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(go.transform, false);
        txtBurbuja = tgo.GetComponent<Text>();
        txtBurbuja.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txtBurbuja.fontSize = 40;
        txtBurbuja.fontStyle = FontStyle.Bold;
        txtBurbuja.alignment = TextAnchor.MiddleCenter;
        txtBurbuja.color = new Color(0.1f, 0.1f, 0.15f);
        txtBurbuja.raycastTarget = false;
        txtBurbuja.horizontalOverflow = HorizontalWrapMode.Wrap;
        txtBurbuja.verticalOverflow = VerticalWrapMode.Overflow;
        var trt = txtBurbuja.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16, 8); trt.offsetMax = new Vector2(-16, -8);

        burbuja = go.transform;
        go.SetActive(false);
    }
}
