using UnityEngine;

/// <summary>
/// Aplica en la escena de juego los ajustes elegidos en el menu.
///
/// Se llama al entrar en la escena de juego, para que los cambios que se hagan en
/// el menu (volumen, velocidad, calidad, tamano de los productos, calibracion...)
/// esten aplicados desde el primer momento.
///
/// TODA la logica esta en AjustesAplicador, que es el mismo sitio que usa el menu
/// de pausa para aplicarlos en caliente. Asi no hay dos sitios que hagan lo mismo
/// (que era como se descuadraban: el menu guardaba y el juego aplicaba, cada uno
/// por su lado).
/// </summary>
public class SettingsApplier : MonoBehaviour
{
    public ArmSwingLocomotion armSwing;

    void Start()
    {
        // Volumen.
        AjustesAplicador.AplicarVolumen(PlayerPrefs.GetFloat(GameSettings.Volume, 1f));

        // Calidad grafica.
        if (PlayerPrefs.HasKey(GameSettings.Quality))
            AjustesAplicador.AplicarCalidad(PlayerPrefs.GetInt(GameSettings.Quality));

        // Velocidad al caminar (joystick y caminar con los brazos).
        AjustesAplicador.AplicarVelocidad(PlayerPrefs.GetFloat(GameSettings.WalkSpeed, 1f));

        // Caminar con los brazos: activado o desactivado.
        AjustesAplicador.AplicarCaminarConBrazos(PlayerPrefs.GetInt(GameSettings.ArmSwing, 1) == 1);

        // Tamano de los productos.
        AjustesAplicador.AplicarTamanoProductos(PlayerPrefs.GetFloat(GameSettings.ProductScale, 1f));

        // Calibracion de los brazos (longitud y altura de la cabeza).
        AjustesAplicador.AplicarCalibracionBrazos();
    }
}
