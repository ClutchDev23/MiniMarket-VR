using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// <summary>
/// Ajusta la locomocion del XR Origin para el juego de cajero:
///
/// 1. El joystick izquierdo SOLO mueve. Girar la camara con el stick izquierdo es
///    muy incomodo (te mete la mano donde miras). El giro se queda en el stick derecho.
///
///    Por que estaba asi: en el asset de input "XRI Default Input Actions" el stick
///    izquierdo esta enlazado a la vez a la accion "Move" (moverse) y a la accion
///    "Turn" (girar), asi que el mismo stick hacia las dos cosas. Aqui se desactiva
///    la parte de giro en tiempo de ejecucion, sin tocar el asset del sample.
///
/// 2. El jugador no atraviesa los estantes. El XR Origin lleva un CharacterController,
///    pero hay que ajustarlo (altura, radio, centro) para que choque con los colliders
///    de los estantes.
///
/// 3. La velocidad al caminar del menu se aplica de verdad a los proveedores de
///    movimiento (antes solo afectaba a caminar con los brazos).
///
/// Se busca todo solo en la escena: no hay que asignar nada a mano.
///
/// Nota: no se toca la escena Menu (esta escena de juego es la unica que lo usa).
/// </summary>
[DefaultExecutionOrder(-50)]
public class AjustarLocomocion : MonoBehaviour
{
    [Header("Joystick izquierdo: solo moverse (desactivar giro)")]
    [Tooltip("Desactiva el giro de camara con el stick izquierdo. Si lo apagas, el stick izquierdo unicamente mueve.")]
    public bool stickIzquierdoSoloMueve = true;

    [Header("Giro")]
    [Tooltip("Permite girar la camara con el stick derecho.")]
    public bool stickDerechoGira = true;
    [Tooltip("El stick derecho tambien mueve. Desactivado por defecto: mover y girar a la vez con el mismo stick es confuso.")]
    public bool stickDerechoTambienMueve = false;
    [Tooltip("Deja solo el giro por sectores (45 grados). El rig trae ademas un giro continuo de 60 g/s, y los dos a la vez hacen que la camara gire doble y a saltos.")]
    public bool dejarSoloSnapTurn = true;

    [Header("Colisiones con los estantes")]
    [Tooltip("Ajusta el CharacterController del XR Origin para que choque con los estantes.")]
    public bool ajustarColisiones = true;
    [Tooltip("Altura total del jugador (metros). Antes venia 1.36, muy bajo: por eso se atravesaban los estantes.")]
    public float alturaController = 1.8f;
    [Tooltip("Radio del cuerpo del jugador (metros). Antes era 0.1, demasiado estrecho para notar el estante.")]
    public float radioController = 0.3f;
    [Tooltip("Centro del cuerpo respecto a los pies (metros).")]
    public float centroController = 0.9f;

    [Header("Gravedad")]
    [Tooltip("Mantiene al jugador pegado al suelo (para que no 'flote' ni atraviese el suelo).")]
    public bool gravedadActivada = true;
    public float fuerzaGravedad = -9.81f;

    [Header("Velocidad al caminar (de los ajustes del menu)")]
    [Tooltip("Aplica la velocidad guardada en el menu (ajuste 'Velocidad al caminar') a los proveedores de movimiento.")]
    public bool aplicarVelocidadAjustada = true;

    [Header("Solo giro (para la escena del menu)")]
    [Tooltip("Si lo activas, unicamente se ajusta el giro del joystick y no se toca ni el CharacterController ni la gravedad. Es lo que se usa en la escena Menu, donde el jugador no debe chocar con nada.")]
    public bool soloGiro = false;

    void Start()
    {
        Aplicar();
    }

    void Aplicar()
    {
        // En la escena del menu solo interesa el giro del joystick: el jugador va
        // suelto por el cuarto y no debe chocar con las paredes ni caerse.
        if (soloGiro)
        {
            AjustarGiro();
            return;
        }

        var origin = BuscarXROrigin();
        if (origin == null)
        {
            Debug.LogWarning("[Locomocion] No se encontro el XR Origin.");
            return;
        }

        if (ajustarColisiones) AjustarCharacterController(origin);
        if (gravedadActivada) AjustarGravedad(origin);
        if (aplicarVelocidadAjustada) AjustarVelocidad();
        AjustarGiro();
    }

    XROrigin BuscarXROrigin()
    {
        // El XR Origin suele estar en la raiz o justo debajo del rig.
        var origenes = FindObjectsByType<XROrigin>(FindObjectsInactive.Include);
        foreach (var o in origenes)
            return o;
        return null;
    }

    // ---------------------------------------------------------------- colisiones
    void AjustarCharacterController(XROrigin origin)
    {
        var cc = origin.GetComponent<CharacterController>();
        if (cc == null)
        {
            // Si no hay CharacterController, se crea uno en el XR Origin para que
            // el jugador tenga volumen y choque con los estantes.
            cc = origin.gameObject.AddComponent<CharacterController>();
        }

        cc.height = alturaController;
        cc.radius = radioController;
        cc.center = new Vector3(0f, centroController, 0f);
        cc.slopeLimit = 50f;
        cc.stepOffset = 0.35f;
        cc.skinWidth = 0.02f;
        cc.minMoveDistance = 0f;

        Debug.Log("[Locomocion] CharacterController ajustado: h=" + alturaController +
                  " r=" + radioController + " c=" + centroController);
    }

