using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

/// <summary>
/// Aplica los ajustes guardados (los del menu) en la escena de juego, y tambien
/// los aplica en caliente cuando se cambian desde el menu de pausa.
///
/// QUE ESTA MAL
/// ------------
/// El menu de pausa guardaba los ajustes en PlayerPrefs, pero NO los aplicaba a la
/// escena: habia que reiniciar la escena (o salir y volver a entrar) para que
/// cogieran efecto. Y el "SettingsApplier" de la escena aplicaba algunos pero no
/// todos, y a veces el mismo ajuste se aplicaba dos veces.
///
/// QUE HACE ESTE
/// --------------
/// Un unico sitio que sabe aplicar cada ajuste:
///
///   - Volumen
///   - Velocidad al caminar (a los proveedores de movimiento y al caminar con los
///     brazos)
///   - Caminar con los brazos (activado/desactivado)
///   - Calidad grafica
///   - Tamano de los productos
///   - Longitud de los brazos (calibracion)
///
/// Se llama desde el menu de pausa (al mover un slider o un boton) y desde el
/// arranque del juego (para que los ajustes del menu se apliquen al entrar).
/// </summary>
public static class AjustesAplicador
{
    // ------------------------------------------------------------------ volumen
    public static void AplicarVolumen(float v)
    {
        AudioListener.volume = Mathf.Clamp01(v);
    }

    // ------------------------------------------------------------------ calidad
    public static void AplicarCalidad(int index)
    {
        int niveles = QualitySettings.names.Length;
        if (niveles <= 0) return;
        index = Mathf.Clamp(index, 0, niveles - 1);
        QualitySettings.SetQualityLevel(index, true);
    }

    // ------------------------------------------------------------------ caminar
    /// <summary>
    /// Aplica la velocidad al caminar a los proveedores de movimiento y al caminar
    /// con los brazos.
    /// </summary>
    public static void AplicarVelocidad(float factor)
    {
        factor = Mathf.Max(0.1f, factor);

        // A los proveedores de movimiento (el joystick).
        // Se guarda la velocidad BASE del proveedor (la del prefab) en un
        // componente pegado a el, para que el factor se pueda aplicar varias
        // veces (al entrar en la escena y al mover el slider) sin que se vaya
        // encadenando.
        foreach (var p in Object.FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include))
        {
            if (p == null) continue;

            var baseVel = p.GetComponent<VelocidadBaseMovimiento>();
            if (baseVel == null)
            {
                baseVel = p.gameObject.AddComponent<VelocidadBaseMovimiento>();
                baseVel.baseVelocidad = p.moveSpeed;
            }

            p.moveSpeed = baseVel.baseVelocidad * factor;
        }

        // Al caminar con los brazos.
        foreach (var s in Object.FindObjectsByType<ArmSwingLocomotion>(FindObjectsInactive.Include))
        {
            if (s == null) continue;
            s.speedMultiplier = s.velocidadBase * factor;
            s.maxSpeed = s.velocidadMaximaBase * factor;
        }
    }

    // ------------------------------------------------------------------ brazos
    /// <summary>
    /// Activa o desactiva el caminar con los brazos.
    /// </summary>
    public static void AplicarCaminarConBrazos(bool on)
    {
        foreach (var s in Object.FindObjectsByType<ArmSwingLocomotion>(FindObjectsInactive.Include))
            if (s != null) s.enabled = on;
    }

    // ------------------------------------------------------------------ productos
    /// <summary>
    /// Aplica el tamano de los productos.
    /// </summary>
    public static void AplicarTamanoProductos(float escala)
    {
        escala = Mathf.Clamp(escala, 0.5f, 2f);

        foreach (var p in Object.FindObjectsByType<CalibrarTamanoProducto>(FindObjectsInactive.Include))
            if (p != null) p.escala = escala;
    }

    // ------------------------------------------------------------------ calibracion
    /// <summary>
    /// Carga la calibracion de brazos guardada y la aplica.
    /// </summary>
    public static void AplicarCalibracionBrazos()
    {
        var cal = Object.FindFirstObjectByType<CalibrarBrazos>();
        if (cal == null) return;

        cal.CargarMedidaGuardada();
    }
}
