using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Abre el menu de pausa con un boton del mando.
///
/// POR QUE HACE FALTA
/// ------------------
/// En el asset de input de XRI, el boton de menu del mando ya esta ocupado: el
/// "Select" (para agarrar) es el Grip y el "UI Press" es el Trigger. Asi que el
/// boton que hay que usar para el menu es el que sobra: el A (primaryButton) o el
/// B (secondaryButton).
///
/// Ademas, con OpenXR en Quest el Input System 1.19 no tiene un "menuButton" (el
/// layout de Quest solo define primaryButton y secondaryButton), asi que buscar
/// CommonUsages.Menu no encuentra nada. Por eso el menu no abria con ningun boton.
///
/// QUE HACE ESTE
/// --------------
/// Busca el boton por varios caminos a la vez, para que funcione con cualquier mando:
///
///   1. Por los usos del Input System: PrimaryAction / SecondaryAction / Menu.
///   2. Por los nombres de control que usa cada fabricante:
///      primaryButton (boton A en Quest), secondaryButton (B), menuButton,
///      startButton (Start en PC), and the Xbox-style aButton/bButton.
///
/// Si el mando no tiene ninguno de esos, se cae al Escape del teclado.
///
/// Se avisa con vibracion para que se note que ha abierto.
/// </summary>
[DefaultExecutionOrder(90)]
public class DisparadorMenuPausa : MonoBehaviour
{
    [Header("Vias de activacion")]
    [Tooltip("Boton A del mando (en Quest es el de menu).")]
    public bool usarBotonPrimario = true;

    [Tooltip("Boton B del mando, como alternativa.")]
    public bool usarBotonSecundario = true;

    [Tooltip("Boton Menu / Start del mando, si el dispositivo lo tiene (mandos de PC).")]
    public bool usarBotonMenu = true;

    [Tooltip("Tecla Escape del teclado, para probar en el editor.")]
    public bool usarTeclaEscape = true;

    [Header("Referencia")]
    public MenuPausa menuPausa;

    [Header("Aviso")]
    public bool vibrarAlAbrir = true;
    public float amplitudVibracion = 0.4f;
    public float duracionVibracion = 0.06f;

    // Nombres de control que significan "este boton sirve para el menu".
    static readonly string[] NombresPrimario =
    {
        "primaryButton", "aButton", "crossButton", "xButton", "squareButton",
    };
    static readonly string[] NombresSecundario =
    {
        "secondaryButton", "bButton", "circleButton", "yButton", "triangleButton",
    };
    static readonly string[] NombresMenu =
    {
        // "menu" es el boton ☰ del mando IZQUIERDO del Quest con OpenXR (no se llama menuButton).
        "menu", "menuButton", "startButton", "optionsButton", "guideButton", "viewButton",
    };

    void Awake()
    {
        if (menuPausa == null) menuPausa = GetComponent<MenuPausa>();
    }

    [Header("Ayuda al empezar")]
    [Tooltip("Muestra unos segundos delante del jugador con que boton se abre la pausa.")]
    public bool mostrarAyuda = true;
    public float segundosAyuda = 9f;
    Transform ayuda;
    float ayudaHasta;

    void Start()
    {
        if (mostrarAyuda) CrearAyuda();
    }

    void CrearAyuda()
    {
        var go = new GameObject("Ayuda Pausa", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(700, 110);
        rt.localScale = Vector3.one * 0.0011f;
        go.GetComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 4f;
        var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        fondo.transform.SetParent(go.transform, false);
        var img = fondo.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.07f, 0.08f, 0.11f, 0.88f);
        img.raycastTarget = false;
        var frt = (RectTransform)fondo.transform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        var tgo = new GameObject("Texto", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        tgo.transform.SetParent(go.transform, false);
        var t = tgo.GetComponent<UnityEngine.UI.Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = "<color=#F28C28>MENU DE PAUSA</color>\nBoton ☰ del mando izquierdo  ·  boton B  ·  Escape en PC";
        t.fontSize = 30; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
        t.supportRichText = true; t.raycastTarget = false;
        var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        ayuda = go.transform;
        ayudaHasta = Time.time + segundosAyuda;
    }

    void LateUpdate()
    {
        if (ayuda == null) return;
        var cam = Camera.main;
        if (Time.time > ayudaHasta || (menuPausa != null && menuPausa.estaAbierto)) { Destroy(ayuda.gameObject); ayuda = null; return; }
        if (cam == null) return;
        var f = cam.transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        f.Normalize();
        var objetivo = cam.transform.position + f * 1.3f + Vector3.down * 0.35f;
        ayuda.position = Vector3.Lerp(ayuda.position == Vector3.zero ? objetivo : ayuda.position, objetivo, Time.deltaTime * 4f);
        ayuda.rotation = Quaternion.LookRotation(f, Vector3.up);
    }

    void Update()
    {
        if (menuPausa == null) return;
        if (menuPausa.estaAbierto)
        {
            // Con el menu abierto, el mismo boton (☰ o B) lo cierra.
            foreach (var dev in InputSystem.devices)
            {
                if (dev is not InputDevice d || !d.added) continue;
                if ((usarBotonSecundario && Pulsado(d, NombresSecundario, CommonUsages.SecondaryAction)) ||
                    (usarBotonMenu && Pulsado(d, NombresMenu, CommonUsages.Menu)))
                {
                    menuPausa.Cerrar();
                    break;
                }
            }
            return;
        }

        bool abrir = false;

        if (usarTeclaEscape && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            abrir = true;

        if (!abrir)
        {
            foreach (var dev in InputSystem.devices)
            {
                if (dev is not InputDevice device) continue;
                if (!device.added) continue;

                if (usarBotonPrimario && Pulsado(device, NombresPrimario, CommonUsages.PrimaryAction)) { abrir = true; break; }
                if (usarBotonSecundario && Pulsado(device, NombresSecundario, CommonUsages.SecondaryAction)) { abrir = true; break; }
                if (usarBotonMenu && Pulsado(device, NombresMenu, CommonUsages.Menu)) { abrir = true; break; }
            }
        }

        if (abrir)
        {
            if (vibrarAlAbrir) Vibrar();
            menuPausa.Abrir();
        }
    }

    /// <summary>
    /// True si alguno de los botones indicados se ha pulsado este frame.
    /// Se prueban primero los usos (agnostico de fabricante) y despues los nombres
    /// de control de cada fabricante.
    /// </summary>
    static bool Pulsado(InputDevice device, string[] nombres, UnityEngine.InputSystem.Utilities.InternedString uso)
    {
        // OJO: GetChildControl LANZA un error si el control no existe (el teclado no tiene
        // "PrimaryAction"), y ese error cortaba el Update antes de mirar los mandos: por
        // eso el menu de pausa no abria nunca. TryGetChildControl devuelve null.

        // 1) Por el uso del Input System ("{PrimaryAction}" = el control con ese uso).
        var porUso = device.TryGetChildControl("{" + uso + "}");
        if (porUso is ButtonControl botonUso && botonUso.wasPressedThisFrame) return true;

        // 2) Por los nombres de control de cada fabricante.
        for (int i = 0; i < nombres.Length; i++)
        {
            var c = device.TryGetChildControl(nombres[i]);
            if (c is ButtonControl boton && boton.wasPressedThisFrame) return true;
        }

        return false;
    }

    void Vibrar()
    {
        foreach (var dev in InputSystem.devices)
        {
            if (dev is UnityEngine.InputSystem.XR.XRControllerWithRumble rumble)
                rumble.SendImpulse(amplitudVibracion, duracionVibracion);
        }
    }
}