    // ---------------------------------------------------------------- gravedad
    void AjustarGravedad(XROrigin origin)
    {
        var provider = origin.GetComponentInChildren<GravityProvider>(true);
        if (provider == null)
        {
            provider = origin.gameObject.AddComponent<GravityProvider>();
        }

        provider.useGravity = true;
        // Gravedad en espacio local (hacia el suelo del mundo, no hacia el "arriba"
        // del XR Origin), para que el jugador no se incline al caminar ni flotar.
        provider.useLocalSpaceGravity = true;
        provider.gravityAccelerationModifier = 1f;
        provider.terminalVelocity = 20f;
        provider.updateCharacterControllerCenterEachFrame = true;
        provider.sphereCastRadius = 0.09f;

        Debug.Log("[Locomocion] Gravedad activada (continua, espacio local).");
    }

    // ---------------------------------------------------------------- velocidad
    void AjustarVelocidad()
    {
        // La velocidad sale del menu principal (mismo PlayerPrefs que usa MainMenu).
        // Se aplica con AjustesAplicador, que es el mismo sitio que usa el menu de
        // pausa: asi no se aplica el factor dos veces.
        float factor = PlayerPrefs.GetFloat("walkSpeed", 1f);
        if (factor <= 0.01f) factor = 1f;

        AjustesAplicador.AplicarVelocidad(factor);

        int proveedores = 0;
        foreach (var p in FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include))
            proveedores++;

        if (proveedores > 0)
            Debug.Log("[Locomocion] Velocidad al caminar x" + factor + " en " + proveedores + " proveedor(es).");
    }

    // ---------------------------------------------------------------- giro
    /// <summary>
    /// Aplica SOLO el ajuste del giro del joystick (izquierda mueve, derecha gira).
    /// Se puede llamar a mano, sin depender del Start.
    /// </summary>
    public void AplicarGiro()
    {
        AjustarGiro();
    }

    void AjustarGiro()
    {
        // El joystick izquierdo SOLO mueve. El giro se queda en el stick derecho.
        if (stickIzquierdoSoloMueve) DesactivarGiroStickIzquierdo();

        // Hay DOS proveedores de giro (Snap Turn de 45 grados y Continuo de 60 g/s)
        // y los dos leen el stick. Por eso la camara giraba doble y a saltos.
        // Se deja el de giro por sectores (snap), que es el que se usa en Quest y
        // es mucho mas comodo en primera persona.
        if (dejarSoloSnapTurn)
        {
            foreach (var p in FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include))
            {
                p.enabled = false;
                Debug.Log("[Locomocion] ContinuousTurnProvider '" + p.name + "' desactivado (habia dos giros a la vez).");
            }
        }

        // El stick derecho: por defecto solo gira (no mueve). Se puede volver a
        // activar con la opcion del Inspector.
        if (!stickDerechoTambienMueve) DesactivarMovimientoStickDerecho();

        var providers = new List<ContinuousTurnProvider>();
        var snaps = new List<SnapTurnProvider>();

        foreach (var p in FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include))
            providers.Add(p);
        foreach (var p in FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include))
            snaps.Add(p);

        foreach (var p in providers)
        {
            // Girar 180 grados de golpe desorienta mucho en primera persona.
            p.enableTurnLeftRight = true;
            p.enableTurnAround = false;
        }

        foreach (var p in snaps)
        {
            p.enableTurnLeftRight = true;
            p.enableTurnAround = false;
        }
    }

    /// <summary>
    /// El joystick izquierdo deja de alimentar el giro de camara.
    ///
    /// En el asset "XRI Default Input Actions" el stick izquierdo
    /// (&lt;XRController&gt;{LeftHand}/{Primary2DAxis}) esta enlazado a la vez a la
    /// accion "Move" (moverse) y a "Turn" / "Snap Turn" (girar). Por eso el mismo
    /// stick hacia las dos cosas.
    ///
    /// NO se desactiva la accion: el InputActionManager de XRI hace
    /// actionAsset.Enable() en su OnEnable y la volveria a habilitar. Lo que se hace es
    /// poner el LECTOR del proveedor en InputSourceMode.Unused, que hace que su
    /// ReadValue() devuelva siempre cero. Es permanente y no se revierte.
    /// </summary>
    void DesactivarGiroStickIzquierdo()
    {
        int n = 0;

        foreach (var p in FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include))
        {
            p.leftHandTurnInput.inputSourceMode = XRInputValueReader<Vector2>.InputSourceMode.Unused;
            n++;
        }

        foreach (var p in FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include))
        {
            p.leftHandTurnInput.inputSourceMode = XRInputValueReader<Vector2>.InputSourceMode.Unused;
            n++;
        }

        if (n > 0)
            Debug.Log("[Locomocion] Giro con stick izquierdo desactivado en " + n + " proveedor(es).");
    }

    /// <summary>
    /// El stick derecho deja de mover (solo gira), si se pide asi.
    /// </summary>
    void DesactivarMovimientoStickDerecho()
    {
        int n = 0;

        foreach (var p in FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include))
        {
            p.rightHandMoveInput.inputSourceMode = XRInputValueReader<Vector2>.InputSourceMode.Unused;
            n++;
        }

        if (n > 0)
            Debug.Log("[Locomocion] Movimiento con stick derecho desactivado en " + n + " proveedor(es).");
    }
}
