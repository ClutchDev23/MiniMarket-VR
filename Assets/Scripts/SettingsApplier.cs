using UnityEngine;

/// <summary>
/// Aplica en la escena de juego los ajustes elegidos en el menu.
/// Poner en un objeto vacio de la escena de juego y asignar ArmSwingLocomotion.
/// </summary>
public class SettingsApplier : MonoBehaviour
{
    public ArmSwingLocomotion armSwing;

    void Start()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(GameSettings.Volume, 1f);

        if (PlayerPrefs.HasKey(GameSettings.Quality))
            QualitySettings.SetQualityLevel(PlayerPrefs.GetInt(GameSettings.Quality), true);

        if (!armSwing) return;

        float factor = PlayerPrefs.GetFloat(GameSettings.WalkSpeed, 1f);
        armSwing.speedMultiplier *= factor;
        armSwing.maxSpeed *= factor;
        armSwing.enabled = PlayerPrefs.GetInt(GameSettings.ArmSwing, 1) == 1;
    }
}
