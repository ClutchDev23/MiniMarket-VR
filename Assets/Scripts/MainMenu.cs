using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Claves de los ajustes guardados (PlayerPrefs).</summary>
public static class GameSettings
{
    public const string Volume = "volume";
    public const string WalkSpeed = "walkSpeed";
    public const string ArmSwing = "armSwing";
    public const string Quality = "quality";
    public const string NightMinutes = "minutesUntilNight";
}

/// <summary>
/// Menu de inicio VR: Jugar, Ajustes, Controles, Creditos y Salir.
/// Ajustes: volumen, velocidad al caminar, caminar con brazos (si/no), calidad grafica.
/// Los botones se conectan por OnClick; sliders, toggle y dropdown se conectan solos.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Escena de juego (nombre EXACTO)")]
    public string gameScene = "MinimarketXR_Full";

    [Header("Paneles")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject controlsPanel;
    public GameObject creditsPanel;

    [Header("Ajustes: volumen (Slider 0 a 1)")]
    public Slider volumeSlider;
    public TMP_Text volumeLabel;

    [Header("Ajustes: velocidad al caminar (Slider 0.5 a 2)")]
    public Slider walkSpeedSlider;
    public TMP_Text walkSpeedLabel;

    [Header("Ajustes: caminar con brazos")]
    public Toggle armSwingToggle;

    [Header("Ajustes: calidad grafica")]
    public TMP_Dropdown qualityDropdown;

    [Header("Ajustes: minutos hasta la noche (opcional, para despues)")]
    public Slider nightMinutesSlider;    // 1 a 30, Whole Numbers
    public TMP_Text nightMinutesLabel;

    void Start()
    {
        SetupUI();
        ShowOnly(mainPanel);
    }

    void SetupUI()
    {
        float vol = PlayerPrefs.GetFloat(GameSettings.Volume, 1f);
        float speed = PlayerPrefs.GetFloat(GameSettings.WalkSpeed, 1f);
        bool swing = PlayerPrefs.GetInt(GameSettings.ArmSwing, 1) == 1;
        float mins = PlayerPrefs.GetFloat(GameSettings.NightMinutes, 10f);
        int quality = PlayerPrefs.GetInt(GameSettings.Quality, QualitySettings.GetQualityLevel());

        AudioListener.volume = vol;
        QualitySettings.SetQualityLevel(quality, true);

        if (volumeSlider)
        {
            volumeSlider.SetValueWithoutNotify(vol);
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        if (walkSpeedSlider)
        {
            walkSpeedSlider.SetValueWithoutNotify(speed);
            walkSpeedSlider.onValueChanged.RemoveAllListeners();
            walkSpeedSlider.onValueChanged.AddListener(SetWalkSpeed);
        }
        if (armSwingToggle)
        {
            armSwingToggle.SetIsOnWithoutNotify(swing);
            armSwingToggle.onValueChanged.RemoveAllListeners();
            armSwingToggle.onValueChanged.AddListener(SetArmSwing);
        }
        if (qualityDropdown)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.SetValueWithoutNotify(quality);
            qualityDropdown.onValueChanged.RemoveAllListeners();
            qualityDropdown.onValueChanged.AddListener(SetQuality);
        }
        if (nightMinutesSlider)
        {
            nightMinutesSlider.SetValueWithoutNotify(mins);
            nightMinutesSlider.onValueChanged.RemoveAllListeners();
            nightMinutesSlider.onValueChanged.AddListener(SetNightMinutes);
        }

        RefreshLabels();
    }

    // --- Botones ---
    public void Play() { SceneManager.LoadScene(gameScene); }
    public void OpenSettings() { ShowOnly(settingsPanel); }
    public void OpenControls() { ShowOnly(controlsPanel); }
    public void OpenCredits() { ShowOnly(creditsPanel); }
    public void Back() { ShowOnly(mainPanel); }
    public void Quit() { Application.Quit(); }

    public void ResetDefaults()
    {
        PlayerPrefs.SetFloat(GameSettings.Volume, 1f);
        PlayerPrefs.SetFloat(GameSettings.WalkSpeed, 1f);
        PlayerPrefs.SetInt(GameSettings.ArmSwing, 1);
        PlayerPrefs.SetFloat(GameSettings.NightMinutes, 10f);
        PlayerPrefs.SetInt(GameSettings.Quality, QualitySettings.GetQualityLevel());
        PlayerPrefs.Save();
        SetupUI();
    }

    // --- Ajustes (se conectan solos) ---
    public void SetVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(GameSettings.Volume, v);
        PlayerPrefs.Save();
        RefreshLabels();
    }

    public void SetWalkSpeed(float s)
    {
        PlayerPrefs.SetFloat(GameSettings.WalkSpeed, s);
        PlayerPrefs.Save();
        RefreshLabels();
    }

    public void SetArmSwing(bool on)
    {
        PlayerPrefs.SetInt(GameSettings.ArmSwing, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetQuality(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        PlayerPrefs.SetInt(GameSettings.Quality, index);
        PlayerPrefs.Save();
    }

    public void SetNightMinutes(float m)
    {
        PlayerPrefs.SetFloat(GameSettings.NightMinutes, Mathf.Round(m));
        PlayerPrefs.Save();
        RefreshLabels();
    }

    void ShowOnly(GameObject panel)
    {
        if (mainPanel) mainPanel.SetActive(panel == mainPanel);
        if (settingsPanel) settingsPanel.SetActive(panel == settingsPanel);
        if (controlsPanel) controlsPanel.SetActive(panel == controlsPanel);
        if (creditsPanel) creditsPanel.SetActive(panel == creditsPanel);
    }

    void RefreshLabels()
    {
        if (volumeLabel)
            volumeLabel.text = "Volumen: " + Mathf.RoundToInt(PlayerPrefs.GetFloat(GameSettings.Volume, 1f) * 100f) + "%";
        if (walkSpeedLabel)
            walkSpeedLabel.text = "Velocidad al caminar: x" + PlayerPrefs.GetFloat(GameSettings.WalkSpeed, 1f).ToString("0.0");
        if (nightMinutesLabel)
            nightMinutesLabel.text = "Anochece en: " + Mathf.RoundToInt(PlayerPrefs.GetFloat(GameSettings.NightMinutes, 10f)) + " min";
    }
}
