using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Como paga el cliente.</summary>
public enum MetodoPago { Efectivo, Tarjeta }

/// <summary>
/// CAJA REGISTRADORA (punto de venta del minimarket)
/// ------------------------------------------------
/// Pantalla en VR que se pulsa con el rayo del mando. La venta va por PASOS, y la
/// pantalla siempre dice arriba que hay que hacer:
///
///   PASO 1  ESCANEAR    Pasa la pistola por cada producto (cada bip suma 1).
///                       Corrige con -1 / +1 / Quitar. Luego pulsa COBRAR.
///   PASO 2  FORMA DE PAGO  EFECTIVO  o  TARJETA (POS).
///   PASO 3a EFECTIVO    Marca lo que te dio el cliente (billetes y monedas) -> CONFIRMAR.
///   PASO 4  VUELTO      Marca los billetes/monedas que le devuelves -> ENTREGAR VUELTO.
///                       La caja revisa que el vuelto sea exacto.
///   PASO 3b TARJETA     ENVIAR AL POS -> el cliente acerca la tarjeta -> APROBADO.
///   FIN                 "Siguiente cliente".
///
/// Todo el dinero va en CENTIMOS (int) para que no haya errores de redondeo.
/// Se instala sola en MinimarketXR_Full y MinimarketXR_Night. La venta en curso y las
/// ventas del turno sobreviven al cambio dia/noche (variables estaticas).
/// </summary>
public class CajaRegistradora : MonoBehaviour
{
    // ================================================================== datos

    [Serializable]
    public class LineaVenta
    {
        public Producto producto;
        public int cantidad;
        public int PrecioCent => ACentimos(producto.precio);
        public int SubtotalCent => PrecioCent * cantidad;
    }

    [Serializable]
    public class Venta
    {
        public int numero;
        public MetodoPago metodo;
        public int totalCent;
        public int recibidoCent;
        public int vueltoCent;
        public int erroresVuelto;
        public int articulos;
        public string hora;
        public float segundos;        // desde el primer producto hasta cobrar
        public bool conforme = true;  // el cliente no reclamo
        public string queja = "";
        public List<LineaVenta> lineas = new List<LineaVenta>();
    }

    /// <summary>Compatibilidad: Vendiendo = cualquier paso antes de terminar.</summary>
    public enum Estado { Vendiendo, Pagada }

    public enum Paso { Escaneando, Metodo, RecibirEfectivo, EntregarVuelto, Tarjeta, Completada }

    enum EstadoPOS { Listo, Esperando, Procesando, Aprobado, Rechazado }

    // ================================================================== inspector

    [Header("Lector")]
    [Tooltip("Vacio = usa todos los LectorCodigos de la escena.")]
    public LectorCodigos[] lectores;

    [Header("Reglas")]
    [Tooltip("Si esta activo, el MISMO objeto no se puede cobrar dos veces. " +
             "Desactivado (normal en un minimarket): volver a escanear suma otra unidad.")]
    public bool noCobrarDosVeces = false;

    [Header("Pantalla")]
    public string nombreCaja = "CAJA 1";
    [Tooltip("Donde poner la pantalla. Vacio = sobre el monitor del mostrador mas cercano al lector.")]
    public Transform anclaPantalla;
    [Tooltip("Posicion RELATIVA al mostrador (o al ancla).")]
    public Vector3 desplazamientoPantalla = new Vector3(-0.30f, 1.55f, -1.05f);
    [Tooltip("Donde esta parado el cajero, relativo al mostrador: la pantalla mira hacia ahi.")]
    public Vector3 puntoCajeroLocal = new Vector3(-0.35f, 1.6f, 0.45f);
    [Tooltip("Tamano: metros por pixel. 0.00085 = la pantalla mide ~0.95 m de ancho.")]
    public float escalaPantalla = 0.00085f;
    [Tooltip("Si esta activo la pantalla gira siguiendo al jugador. Apagado es mas facil de pulsar.")]
    public bool mirarAlJugador = false;

    [Header("Aviso junto al lector")]
    public bool mostrarAvisoEnLector = true;
    public float duracionAviso = 2.0f;

    // ================================================================== estado publico

    public event Action VentaActualizada;
    public event Action<Venta> VentaCobrada;

    public IReadOnlyList<LineaVenta> Lineas => lineas;
    public Paso PasoActual => paso;
    public Estado EstadoActual => paso == Paso.Completada ? Estado.Pagada : Estado.Vendiendo;
    public int TotalCent { get; private set; }
    public int RecibidoCent => recibido;
    public int VueltoCent => Mathf.Max(0, recibido - TotalCent);
    public int FaltaCent => Mathf.Max(0, TotalCent - recibido);
    public int EntregadoCent => entregado;
    public int CantidadArticulos { get; private set; }
    public float Total => TotalCent / 100f;
    public IReadOnlyList<Venta> VentasDelTurno => ventas;
    public Venta UltimaVenta => ventas.Count > 0 ? ventas[ventas.Count - 1] : null;
    public int Abandonos => abandonos;
    /// <summary>El mostrador donde esta esta caja (Pref_CashCounter).</summary>
    public Transform Mostrador { get; private set; }

    /// <summary>Lo que muestra la pantallita del POS 3D.</summary>
    public string TextoPOS
    {
        get
        {
            if (paso == Paso.Tarjeta)
                return pos == EstadoPOS.Listo ? "MONTO\n" + Dinero(TotalCent) : posMensaje.Replace("\n", " ");
            var u = UltimaVenta;
            if (paso == Paso.Completada && u != null && u.metodo == MetodoPago.Tarjeta)
                return "APROBADO\n" + Dinero(u.totalCent);
            return "CAJERO XR\nListo";
        }
    }

    // ================================================================== privado (estatico)

    static readonly List<LineaVenta> lineas = new List<LineaVenta>();
    static readonly List<Venta> ventas = new List<Venta>();
    static Paso paso = Paso.Escaneando;
    static MetodoPago metodo = MetodoPago.Efectivo;
    static int recibido;      // lo que dio el cliente
    static int entregado;     // el vuelto que va devolviendo el cajero
    static int erroresVuelto;
    static int seleccion = -1;
    static float inicioVenta = -1f;   // Time.time del primer producto de la venta
    static int abandonos;             // clientes que se fueron sin pagar
    static bool viendoResumen;

    GameObject ultimoObjeto;          // para no sumar dos veces el mismo bip
    float ultimoObjetoHora = -10f;

    readonly HashSet<GameObject> objetosCobrados = new HashSet<GameObject>();
    int primeraFila = 0;
    EstadoPOS pos = EstadoPOS.Listo;
    string posMensaje = "";
    Coroutine rutinaPOS;

    public static readonly int[] Denominaciones = { 20000, 10000, 5000, 2000, 1000, 500, 200, 100, 50, 20, 10 };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarEstatico()
    {
        lineas.Clear(); ventas.Clear();
        paso = Paso.Escaneando; metodo = MetodoPago.Efectivo;
        recibido = 0; entregado = 0; erroresVuelto = 0; seleccion = -1;
        inicioVenta = -1f; abandonos = 0; viendoResumen = false;
        instalado = false;
    }

