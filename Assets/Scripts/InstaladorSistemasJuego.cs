using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Se engancha automaticamente a la escena de juego y monta los sistemas que
///needsita:
///   - AjustarLocomocion: stick izquierdo solo mueve + colisiones con los estantes
///   - MenuPausa: pausa con ajustes, reiniciar y salir
///
/// Se evita duplicar componentes y solo se activa en la escena de juego
/// (no en la escena Menu, que ya tiene su propio menu).
///
/// Uso: anadir este componente a un GameObject vacio en MinimarketXR_Full, o
/// ejecutarlo desde el editor con Tools/Minimarket/Agarrar/Instalar Sistemas de Juego.
/// </summary>
[DefaultExecutionOrder(-200)]
public class InstaladorSistemasJuego : MonoBehaviour
{
    [Header("Escena de juego (deja vacio = la actual, siempre que no sea el Menu)")]
    public string escenaJuego = "MinimarketXR_Full";

    [Header("Instalar")]
    public bool instalarLocomocion = true;
    public bool instalarMenuPausa = true;
    public bool instalarCorreccionSockets = true;
    [Tooltip("Caja registradora: precios de la base de datos, pantalla y total de la venta.")]
    public bool instalarCaja = true;

    [Header("Menu de pausa")]
    [Tooltip("Boton A del mando (PrimaryAction): el boton de menu en Quest.")]
    public bool abrirConBotonPrimario = true;
    [Tooltip("Boton B del mando (SecondaryAction) como alternativa.")]
    public bool abrirConBotonSecundario = true;
    [Tooltip("Boton Menu del mando, si el dispositivo lo tiene (mandos de PC).")]
    public bool abrirConBotonMenu = true;
    public string escenaMenu = "Menu";

    void Awake()
    {
        // Solo en la escena de juego.
        var actual = SceneManager.GetActiveScene().name;
        if (actual == "Menu") return;
        if (!string.IsNullOrEmpty(escenaJuego) && actual != escenaJuego) return;

        Instalar();
    }

    void Instalar()
    {
        // ------------------------------------------------------------- locomocion
        if (instalarLocomocion)
        {
            var loc = GetComponent<AjustarLocomocion>();
            if (loc == null) loc = gameObject.AddComponent<AjustarLocomocion>();
            loc.stickIzquierdoSoloMueve = true;
            loc.stickDerechoGira = true;
            loc.ajustarColisiones = true;
        }

        // ------------------------------------------------------------- menu de pausa
        if (instalarMenuPausa)
        {
            var pausa = GetComponent<MenuPausa>();
            if (pausa == null) pausa = gameObject.AddComponent<MenuPausa>();
            pausa.escenaMenu = escenaMenu;

            // El disparador es el que escucha el boton del mando y abre el menu.
            var disparador = GetComponent<DisparadorMenuPausa>();
            if (disparador == null) disparador = gameObject.AddComponent<DisparadorMenuPausa>();
            disparador.menuPausa = pausa;
            disparador.usarBotonPrimario = abrirConBotonPrimario;
            disparador.usarBotonSecundario = abrirConBotonSecundario;
            disparador.usarBotonMenu = abrirConBotonMenu;
        }

        // ------------------------------------------------------------- sockets
        if (instalarCorreccionSockets)
        {
            var sockets = GetComponent<CorregirConflictoSockets>();
            if (sockets == null) sockets = gameObject.AddComponent<CorregirConflictoSockets>();
        }

        // ------------------------------------------------------------- caja registradora
        if (instalarCaja && Object.FindAnyObjectByType<CajaRegistradora>() == null)
            gameObject.AddComponent<CajaRegistradora>();

        // ------------------------------------------------------------- calibraciones
        // El tamano de los productos y la longitud de los brazos vienen del menu.
        AplicarCalibraciones();

        // La calibracion de brazos necesita una referencia al ArmSwingLocomotion para
        // poder ajustar el umbral de "brazo estirado" con la medida real.
        var calBrazos = Object.FindFirstObjectByType<CalibrarBrazos>();
        if (calBrazos != null)
        {
            if (calBrazos.armSwing == null)
            {
                var swing = Object.FindFirstObjectByType<ArmSwingLocomotion>();
                if (swing != null) calBrazos.armSwing = swing;
            }
            if (calBrazos.head == null)
            {
                var cam = Camera.main;
                if (cam != null) calBrazos.head = cam.transform;
            }
        }

        Debug.Log("[Instalador] Sistemas de juego listos en la escena '" + SceneManager.GetActiveScene().name + "'.");
    }

    /// <summary>
    /// Aplica los ajustes de calibracion del menu a la escena:
    /// el tamano de los productos y la longitud de los brazos.
    /// </summary>
    void AplicarCalibraciones()
    {
        // Tamano de los productos.
        float escala = PlayerPrefs.GetFloat("productScale", 1f);
        if (escala <= 0.01f) escala = 1f;

        var productos = Object.FindObjectsByType<CalibrarTamanoProducto>(FindObjectsInactive.Include);

        // Red de seguridad: si a algun producto le falta el componente (porque se
        // perdio al reserializar la escena), se le anade aqui. El componente
        // normally viene heredado del prefab del producto, que es lo que no se
        // puede perder.
        var root = GameObject.Find("Productos");
        if (root != null)
        {
            var grabs = root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);
            foreach (var g in grabs)
            {
                if (g.GetComponent<CalibrarTamanoProducto>() == null)
                    g.gameObject.AddComponent<CalibrarTamanoProducto>();
            }
        }

        productos = Object.FindObjectsByType<CalibrarTamanoProducto>(FindObjectsInactive.Include);
        foreach (var p in productos)
            p.escala = escala;

        if (productos.Length > 0)
            Debug.Log("[Instalador] Tamano de productos: x" + escala + " (" + productos.Length + " productos).");

        // Longitud de los brazos.
        var cal = Object.FindFirstObjectByType<CalibrarBrazos>();
        if (cal != null)
        {
            cal.CargarMedidaGuardada();
            if (cal.tieneCalibracion)
                Debug.Log("[Instalador] Brazos calibrados: " + cal.longitud.ToString("0.00") + " m.");
        }
    }
}
