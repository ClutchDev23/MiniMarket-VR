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
        "menuButton", "startButton", "optionsButton", "guideButton", "viewButton",
    };

    void Awake()
    {
        if (menuPausa == null) menuPausa = GetComponent<MenuPausa>();
    }

    void Update()
    {
        if (menuPausa == null) return;
        if (menuPausa.estaAbierto) return;

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
        // 1) Por el uso del Input System.
        var porUso = device.GetChildControl(uso.ToString());
        if (porUso is ButtonControl botonUso && botonUso.wasPressedThisFrame) return true;

        // 2) Por los nombres de control de cada fabricante.
        for (int i = 0; i < nombres.Length; i++)
        {
            var c = device.GetChildControl(nombres[i]);
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
