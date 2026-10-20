using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Arranque automatico: deja el juego bien en TODAS las escenas, sin tener que
/// montar nada a mano.
///
/// QUE ARREGLA
/// -----------
/// 1. LOCOMOCION (tambien en la escena del menu)
///    El joystick izquierdo estaba enlazado a la vez a "Move" (moverse) y a "Turn"
///    (girar), asi que la camara giraba sola mientras te movias. Es muy
///    estresante, sobre todo en la escena del menu, donde ademas no hace falta
///    girar para nada. Aqui se deja el stick izquierdo SOLO para moverse y el
///    stick derecho SOLO para girar, en todas las escenas.
///    En la escena del menu ademas no se tocan el CharacterController ni la
///    gravedad (alli el jugador va suelto por el cuarto).
///
/// 2. CARRITOS PEGADOS AL SUELO
///    Los carritos tenian el Rigidbody en "kinematic": se quedaban clavados, no
///    se podian empujar y al agarrarlos se quedaban suspendidos en el aire. Con
///    CarritoEnElSuelo se pueden empujar y rodar, pero siempre pegados al suelo.
///
/// 3. MANOS EN VERTICAL
///    El modelo de mano salia tumbado, y hacia falta mas comodo agarrar.
///
/// Este script se monta solo al cargar cada escena, asi que funciona aunque
/// se abra el proyecto con el editor encima (no hace falta tocar las escenas).
/// </summary>
[DefaultExecutionOrder(-100)]
public class AjustesAutomaticos : MonoBehaviour
{
    [Header("Escenas donde NO se aplican (vacio = todas)")]
    public string[] escenasExcluidas = new string[0];

    [Header("Opciones")]
    public bool ajustarLocomocion = true;
    public bool ajustarCarritos = true;
    public bool manosVerticales = true;

    void Start()
    {
        string escena = SceneManager.GetActiveScene().name;

        foreach (var e in escenasExcluidas)
            if (!string.IsNullOrEmpty(e) && e == escena) return;

        if (ajustarLocomocion) AplicarLocomocion(escena);
        if (ajustarCarritos) AplicarCarritos();
        if (manosVerticales) AplicarManosVerticales();
    }

    // ------------------------------------------------------------------ locomocion
    void AplicarLocomocion(string escena)
    {
        var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin == null) return;

        var loc = FindFirstObjectByType<AjustarLocomocion>();
        if (loc == null) loc = origin.gameObject.AddComponent<AjustarLocomocion>();

        bool esMenu = escena == "Menu";

        // En el menu: solo el giro del joystick. Ni colisiones ni gravedad, que
        // en el cuarto del menu no hacen falta y estorban.
        loc.soloGiro = esMenu;
        loc.stickIzquierdoSoloMueve = true;
        loc.stickDerechoGira = true;

        // Se llama directamente para que el giro se aplique YA, sin depender del
        // orden de arranque de los componentes.
        loc.AplicarGiro();
    }

    // ------------------------------------------------------------------ carritos
    void AplicarCarritos()
    {
        int n = 0;

        foreach (var g in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include))
        {
            if (!Contiene(g.transform, "Trolley")) continue;

            var suelo = g.GetComponent<CarritoEnElSuelo>();
            if (suelo == null) suelo = g.gameObject.AddComponent<CarritoEnElSuelo>();
            suelo.pegarAlSuelo = true;
            suelo.empujable = true;

            // El carrito se mueve con fisica (no se teletransporta): asi se puede
            // empujar con el cuerpo o con la mano y rebota en los estantes.
            g.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            g.throwOnDetach = false;

            n++;
        }

        if (n > 0)
            Debug.Log("[Ajustes] " + n + " carrito(s) con empuje y pegados al suelo.");
    }

    // ------------------------------------------------------------------ manos
    void AplicarManosVerticales()
    {
        var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin == null) return;

        var manos = origin.GetComponent<ManosVerticales>();
        if (manos == null) manos = origin.gameObject.AddComponent<ManosVerticales>();
        manos.vertical = true;
        manos.palmaHaciaDentro = true;
    }

    // ------------------------------------------------------------------ utilidades
    /// <summary>
    /// True si el objeto esta dentro de una jerarquia cuyo nombre tiene el texto.
    /// </summary>
    static bool Contiene(Transform tr, string texto)
    {
        int guard = 0;
        while (tr != null && guard < 40)
        {
            if (tr.name.Contains(texto)) return true;
            tr = tr.parent;
            guard++;
        }
        return false;
    }
}