    // ------------------------------------------------------------------ auto-instalacion
    static bool instalado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        if (!instalado)
        {
            SceneManager.sceneLoaded += (esc, modo) => AsegurarEnEscena(esc);
            instalado = true;
        }
        AsegurarEnEscena(SceneManager.GetActiveScene());
    }

    static void AsegurarEnEscena(Scene esc)
    {
        if (!esc.IsValid() || !esc.name.StartsWith("MinimarketXR")) return;
        if (FindAnyObjectByType<CajaRegistradora>() != null) return;
        var go = new GameObject("Caja Registradora");
        SceneManager.MoveGameObjectToScene(go, esc);
        go.AddComponent<CajaRegistradora>();
        Debug.Log("[Caja] Instalada en la escena '" + esc.name + "'.");
    }

    // ================================================================== UI refs

    const float W = 1100f, H = 700f;
    const int FilasVisibles = 8;
    Transform pantalla;
    Canvas canvasPantalla;
    Text txtTurno, txtPaso, txtMensaje;
    readonly List<Button> filas = new List<Button>();
    readonly List<Text[]> filasTxt = new List<Text[]>();
    readonly List<Button> botonesEdicion = new List<Button>();

    // paneles de la derecha
    GameObject pEscanear, pMetodo, pRecibir, pVuelto, pTarjeta, pFin;
    Text eTotal, eArticulos;
    Text mTotal;
    Text rTotal, rRecibido, rEstado; Button rConfirmar;
    Text vAEntregar, vEntregando, vEstado; Button vConfirmar;
    Text tMonto, tEstado; Button tEnviar;
    Text fTitulo, fDetalle, fVuelto;
    GameObject pResumen;
    Text rsNota, rsCalificacion, rsConsejo;
    readonly List<Text> rsValores = new List<Text>();

    Transform aviso;
    Text txtAviso;
    float avisoHasta;
    Transform lectorAviso;
    Transform cam;

    AudioSource sfx;
    AudioClip cClick, cCobro, cError, cPos, cMoneda, cBillete, cCajon, cImpresora;

    // colores
    static readonly Color CFondo = new Color(0.07f, 0.09f, 0.13f, 0.97f);
    static readonly Color CPanel = new Color(0.12f, 0.15f, 0.20f, 1f);
    static readonly Color CCabecera = new Color(0.10f, 0.55f, 0.35f, 1f);
    static readonly Color CFila = new Color(0.15f, 0.18f, 0.24f, 1f);
    static readonly Color CFilaSel = new Color(0.20f, 0.45f, 0.75f, 1f);
    static readonly Color CBillete = new Color(0.20f, 0.50f, 0.32f, 1f);
    static readonly Color CMoneda = new Color(0.62f, 0.50f, 0.18f, 1f);
    static readonly Color CBoton = new Color(0.25f, 0.30f, 0.38f, 1f);
    static readonly Color CPeligro = new Color(0.70f, 0.22f, 0.22f, 1f);
    static readonly Color CVerde = new Color(0.10f, 0.62f, 0.30f, 1f);
    static readonly Color CAzul = new Color(0.15f, 0.40f, 0.80f, 1f);
    static readonly Color COk = new Color(0.45f, 1f, 0.6f, 1f);
    static readonly Color CError = new Color(1f, 0.45f, 0.4f, 1f);
    static readonly Color CAviso = new Color(1f, 0.85f, 0.3f, 1f);
    static readonly Color CTexto = new Color(0.92f, 0.94f, 0.97f, 1f);
    static readonly Color CTextoSuave = new Color(0.62f, 0.68f, 0.76f, 1f);

    // ================================================================== ciclo

    void Start()
    {
        if (lectores == null || lectores.Length == 0)
            lectores = FindObjectsByType<LectorCodigos>(FindObjectsInactive.Include);
        foreach (var l in lectores)
            if (l != null) l.ProductoEscaneado += AlEscanear;
        if (lectores.Length == 0)
            Debug.LogWarning("[Caja] No hay ningun LectorCodigos en la escena.");

        int n = BaseDatosProductos.Todos.Count;

        CrearSonidos();
        AsegurarEventSystem();
        CrearPantalla();
        if (mostrarAvisoEnLector) CrearAviso();
        if (Mostrador != null && Mostrador.name.StartsWith("Pref_CashCounter")) ObjetosCaja.Instalar(this, Mostrador);

        // Si venimos del cambio dia/noche, se sigue con la venta que habia.
        if (paso == Paso.Tarjeta) paso = Paso.Metodo;   // el POS no sobrevive al cambio
        Recalcular();
        Seleccionar(seleccion, false);
        Mensaje(lineas.Count > 0 && paso != Paso.Completada
            ? "Venta en curso: " + CantidadArticulos + " articulo(s)"
            : "Escanea el primer producto", CAviso);
        Refrescar();

        Debug.Log("[Caja] Lista. Lectores=" + lectores.Length + " productos en base de datos=" + n);
    }

    void OnDestroy()
    {
        if (lectores == null) return;
        foreach (var l in lectores)
            if (l != null) l.ProductoEscaneado -= AlEscanear;
    }

    void LateUpdate()
    {
        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.transform;
            if (canvasPantalla != null) canvasPantalla.worldCamera = Camera.main;
        }
        if (mirarAlJugador && pantalla != null) MirarA(pantalla, cam, true);

        if (aviso != null)
        {
            bool visible = Time.time < avisoHasta && lectorAviso != null;
            if (aviso.gameObject.activeSelf != visible) aviso.gameObject.SetActive(visible);
            if (visible)
            {
                aviso.position = lectorAviso.position + Vector3.up * 0.18f;
                MirarA(aviso, cam, false);
            }
        }
    }

    // ================================================================== PASO 1: escanear

    void AlEscanear(GameObject objeto, Producto p)
    {
        if (objeto == null) return;
        lectorAviso = BuscarLectorMasCercano(objeto.transform.position);

        if (p == null)
        {
            Debug.LogWarning("[Caja] '" + objeto.name + "' no esta en la base de datos.");
            Mensaje("SIN PRECIO: " + BaseDatosProductos.Normalizar(objeto.name), CError, true);
            Sonar(cError);
            return;
        }
        // El mismo objeto leido otra vez en menos de 0.8 s es el mismo bip repetido
        // (gatillo rebotando o la pistola quieta sobre el producto), no otra unidad.
        if (objeto == ultimoObjeto && Time.time - ultimoObjetoHora < 0.8f)
        {
            ultimoObjetoHora = Time.time;
            return;
        }
        ultimoObjeto = objeto;
        ultimoObjetoHora = Time.time;

        if (noCobrarDosVeces && objetosCobrados.Contains(objeto))
        {
            Mensaje("Ya escaneado: " + p.nombre, CAviso, true);
            Sonar(cError);
            return;
        }
        objetosCobrados.Add(objeto);
        AgregarProducto(p, 1);
    }

    /// <summary>Agrega unidades de un producto a la venta.</summary>
    public void AgregarProducto(Producto p, int cantidad = 1)
    {
        if (p == null || cantidad <= 0) return;

        if (paso == Paso.Completada) NuevaVenta();
        else if (paso != Paso.Escaneando)
        {
            // Escaneo durante el cobro: se vuelve al paso 1 (el pago se reinicia).
            CancelarPOS();
            recibido = 0; entregado = 0;
            paso = Paso.Escaneando;
        }

        if (lineas.Count == 0 || inicioVenta < 0f) inicioVenta = Time.time;
        if (p.stock <= 0) Debug.LogWarning("[Caja] " + p.nombre + " figura SIN STOCK en la base de datos.");

        int idx = lineas.FindIndex(x => x.producto == p);
        if (idx < 0)
        {
            lineas.Add(new LineaVenta { producto = p, cantidad = 0 });
            idx = lineas.Count - 1;
        }
        lineas[idx].cantidad += cantidad;
        Seleccionar(idx, false);

        Recalcular();
        var l = lineas[idx];
        Mensaje("+ " + p.nombre + "   " + Dinero(l.PrecioCent) +
                (l.cantidad > 1 ? "   (x" + l.cantidad + ")" : ""), COk, true);
        Refrescar();
        Debug.Log("[Caja] + " + p.nombre + " " + Dinero(l.PrecioCent) + " x" + l.cantidad +
                  " | TOTAL " + Dinero(TotalCent) + " (" + CantidadArticulos + " art.)");
    }

    public void Seleccionar(int idx) { Seleccionar(idx, true); }

    void Seleccionar(int idx, bool conSonido)
    {
        if (lineas.Count == 0) { seleccion = -1; primeraFila = 0; return; }
        seleccion = Mathf.Clamp(idx, 0, lineas.Count - 1);
        if (seleccion < primeraFila) primeraFila = seleccion;
        if (seleccion >= primeraFila + FilasVisibles) primeraFila = seleccion - FilasVisibles + 1;
        if (conSonido) { Sonar(cClick); Refrescar(); }
    }

    public void SubirSeleccion() { if (lineas.Count > 0) Seleccionar(seleccion <= 0 ? 0 : seleccion - 1); }
    public void BajarSeleccion() { if (lineas.Count > 0) Seleccionar(seleccion + 1); }

    public void CambiarCantidad(int delta)
    {
        if (!EnPaso1() || seleccion < 0 || seleccion >= lineas.Count) return;
        var l = lineas[seleccion];
        l.cantidad += delta;
        if (l.cantidad <= 0)
        {
            lineas.RemoveAt(seleccion);
            Mensaje("Quitado: " + l.producto.nombre, CAviso);
            Seleccionar(Mathf.Min(seleccion, lineas.Count - 1), false);
        }
        else Mensaje(l.producto.nombre + "  ->  x" + l.cantidad, CTexto);
        Sonar(cClick);
        Recalcular();
        Refrescar();
    }

    public void QuitarLinea()
    {
        if (!EnPaso1() || seleccion < 0 || seleccion >= lineas.Count) return;
        var l = lineas[seleccion];
        lineas.RemoveAt(seleccion);
        Seleccionar(Mathf.Min(seleccion, lineas.Count - 1), false);
        Mensaje("Quitado: " + l.cantidad + " x " + l.producto.nombre, CAviso);
        Sonar(cClick);
        Recalcular();
        Refrescar();
    }

    /// <summary>Borra todo lo escaneado (el cliente se arrepintio).</summary>
    public void CancelarVenta()
    {
        if (paso == Paso.Completada) { NuevaVenta(); return; }
        CancelarPOS();
        lineas.Clear();
        objetosCobrados.Clear();
        recibido = 0; entregado = 0; erroresVuelto = 0;
        seleccion = -1; primeraFila = 0;
        inicioVenta = -1f;
        paso = Paso.Escaneando;
        Recalcular();
        Mensaje("Venta cancelada", CAviso);
        Sonar(cClick);
        Refrescar();
    }

    bool EnPaso1()
    {
        if (paso == Paso.Escaneando) return true;
        Mensaje(paso == Paso.Completada ? "Venta terminada. Pulsa 'Siguiente cliente'." : "Para corregir, pulsa 'Volver'.", CAviso);
        Sonar(cError);
        Refrescar();
        return false;
    }

    // ================================================================== PASO 2: forma de pago

    /// <summary>Boton COBRAR del paso 1.</summary>
    public void IrACobrar()
    {
        if (paso != Paso.Escaneando) return;
        if (TotalCent <= 0) { Error("Primero escanea los productos."); return; }
        paso = Paso.Metodo;
        Sonar(cClick);
        Mensaje("Total " + Dinero(TotalCent) + ". Pregunta al cliente como paga.", CTexto);
        Refrescar();
    }

    public void ElegirEfectivo()
    {
        if (paso != Paso.Metodo) return;
        metodo = MetodoPago.Efectivo;
        recibido = 0; entregado = 0;
        paso = Paso.RecibirEfectivo;
        Sonar(cClick);
        Mensaje("Efectivo: marca lo que te dio el cliente.", CTexto);
        Refrescar();
    }

    public void ElegirTarjeta()
    {
        if (paso != Paso.Metodo) return;
        metodo = MetodoPago.Tarjeta;
        recibido = 0; entregado = 0;
        pos = EstadoPOS.Listo; posMensaje = "";
        paso = Paso.Tarjeta;
        Sonar(cClick);
        Mensaje("Tarjeta: pulsa ENVIAR AL POS.", CTexto);
        Refrescar();
    }

    /// <summary>Boton "Volver" de los pasos de pago.</summary>
    public void Volver()
    {
        CancelarPOS();
        switch (paso)
        {
            case Paso.Metodo: paso = Paso.Escaneando; break;
            case Paso.RecibirEfectivo:
            case Paso.Tarjeta: paso = Paso.Metodo; recibido = 0; break;
            case Paso.EntregarVuelto: paso = Paso.RecibirEfectivo; entregado = 0; break;
            default: return;
        }
        Sonar(cClick);
        Mensaje("", CTexto);
        Refrescar();
    }

    // ================================================================== PASO 3a: recibir efectivo

    /// <summary>Billete/moneda: en el paso 3 suma a lo RECIBIDO; en el paso 4 suma al VUELTO.</summary>
    public void PulsarDenominacion(int centimos)
    {
        if (paso != Paso.RecibirEfectivo && paso != Paso.EntregarVuelto) return;
        Sonar(centimos >= 1000 ? cBillete : cMoneda);
        if (paso == Paso.RecibirEfectivo) ClienteEntrega(centimos);
        else EntregarDenominacion(centimos);
    }

    /// <summary>El cliente entrega un billete/moneda (en centimos).</summary>
    public void ClienteEntrega(int centimos)
    {
        // Atajo (pruebas / clientes): si aun no se eligio, se asume efectivo.
        if (paso == Paso.Escaneando && TotalCent > 0) paso = Paso.Metodo;
        if (paso == Paso.Metodo) { metodo = MetodoPago.Efectivo; recibido = 0; paso = Paso.RecibirEfectivo; }
        if (paso != Paso.RecibirEfectivo) return;

        recibido += Mathf.Max(0, centimos);
        Sonar(cClick);
        Mensaje("Recibido " + Dinero(centimos), CTexto);
        Refrescar();
    }

    public void PagoExacto()
    {
        if (paso != Paso.RecibirEfectivo) return;
        recibido = TotalCent;
        Sonar(cClick);
        Mensaje("Pago exacto", CTexto);
        Refrescar();
    }

    public void BorrarPago()
    {
        if (paso == Paso.RecibirEfectivo) recibido = 0;
        else if (paso == Paso.EntregarVuelto) entregado = 0;
        else return;
        Sonar(cClick);
        Mensaje("Borrado", CTexto);
        Refrescar();
    }

    /// <summary>CONFIRMAR del paso 3: si hay vuelto pasa al paso 4.</summary>
    public void ConfirmarRecibido()
    {
        if (paso != Paso.RecibirEfectivo) return;
        if (recibido < TotalCent)
        {
            Error("Falta " + Dinero(FaltaCent) + ". Marca TODO lo que te dio el cliente.");
            return;
        }
        if (VueltoCent == 0) { Finalizar(); return; }
        entregado = 0;
        paso = Paso.EntregarVuelto;
        Sonar(cClick);
        Mensaje("Ahora devuelve el vuelto: " + Dinero(VueltoCent), CAviso);
        Refrescar();
    }

    // ================================================================== PASO 4: entregar vuelto

    public void EntregarDenominacion(int centimos)
    {
        if (paso != Paso.EntregarVuelto) return;
        entregado += Mathf.Max(0, centimos);
        Sonar(cClick);
        Mensaje("Entregas " + Dinero(centimos), CTexto);
        Refrescar();
    }

    public void AyudaVuelto()
    {
        if (paso != Paso.EntregarVuelto) return;
        Sonar(cClick);
        Mensaje("Sugerencia: " + DesgloseVuelto(VueltoCent), CAviso);
        Refrescar();
    }

    /// <summary>ENTREGAR VUELTO: la caja revisa que sea exacto.</summary>
    public void ConfirmarVuelto()
    {
        if (paso != Paso.EntregarVuelto) return;
        if (entregado != VueltoCent)
        {
            erroresVuelto++;
            Error(entregado < VueltoCent
                ? "Vuelto incompleto: faltan " + Dinero(VueltoCent - entregado) + "."
                : "Te pasaste por " + Dinero(entregado - VueltoCent) + ". Pulsa Borrar y vuelve a contar.");
            return;
        }
        Finalizar();
    }

    // ================================================================== PASO 3b: tarjeta (POS)

    public void EnviarPOS()
    {
        if (paso != Paso.Tarjeta || pos == EstadoPOS.Esperando || pos == EstadoPOS.Procesando) return;
        Sonar(cClick);
        rutinaPOS = StartCoroutine(RutinaPOS());
    }

    IEnumerator RutinaPOS()
    {
        pos = EstadoPOS.Esperando;
        posMensaje = "Acerque o inserte\nla tarjeta...";
        Refrescar();
        yield return new WaitForSeconds(1.2f);

        // Si hay un cliente en la caja, el decide.
        var cliente = GestorClientes.I != null ? GestorClientes.I.ClienteEnCaja : null;
        if (cliente != null && cliente.EnCaja)
        {
            if (!cliente.QuiereTarjeta)
            {
                cliente.Decir("No, yo pago en efectivo.", 3f);
                pos = EstadoPOS.Rechazado;
                posMensaje = "Cancelado:\nel cliente paga en EFECTIVO";
                Sonar(cError);
                Refrescar();
                rutinaPOS = null;
                yield break;
            }
            cliente.AcercarTarjeta();
        }
        yield return new WaitForSeconds(1.3f);

        pos = EstadoPOS.Procesando;
        posMensaje = "Procesando...";
        Sonar(cPos);
        Refrescar();
        yield return new WaitForSeconds(1.5f);

        pos = EstadoPOS.Aprobado;
        posMensaje = "APROBADO";
        recibido = TotalCent;
        Haptica.Ambos(0.25f, 0.08f);
        Sonar(cCobro);
        Refrescar();
        yield return new WaitForSeconds(1.0f);

        rutinaPOS = null;
        Finalizar();
    }

    void CancelarPOS()
    {
        if (rutinaPOS != null) StopCoroutine(rutinaPOS);
        rutinaPOS = null;
        pos = EstadoPOS.Listo;
        posMensaje = "";
    }

    // ================================================================== fin

    void Finalizar()
    {
        var v = new Venta
        {
            numero = ventas.Count + 1,
            metodo = metodo,
            totalCent = TotalCent,
            recibidoCent = metodo == MetodoPago.Tarjeta ? TotalCent : recibido,
            vueltoCent = metodo == MetodoPago.Tarjeta ? 0 : VueltoCent,
            erroresVuelto = erroresVuelto,
            articulos = CantidadArticulos,
            hora = DateTime.Now.ToString("HH:mm:ss"),
            segundos = inicioVenta >= 0f ? Time.time - inicioVenta : 0f,
        };
        foreach (var l in lineas)
        {
            v.lineas.Add(new LineaVenta { producto = l.producto, cantidad = l.cantidad });
            // Lo vendido sale del inventario.
            l.producto.stock = Mathf.Max(0, l.producto.stock - l.cantidad);
            if (l.producto.stock == 0) Debug.Log("[Caja] Se acabo el stock de " + l.producto.nombre + ".");
        }
        ventas.Add(v);
        inicioVenta = -1f;

        paso = Paso.Completada;
        Sonar(metodo == MetodoPago.Efectivo ? cCajon : cImpresora);
        Haptica.Ambos(0.3f, 0.12f);
        Sonar(cCobro);
        Mensaje("Venta #" + v.numero + " cobrada", COk);
        Refrescar();

        Debug.Log("[Caja] VENTA #" + v.numero + " | " + v.metodo + " | total " + Dinero(v.totalCent) +
                  " | recibido " + Dinero(v.recibidoCent) + " | vuelto " + Dinero(v.vueltoCent) +
                  " | errores de vuelto " + v.erroresVuelto);
        VentaCobrada?.Invoke(v);
    }

    /// <summary>Limpia la pantalla para el siguiente cliente.</summary>
    [ContextMenu("Nueva venta")]
    public void NuevaVenta()
    {
        CancelarPOS();
        lineas.Clear();
        objetosCobrados.Clear();
        recibido = 0; entregado = 0; erroresVuelto = 0;
        seleccion = -1; primeraFila = 0;
        inicioVenta = -1f;
        paso = Paso.Escaneando;
        Recalcular();
        Mensaje("Nueva venta. Escanea los productos.", CAviso);
        Refrescar();
    }

    /// <summary>
    /// El cliente de la caja se fue sin pagar (se canso de esperar). Lo que estaba
    /// escaneado era SUYO: se anula, asi el siguiente cliente no paga sus productos.
    /// </summary>
    public void ClienteSeFue()
    {
        abandonos++;
        if (paso != Paso.Completada && lineas.Count > 0)
        {
            CancelarPOS();
            lineas.Clear();
            objetosCobrados.Clear();
            recibido = 0; entregado = 0; erroresVuelto = 0;
            seleccion = -1; primeraFila = 0;
            inicioVenta = -1f;
            paso = Paso.Escaneando;
            Recalcular();
        }
        Mensaje("El cliente se fue sin pagar. Venta anulada.", CError);
        Sonar(cError);
        Refrescar();
        Debug.Log("[Caja] Cliente se fue sin pagar (" + abandonos + " en el turno).");
    }

    /// <summary>El cliente dice si quedo conforme con la venta (para la evaluacion del turno).</summary>
    public void RegistrarOpinion(Venta v, bool conforme, string queja)
    {
        if (v == null) return;
        v.conforme = conforme;
        v.queja = conforme ? "" : queja;
        if (viendoResumen) RefrescarResumen();
    }

    /// <summary>
    /// Compatibilidad / pruebas: cobra en efectivo con lo recibido y entrega el vuelto
    /// exacto automaticamente. Devuelve true si la venta quedo cerrada.
    /// </summary>
    public bool Cobrar()
    {
        if (paso == Paso.Completada || TotalCent <= 0) return false;
        if (paso == Paso.Escaneando) IrACobrar();
        if (paso == Paso.Metodo) ElegirEfectivo();
        if (paso == Paso.RecibirEfectivo) ConfirmarRecibido();
        if (paso == Paso.EntregarVuelto)
        {
            foreach (var d in DesgloseLista(VueltoCent)) EntregarDenominacion(d);
            ConfirmarVuelto();
        }
        return paso == Paso.Completada;
    }

    void Recalcular()
    {
        int t = 0, n = 0;
        foreach (var l in lineas) { t += l.SubtotalCent; n += l.cantidad; }
        TotalCent = t;
        CantidadArticulos = n;
        VentaActualizada?.Invoke();
    }

    void Error(string texto)
    {
        Mensaje(texto, CError);
        Haptica.Ambos(0.5f, 0.15f);
        Sonar(cError);
        Refrescar();
    }

    // ================================================================== dinero

    public static int ACentimos(float soles) => Mathf.RoundToInt(soles * 100f);

    public static string Dinero(int centimos)
    {
        return BaseDatosProductos.Moneda + " " + (centimos / 100f).ToString("0.00", CultureInfo.InvariantCulture);
    }

    static string Denom(int c)
    {
        return c >= 100 && c % 100 == 0
            ? BaseDatosProductos.Moneda + " " + (c / 100)
            : BaseDatosProductos.Moneda + " " + (c / 100f).ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Billetes/monedas (uno por elemento) que suman el monto, de mayor a menor.</summary>
    public static List<int> DesgloseLista(int centimos)
    {
        var r = new List<int>();
        int resto = centimos;
        foreach (var d in Denominaciones)
            while (resto >= d) { r.Add(d); resto -= d; }
        return r;
    }

    /// <summary>Ej: "1 x S/ 20,  1 x S/ 5,  1 x S/ 0.20"</summary>
    public static string DesgloseVuelto(int centimos)
    {
        if (centimos <= 0) return "Sin vuelto";
        var sb = new StringBuilder();
        int resto = centimos;
        foreach (var d in Denominaciones)
        {
            int n = resto / d;
            if (n <= 0) continue;
            resto -= n * d;
            if (sb.Length > 0) sb.Append(",  ");
            sb.Append(n).Append(" x ").Append(Denom(d));
        }
        if (resto > 0) sb.Append(sb.Length > 0 ? ",  " : "").Append("(redondeo ").Append(resto).Append(" cts)");
        return sb.ToString();
    }

    // ================================================================== refrescar UI

    void Mensaje(string texto, Color color, bool tambienEnLector = false)
    {
        if (txtMensaje != null) { txtMensaje.text = texto; txtMensaje.color = color; }
        if (tambienEnLector && txtAviso != null && lectorAviso != null)
        {
            txtAviso.text = texto.TrimStart('+', ' ');
            txtAviso.color = color;
            avisoHasta = Time.time + duracionAviso;
        }
    }

    void Refrescar()
    {
        if (pantalla == null) return;

        // ---- titulo del paso
        switch (paso)
        {
            case Paso.Escaneando: txtPaso.text = "PASO 1:  ESCANEA LOS PRODUCTOS"; break;
            case Paso.Metodo: txtPaso.text = "PASO 2:  ¿COMO PAGA EL CLIENTE?"; break;
            case Paso.RecibirEfectivo: txtPaso.text = "PASO 3:  ¿CUANTO TE DIO EL CLIENTE?"; break;
            case Paso.EntregarVuelto: txtPaso.text = "PASO 4:  DEVUELVE EL VUELTO"; break;
            case Paso.Tarjeta: txtPaso.text = "PASO 3:  COBRA CON EL POS (TARJETA)"; break;
            case Paso.Completada: txtPaso.text = "VENTA COMPLETADA"; break;
        }
        txtPaso.color = paso == Paso.Completada ? COk : CAviso;

        // ---- lista
        bool editable = paso == Paso.Escaneando;
        for (int i = 0; i < FilasVisibles; i++)
        {
            int idx = primeraFila + i;
            var t = filasTxt[i];
            var btn = filas[i];
            if (idx < lineas.Count)
            {
                var l = lineas[idx];
                t[0].text = l.cantidad.ToString();
                t[1].text = Recortar(l.producto.nombre, 24);
                t[2].text = (l.PrecioCent / 100f).ToString("0.00", CultureInfo.InvariantCulture);
                t[3].text = (l.SubtotalCent / 100f).ToString("0.00", CultureInfo.InvariantCulture);
                btn.image.color = idx == seleccion ? CFilaSel : CFila;
                btn.interactable = editable;
            }
            else
            {
                t[0].text = t[1].text = t[2].text = t[3].text = "";
                if (i == 0 && lineas.Count == 0) t[1].text = "(sin productos)";
                btn.image.color = new Color(CFila.r, CFila.g, CFila.b, 0.35f);
                btn.interactable = false;
            }
        }
        foreach (var b in botonesEdicion) b.interactable = editable;

        // ---- paneles de la derecha
        pEscanear.SetActive(paso == Paso.Escaneando);
        pMetodo.SetActive(paso == Paso.Metodo);
        pRecibir.SetActive(paso == Paso.RecibirEfectivo);
        pVuelto.SetActive(paso == Paso.EntregarVuelto);
        pTarjeta.SetActive(paso == Paso.Tarjeta);
        pFin.SetActive(paso == Paso.Completada);
        if (pResumen != null)
        {
            pResumen.SetActive(viendoResumen);
            if (viendoResumen) RefrescarResumen();
        }

        eTotal.text = Dinero(TotalCent);
        eArticulos.text = CantidadArticulos + " articulo(s)";
        mTotal.text = "Total a cobrar:  " + Dinero(TotalCent);

        rTotal.text = "Total:  " + Dinero(TotalCent);
        rRecibido.text = "Recibido:  " + Dinero(recibido);
        if (recibido <= 0) { rEstado.text = "Marca los billetes y monedas que te dio"; rEstado.color = CTextoSuave; }
        else if (recibido < TotalCent) { rEstado.text = "Falta " + Dinero(FaltaCent); rEstado.color = CError; }
        else { rEstado.text = "Vuelto a devolver: " + Dinero(VueltoCent); rEstado.color = COk; }
        rConfirmar.image.color = recibido >= TotalCent && TotalCent > 0 ? CVerde : CBoton;

        vAEntregar.text = Dinero(VueltoCent);
        vEntregando.text = "Estas devolviendo:  " + Dinero(entregado);
        if (entregado == 0) { vEstado.text = "Marca los billetes y monedas del vuelto"; vEstado.color = CTextoSuave; }
        else if (entregado < VueltoCent) { vEstado.text = "Faltan " + Dinero(VueltoCent - entregado); vEstado.color = CAviso; }
        else if (entregado > VueltoCent) { vEstado.text = "Te pasaste por " + Dinero(entregado - VueltoCent); vEstado.color = CError; }
        else { vEstado.text = "¡Exacto! Pulsa ENTREGAR VUELTO"; vEstado.color = COk; }
        vConfirmar.image.color = entregado == VueltoCent ? CVerde : CBoton;

        tMonto.text = "Monto:  " + Dinero(TotalCent);
        tEstado.text = pos == EstadoPOS.Listo ? "Pulsa ENVIAR AL POS" : posMensaje;
        tEstado.color = pos == EstadoPOS.Aprobado ? COk : pos == EstadoPOS.Rechazado ? CError : CTexto;
        tEnviar.interactable = pos == EstadoPOS.Listo || pos == EstadoPOS.Rechazado;

        var u = UltimaVenta;
        if (paso == Paso.Completada && u != null)
        {
            fTitulo.text = u.metodo == MetodoPago.Tarjeta ? "PAGADO CON TARJETA" : "PAGADO EN EFECTIVO";
            fDetalle.text = "Total " + Dinero(u.totalCent) +
                            (u.metodo == MetodoPago.Efectivo ? "\nRecibido " + Dinero(u.recibidoCent) : "\nVoucher impreso");
            fVuelto.text = u.metodo == MetodoPago.Efectivo
                ? "Vuelto entregado\n<size=52>" + Dinero(u.vueltoCent) + "</size>"
                : "<size=52>APROBADO</size>";
        }

        // ---- turno
        int totalTurno = 0;
        foreach (var v in ventas) totalTurno += v.totalCent;
        txtTurno.text = "Turno: " + ventas.Count + " venta(s)  -  " + Dinero(totalTurno);
    }

    static string Recortar(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + ".";

    // ================================================================== construir UI

    void CrearPantalla()
    {
        Transform baseT = anclaPantalla;
        if (baseT == null)
        {
            var lector = lectores.Length > 0 && lectores[0] != null ? lectores[0].transform : transform;
            baseT = BuscarMostradorMasCercano(lector.position);
            if (baseT == null) baseT = lector;
        }
        Mostrador = baseT;
        Vector3 pos = baseT.position + baseT.rotation * desplazamientoPantalla;

        canvasPantalla = CrearCanvas("Pantalla Caja", new Vector2(W, H), pos, escalaPantalla, true);
        pantalla = canvasPantalla.transform;

        // Fija, mirando al lugar del cajero (asi el rayo apunta facil).
        var cajero = baseT.position + baseT.rotation * puntoCajeroLocal;
        var dir = pantalla.position - cajero; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) pantalla.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        var raiz = (RectTransform)pantalla;
        Img(raiz, "Fondo", CFondo, 0, 0, W, H);

        // ---- cabecera
        var cab = Img(raiz, "Cabecera", CCabecera, 0, 0, W, 60);
        Txt(cab, "Nombre", nombreCaja + "  -  MiniMarket", 32, TextAnchor.MiddleLeft, FontStyle.Bold, 20, 0, 560, 60).color = Color.white;
        txtTurno = Txt(cab, "Turno", "", 24, TextAnchor.MiddleRight, FontStyle.Normal, 540, 0, 540, 60);
        txtTurno.color = new Color(1, 1, 1, 0.85f);

        // ---- paso actual (lo que hay que hacer)
        var barraPaso = Img(raiz, "BarraPaso", new Color(0, 0, 0, 0.35f), 0, 60, W, 56);
        txtPaso = Txt(barraPaso, "Paso", "", 34, TextAnchor.MiddleCenter, FontStyle.Bold, 0, 0, W, 56);

        // ---- lista (izquierda)
        float lx = 16, lw = 620;
        var colCab = Img(raiz, "Columnas", CPanel, lx, 124, lw, 34);
        Txt(colCab, "c0", "Cant", 22, TextAnchor.MiddleCenter, FontStyle.Bold, 0, 0, 70, 34).color = CTextoSuave;
        Txt(colCab, "c1", "Producto", 22, TextAnchor.MiddleLeft, FontStyle.Bold, 80, 0, 300, 34).color = CTextoSuave;
        Txt(colCab, "c2", "P. Unit", 22, TextAnchor.MiddleRight, FontStyle.Bold, 390, 0, 100, 34).color = CTextoSuave;
        Txt(colCab, "c3", "Importe", 22, TextAnchor.MiddleRight, FontStyle.Bold, 495, 0, 115, 34).color = CTextoSuave;

        for (int i = 0; i < FilasVisibles; i++)
        {
            int fila = i;
            var b = Btn(raiz, "Fila " + i, "", CFila, lx, 162 + i * 44, lw, 40, 24, () => Seleccionar(primeraFila + fila));
            var rt = (RectTransform)b.transform;
            filasTxt.Add(new[]
            {
                Txt(rt, "Cant", "", 26, TextAnchor.MiddleCenter, FontStyle.Bold, 0, 0, 70, 40),
                Txt(rt, "Nombre", "", 24, TextAnchor.MiddleLeft, FontStyle.Normal, 80, 0, 300, 40),
                Txt(rt, "PU", "", 24, TextAnchor.MiddleRight, FontStyle.Normal, 390, 0, 100, 40),
                Txt(rt, "Imp", "", 26, TextAnchor.MiddleRight, FontStyle.Bold, 495, 0, 115, 40),
            });
            filas.Add(b);
        }

        var barraMsg = Img(raiz, "BarraMensaje", CPanel, lx, 518, lw, 50);
        txtMensaje = Txt(barraMsg, "Mensaje", "", 25, TextAnchor.MiddleLeft, FontStyle.Bold, 14, 0, lw - 28, 50);

        float ay = 576, ah = 64, g = 6, x = lx;
        botonesEdicion.Add(Btn(raiz, "Subir", "▲", CBoton, x, ay, 66, ah, 30, SubirSeleccion)); x += 66 + g;
        botonesEdicion.Add(Btn(raiz, "Bajar", "▼", CBoton, x, ay, 66, ah, 30, BajarSeleccion)); x += 66 + g;
        botonesEdicion.Add(Btn(raiz, "Menos", "-1", CBoton, x, ay, 86, ah, 32, () => CambiarCantidad(-1))); x += 86 + g;
        botonesEdicion.Add(Btn(raiz, "Mas", "+1", CBoton, x, ay, 86, ah, 32, () => CambiarCantidad(+1))); x += 86 + g;
        botonesEdicion.Add(Btn(raiz, "Quitar", "Quitar", CPeligro, x, ay, 120, ah, 26, QuitarLinea)); x += 120 + g;
        botonesEdicion.Add(Btn(raiz, "Cancelar", "Cancelar venta", CPeligro, x, ay, lx + lw - x, ah, 22, CancelarVenta));
        Txt(raiz, "Ayuda", "Toca una linea para elegirla.   Pausa: boton ☰ o B del mando.",
            19, TextAnchor.MiddleLeft, FontStyle.Italic, lx + 4, 646, lw, 40).color = CTextoSuave;

        // ---- derecha: un panel por paso
        float rx = 652, rw = 432, ry = 124, rh = H - ry - 12;
        pEscanear = Panel(raiz, "P1 Escanear", rx, ry, rw, rh);
        pMetodo = Panel(raiz, "P2 Metodo", rx, ry, rw, rh);
        pRecibir = Panel(raiz, "P3 Recibir", rx, ry, rw, rh);
        pVuelto = Panel(raiz, "P4 Vuelto", rx, ry, rw, rh);
        pTarjeta = Panel(raiz, "P3 Tarjeta", rx, ry, rw, rh);
        pFin = Panel(raiz, "Fin", rx, ry, rw, rh);

        // P1
        var p1 = (RectTransform)pEscanear.transform;
        var caja1 = Img(p1, "CajaTotal", CPanel, 0, 0, rw, 120);
        Txt(caja1, "Etiqueta", "TOTAL", 26, TextAnchor.UpperLeft, FontStyle.Bold, 16, 10, 200, 32).color = CTextoSuave;
        eArticulos = Txt(caja1, "Art", "", 22, TextAnchor.UpperRight, FontStyle.Normal, 200, 12, rw - 216, 30);
        eArticulos.color = CTextoSuave;
        eTotal = Txt(caja1, "Total", "", 60, TextAnchor.LowerRight, FontStyle.Bold, 16, 40, rw - 32, 74);
        eTotal.color = COk;
        Txt(p1, "Instr", "1. Apunta la pistola al producto\n    y aprieta el gatillo.\n2. Si lleva varios iguales,\n    escanea cada uno (o +1).\n3. Cuando termines: COBRAR.",
            25, TextAnchor.UpperLeft, FontStyle.Normal, 10, 134, rw - 20, 200).color = CTexto;
        Btn(p1, "Resumen", "Resumen del turno", CBoton, 0, 346, rw, 70, 26, MostrarResumen);
        Btn(p1, "Cobrar", "COBRAR  >", CVerde, 0, 430, rw, 130, 48, IrACobrar);

        // P2
        var p2 = (RectTransform)pMetodo.transform;
        mTotal = Txt(p2, "Total", "", 32, TextAnchor.MiddleCenter, FontStyle.Bold, 0, 0, rw, 60);
        mTotal.color = COk;
        Txt(p2, "Preg", "Pregunta al cliente como paga:", 24, TextAnchor.MiddleCenter, FontStyle.Normal, 0, 62, rw, 40).color = CTextoSuave;
        Btn(p2, "Efectivo", "EFECTIVO\n<size=24>billetes y monedas</size>", CVerde, 0, 110, rw, 150, 46, ElegirEfectivo);
        Btn(p2, "Tarjeta", "TARJETA\n<size=24>POS (debito / credito)</size>", CAzul, 0, 274, rw, 150, 46, ElegirTarjeta);
        Btn(p2, "Volver", "<  Volver a productos", CBoton, 0, 470, rw, 80, 28, Volver);

        // P3 efectivo: lo recibido
        var p3 = (RectTransform)pRecibir.transform;
        rTotal = Txt(p3, "Total", "", 28, TextAnchor.MiddleLeft, FontStyle.Bold, 8, 0, rw - 16, 40);
        rTotal.color = COk;
        rRecibido = Txt(p3, "Recibido", "", 36, TextAnchor.MiddleLeft, FontStyle.Bold, 8, 40, rw - 16, 50);
        rEstado = Txt(p3, "Estado", "", 24, TextAnchor.MiddleLeft, FontStyle.Bold, 8, 90, rw - 16, 34);
        GrillaDinero(p3, 132, PagoExacto, "Exacto");
        Btn(p3, "Borrar", "Borrar", CBoton, 0, 448, 120, 100, 26, BorrarPago);
        Btn(p3, "Volver", "<", CBoton, 128, 448, 70, 100, 34, Volver);
        rConfirmar = Btn(p3, "Confirmar", "CONFIRMAR  >", CBoton, 206, 448, rw - 206, 100, 32, ConfirmarRecibido);

        // P4 efectivo: el vuelto
        var p4 = (RectTransform)pVuelto.transform;
        Txt(p4, "Etiqueta", "Vuelto que debes devolver:", 24, TextAnchor.MiddleLeft, FontStyle.Normal, 8, 0, rw - 16, 32).color = CTextoSuave;
        vAEntregar = Txt(p4, "AEntregar", "", 48, TextAnchor.MiddleLeft, FontStyle.Bold, 8, 30, rw - 16, 56);
        vAEntregar.color = CAviso;
        vEntregando = Txt(p4, "Entregando", "", 26, TextAnchor.MiddleLeft, FontStyle.Bold, 8, 86, rw - 16, 36);
        vEstado = Txt(p4, "Estado", "", 22, TextAnchor.MiddleLeft, FontStyle.Bold, 8, 120, rw - 16, 30);
        GrillaDinero(p4, 154, AyudaVuelto, "Ayuda");
        Btn(p4, "Borrar", "Borrar", CBoton, 0, 470, 120, 92, 26, BorrarPago);
        vConfirmar = Btn(p4, "Entregar", "ENTREGAR VUELTO", CBoton, 128, 470, rw - 128, 92, 30, ConfirmarVuelto);

        // P3 tarjeta (POS)
        var pt = (RectTransform)pTarjeta.transform;
        var posCaja = Img(pt, "POS", new Color(0.05f, 0.05f, 0.06f, 1f), 40, 0, rw - 80, 330);
        Img(posCaja, "Pantallita", new Color(0.55f, 0.72f, 0.55f, 1f), 20, 20, rw - 120, 170);
        Txt(posCaja, "Marca", "POS", 22, TextAnchor.UpperRight, FontStyle.Bold, 20, 196, rw - 120, 30).color = CTextoSuave;
        tMonto = Txt(posCaja, "Monto", "", 30, TextAnchor.UpperCenter, FontStyle.Bold, 20, 30, rw - 120, 44);
        tMonto.color = new Color(0.05f, 0.12f, 0.05f);
        tEstado = Txt(posCaja, "Estado", "", 28, TextAnchor.MiddleCenter, FontStyle.Bold, 20, 76, rw - 120, 110);
        Txt(posCaja, "Teclas", "[1] [2] [3]\n[4] [5] [6]", 22, TextAnchor.UpperCenter, FontStyle.Normal, 20, 228, rw - 120, 90).color = CTextoSuave;
        tEnviar = Btn(pt, "Enviar", "ENVIAR AL POS", CAzul, 0, 344, rw, 110, 38, EnviarPOS);
        Btn(pt, "Volver", "<  Volver", CBoton, 0, 470, rw, 80, 28, Volver);

        // FIN
        var pf = (RectTransform)pFin.transform;
        Img(pf, "FondoFin", new Color(0.06f, 0.20f, 0.12f, 1f), 0, 0, rw, rh);
        fTitulo = Txt(pf, "Titulo", "", 30, TextAnchor.MiddleCenter, FontStyle.Bold, 0, 10, rw, 44);
        fTitulo.color = COk;
        fDetalle = Txt(pf, "Detalle", "", 26, TextAnchor.MiddleCenter, FontStyle.Normal, 0, 60, rw, 80);
        fVuelto = Txt(pf, "Vuelto", "", 28, TextAnchor.MiddleCenter, FontStyle.Bold, 0, 150, rw, 150);
        fVuelto.color = Color.white;
        Btn(pf, "Resumen", "Resumen del turno", CBoton, 16, 330, rw - 32, 80, 26, MostrarResumen);
        Btn(pf, "Siguiente", "Siguiente cliente", CVerde, 16, 430, rw - 32, 120, 38, NuevaVenta);

        CrearResumen(raiz);
    }

    // ================================================================== evaluacion del turno

    /// <summary>Numeros del turno, para la pantalla de resumen y las pruebas.</summary>
    public struct Evaluacion
    {
        public int ventas, articulos, totalCent, efectivo, tarjeta;
        public int erroresVuelto, quejas, abandonos, lentas;
        public float tiempoPromedio;
        public int nota;
        public string calificacion, consejo;
    }

    public const float SegundosVentaLenta = 120f;

    public Evaluacion Evaluar()
    {
        var e = new Evaluacion();
        float tiempo = 0f;
        foreach (var v in ventas)
        {
            e.ventas++;
            e.articulos += v.articulos;
            e.totalCent += v.totalCent;
            if (v.metodo == MetodoPago.Efectivo) e.efectivo++; else e.tarjeta++;
            e.erroresVuelto += v.erroresVuelto;
            if (!v.conforme) e.quejas++;
            if (v.segundos > SegundosVentaLenta) e.lentas++;
            tiempo += v.segundos;
        }
        e.abandonos = abandonos;
        e.tiempoPromedio = e.ventas > 0 ? tiempo / e.ventas : 0f;

        // Nota de 0 a 20 (como en el colegio): se parte de 20 y se resta por cada falla.
        int puntos = 20 - 2 * e.quejas - e.erroresVuelto - 3 * e.abandonos - e.lentas;
        e.nota = Mathf.Clamp(puntos, 0, 20);
        if (e.ventas == 0 && e.abandonos == 0) { e.nota = 0; e.calificacion = "Sin ventas todavia"; }
        else if (e.nota >= 18) e.calificacion = "¡Excelente!";
        else if (e.nota >= 14) e.calificacion = "Bien";
        else if (e.nota >= 11) e.calificacion = "Regular";
        else e.calificacion = "Necesitas practicar";

        // El consejo apunta a la falla mas costosa.
        int pQ = 2 * e.quejas, pV = e.erroresVuelto, pA = 3 * e.abandonos, pL = e.lentas;
        int peor = Mathf.Max(Mathf.Max(pQ, pV), Mathf.Max(pA, pL));
        if (e.ventas == 0 && e.abandonos == 0) e.consejo = "Atiende a un cliente para ver tu evaluacion.";
        else if (peor == 0) e.consejo = "Sin errores. ¡Sigue asi!";
        else if (peor == pA) e.consejo = "Atiende mas rapido: los clientes se cansan de esperar.";
        else if (peor == pQ) e.consejo = "Revisa que escaneaste TODO y el metodo de pago antes de cobrar.";
        else if (peor == pV) e.consejo = "Usa el boton Ayuda para armar el vuelto exacto.";
        else e.consejo = "Practica el escaneo: algunas ventas pasaron de 2 minutos.";
        return e;
    }

    public void MostrarResumen()
    {
        viendoResumen = true;
        Sonar(cClick);
        Refrescar();
        var e = Evaluar();
        Debug.Log("[Caja] RESUMEN: ventas " + e.ventas + " | total " + Dinero(e.totalCent) +
                  " | prom " + e.tiempoPromedio.ToString("0") + " s | errores vuelto " + e.erroresVuelto +
                  " | quejas " + e.quejas + " | se fueron " + e.abandonos + " | nota " + e.nota + "/20");
    }

    public void CerrarResumen()
    {
        viendoResumen = false;
        Sonar(cClick);
        Refrescar();
    }

    /// <summary>Empieza un turno nuevo: borra las ventas y la evaluacion.</summary>
    public void NuevoTurno()
    {
        ventas.Clear();
        abandonos = 0;
        NuevaVenta();
        viendoResumen = false;
        Mensaje("Turno nuevo. ¡Suerte!", CAviso);
        Sonar(cClick);
        Refrescar();
    }

    void CrearResumen(RectTransform raiz)
    {
        var rt = Img(raiz, "Resumen", CFondo, 0, 60, W, H - 60);
        // Tapa los botones de debajo mientras esta abierto.
        rt.GetComponent<Image>().raycastTarget = true;
        pResumen = rt.gameObject;

        Txt(rt, "Titulo", "RESUMEN DEL TURNO", 36, TextAnchor.MiddleLeft, FontStyle.Bold, 24, 8, 600, 56).color = CAviso;

        string[] etiquetas = { "Ventas", "Total vendido", "Articulos", "Tiempo promedio", "Efectivo / Tarjeta",
                               "Errores de vuelto", "Clientes con queja", "Se fueron sin pagar", "Ventas lentas (+2 min)" };
        float tx = 24, ty = 74, tw = 222, th = 118, tg = 12;
        for (int i = 0; i < etiquetas.Length; i++)
        {
            float x = tx + (i % 3) * (tw + tg), y = ty + (i / 3) * (th + tg);
            var tile = Img(rt, "Dato " + i, CPanel, x, y, tw, th);
            Txt(tile, "Etiqueta", etiquetas[i], 20, TextAnchor.UpperLeft, FontStyle.Normal, 14, 10, tw - 28, 28).color = CTextoSuave;
            var val = Txt(tile, "Valor", "", 40, TextAnchor.LowerLeft, FontStyle.Bold, 14, 40, tw - 28, 66);
            rsValores.Add(val);
        }

        float nx = 740, nw = 336;
        var notaCaja = Img(rt, "Nota", CPanel, nx, 74, nw, 250);
        Txt(notaCaja, "Etiqueta", "NOTA", 24, TextAnchor.UpperCenter, FontStyle.Bold, 0, 12, nw, 30).color = CTextoSuave;
        rsNota = Txt(notaCaja, "Valor", "", 96, TextAnchor.MiddleCenter, FontStyle.Bold, 0, 40, nw, 130);
        rsCalificacion = Txt(notaCaja, "Calif", "", 30, TextAnchor.MiddleCenter, FontStyle.Bold, 0, 180, nw, 50);

        rsConsejo = Txt(rt, "Consejo", "", 24, TextAnchor.MiddleLeft, FontStyle.Italic, 24, 470, 690, 70);
        rsConsejo.color = CTexto;

        Btn(rt, "Volver", "<  Volver a la caja", CVerde, nx, 340, nw, 100, 32, CerrarResumen);
        Btn(rt, "Nuevo", "Nuevo turno", CPeligro, nx, 456, nw, 80, 28, NuevoTurno);
        Txt(rt, "Regla", "Nota: 20 - 2 por queja - 1 por error de vuelto - 3 por cliente perdido - 1 por venta lenta",
            18, TextAnchor.MiddleLeft, FontStyle.Normal, 24, 560, W - 48, 40).color = CTextoSuave;
        pResumen.SetActive(false);
    }

    void RefrescarResumen()
    {
        if (pResumen == null) return;
        var e = Evaluar();
        string[] v =
        {
            e.ventas.ToString(), Dinero(e.totalCent), e.articulos.ToString(),
            e.ventas > 0 ? e.tiempoPromedio.ToString("0") + " s" : "-",
            e.efectivo + " / " + e.tarjeta,
            e.erroresVuelto.ToString(), e.quejas.ToString(), e.abandonos.ToString(), e.lentas.ToString(),
        };
        for (int i = 0; i < rsValores.Count && i < v.Length; i++)
        {
            rsValores[i].text = v[i];
            bool malo = (i >= 5) && v[i] != "0";
            rsValores[i].color = i == 1 ? COk : malo ? CError : CTexto;
        }
        bool sinDatos = e.ventas == 0 && e.abandonos == 0;
        rsNota.text = sinDatos ? "-" : e.nota + "<size=40>/20</size>";
        rsNota.color = sinDatos ? CTextoSuave : e.nota >= 14 ? COk : e.nota >= 11 ? CAviso : CError;
        rsCalificacion.text = e.calificacion;
        rsCalificacion.color = rsNota.color;
        rsConsejo.text = e.consejo;
    }

    GameObject Panel(RectTransform padre, string nombre, float x, float y, float w, float h)
    {
        var rt = Img(padre, nombre, new Color(0, 0, 0, 0), x, y, w, h);
        rt.gameObject.SetActive(false);
        return rt.gameObject;
    }

    /// <summary>3 x 4 botones: S/200 ... S/0.10 + un boton extra (Exacto o Ayuda).</summary>
    void GrillaDinero(RectTransform padre, float y0, Action extra, string textoExtra)
    {
        float rw = ((RectTransform)padre).sizeDelta.x;
        float cw = (rw - 2 * 8) / 3f, ch = 70;
        for (int i = 0; i < 12; i++)
        {
            float bx = (i % 3) * (cw + 8);
            float by = y0 + (i / 3) * (ch + 8);
            if (i < Denominaciones.Length)
            {
                int v = Denominaciones[i];
                Btn(padre, "Dinero " + v, Denom(v), v >= 1000 ? CBillete : CMoneda, bx, by, cw, ch, 30, () => PulsarDenominacion(v));
            }
            else Btn(padre, textoExtra, textoExtra, CBoton, bx, by, cw, ch, 28, extra);
        }
    }

    void CrearAviso()
    {
        var c = CrearCanvas("Aviso Lector", new Vector2(600, 90), transform.position, 0.0006f, false);
        aviso = c.transform;
        Img((RectTransform)aviso, "Fondo", new Color(0, 0, 0, 0.8f), 0, 0, 600, 90);
        txtAviso = Txt((RectTransform)aviso, "Texto", "", 34, TextAnchor.MiddleCenter, FontStyle.Bold, 10, 0, 580, 90);
        aviso.gameObject.SetActive(false);
    }

    Canvas CrearCanvas(string nombre, Vector2 tamano, Vector3 pos, float escala, bool interactivo)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) c.worldCamera = Camera.main;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = tamano;
        rt.position = pos;
        rt.localScale = Vector3.one * escala;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        if (interactivo)
        {
            // Igual que el menu de pausa: solo el raycaster del mando XR.
            go.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        }
        return c;
    }

    static void AsegurarEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
    }

    static void Rect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static RectTransform Img(RectTransform padre, string nombre, Color color, float x, float y, float w, float h)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        Rect(rt, x, y, w, h);
        return rt;
    }

    static Text Txt(RectTransform padre, string nombre, string texto, int tam, TextAnchor ancla, FontStyle estilo,
                    float x, float y, float w, float h)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = texto;
        t.fontSize = tam;
        t.alignment = ancla;
        t.fontStyle = estilo;
        t.color = CTexto;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Rect(t.rectTransform, x, y, w, h);
        return t;
    }

    Button Btn(RectTransform padre, string nombre, string texto, Color color, float x, float y, float w, float h,
               int tam, Action accion)
    {
        var rt = Img(padre, nombre, color, x, y, w, h);
        var img = rt.GetComponent<Image>();
        img.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        cb.colorMultiplier = 1.3f;
        cb.fadeDuration = 0.06f;
        b.colors = cb;
        b.onClick.AddListener(() => accion());
        if (!string.IsNullOrEmpty(texto))
            Txt(rt, "Texto", texto, tam, TextAnchor.MiddleCenter, FontStyle.Bold, 0, 0, w, h).color = Color.white;
        return b;
    }

    static void MirarA(Transform t, Transform objetivo, bool suave)
    {
        if (objetivo == null) return;
        Vector3 dir = t.position - objetivo.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        t.rotation = suave ? Quaternion.Slerp(t.rotation, rot, Time.deltaTime * 4f) : rot;
    }

    // ================================================================== sonidos

    void CrearSonidos()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        sfx.volume = 0.6f;
        cClick = Tono("caja_clic", new[] { 1100f }, 0.05f);
        cError = Tono("caja_error", new[] { 220f, 180f }, 0.22f);
        cCobro = Tono("caja_cobro", new[] { 1320f, 1760f, 2640f }, 0.45f);
        cPos = Tono("caja_pos", new[] { 880f, 880f }, 0.25f);
        cMoneda = SonidosProcedurales.Moneda();
        cBillete = SonidosProcedurales.Billete();
        cCajon = SonidosProcedurales.Cajon();
        cImpresora = SonidosProcedurales.Impresora();
    }

    void Sonar(AudioClip c)
    {
        if (sfx != null && c != null) sfx.PlayOneShot(c);
    }

    static AudioClip Tono(string nombre, float[] notas, float dur)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * dur);
        var data = new float[n];
        int porNota = n / notas.Length;
        for (int i = 0; i < n; i++)
        {
            int k = Mathf.Min(i / Mathf.Max(1, porNota), notas.Length - 1);
            float tLocal = (i - k * porNota) / (float)rate;
            float env = Mathf.Exp(-tLocal * 18f) * Mathf.Clamp01(tLocal * 400f);
            data[i] = Mathf.Sin(2f * Mathf.PI * notas[k] * i / rate) * env * 0.5f;
        }
        var clip = AudioClip.Create(nombre, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ================================================================== busquedas

    Transform BuscarLectorMasCercano(Vector3 punto)
    {
        Transform mejor = null; float d = float.MaxValue;
        if (lectores == null) return null;
        foreach (var l in lectores)
        {
            if (l == null || !l.isActiveAndEnabled) continue;
            float dd = (l.transform.position - punto).sqrMagnitude;
            if (dd < d) { d = dd; mejor = l.transform; }
        }
        return mejor;
    }

    static Transform BuscarMostradorMasCercano(Vector3 punto)
    {
        Transform mejor = null; float d = float.MaxValue;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!t.name.StartsWith("Pref_CashCounter")) continue;
            float dd = (t.position - punto).sqrMagnitude;
            if (dd < d) { d = dd; mejor = t; }
        }
        return mejor;
    }

    // ================================================================== pruebas (sin visor)

    [ContextMenu("Probar: escanear 5 productos")]
    public void ProbarEscaneo() { ProbarEscaneo(5); }

    public void ProbarEscaneo(int cantidad)
    {
        var raiz = GameObject.Find("Productos");
        if (raiz == null) { Debug.LogWarning("[Caja] Prueba: no hay 'Productos'."); return; }
        var grabs = raiz.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabs.Length == 0) return;
        GameObject anterior = null;
        for (int i = 0; i < cantidad; i++)
        {
            var g = (anterior != null && UnityEngine.Random.value < 0.33f)
                ? anterior
                : grabs[UnityEngine.Random.Range(0, grabs.Length)].gameObject;
            AlEscanear(g, BaseDatosProductos.Buscar(g.name));
            anterior = g;
        }
    }

    /// <summary>Prueba: el cliente paga con el billete mas chico que alcance y se entrega el vuelto.</summary>
    [ContextMenu("Probar: cobrar con billete")]
    public void ProbarCobro()
    {
        if (TotalCent <= 0) ProbarEscaneo(5);
        int[] billetes = { 1000, 2000, 5000, 10000, 20000 };
        int paga = 0;
        foreach (var b in billetes) if (b >= TotalCent) { paga = b; break; }
        if (paga == 0) paga = (TotalCent / 20000 + 1) * 20000;
        IrACobrar();
        ElegirEfectivo();
        ClienteEntrega(paga);
        ConfirmarRecibido();
        if (paso == Paso.EntregarVuelto)
        {
            foreach (var d in DesgloseLista(VueltoCent)) EntregarDenominacion(d);
            ConfirmarVuelto();
        }
    }
}
