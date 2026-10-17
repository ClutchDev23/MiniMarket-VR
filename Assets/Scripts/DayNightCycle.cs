using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ciclo dia/noche por tiempo real. Ejemplo: empieza a las 12:00 y a los 10 minutos
/// reales llega a las 20:00 (noche). Despues sigue el ciclo de 24 h al mismo ritmo.
/// Poner en un objeto vacio de la escena de juego.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("Referencias")]
    public Light sun;                    // Directional Light (modo Realtime)
    public TMP_Text clockLabel;          // Reloj HH:mm (opcional)
    public GameObject[] nightObjects;    // Farolas, luces del techo, etc. (se encienden de noche)

    [Header("Tiempo")]
    [Range(0f, 24f)] public float startHour = 12f;
    public float nightStartHour = 20f;
    public float minutesUntilNight = 10f;   // minutos REALES desde startHour hasta nightStartHour
    public bool useSavedMinutes = true;     // usa lo elegido en Ajustes del menu

    [Header("Luz")]
    public float maxSunIntensity = 1.2f;
    public Color dayAmbient = new Color(0.75f, 0.8f, 0.9f);
    public Color nightAmbient = new Color(0.05f, 0.06f, 0.12f);
    public float maxSkyExposure = 1.3f;
    public float minSkyExposure = 0.05f;
    [Range(-0.5f, 0.5f)] public float lampsOnBelow = 0.1f;

    float hour;
    float hoursPerSecond;
    Material skyInstance;
    bool lampsOn;
    bool lampsInitialized;

    void Start()
    {
        if (useSavedMinutes && PlayerPrefs.HasKey("minutesUntilNight"))
            minutesUntilNight = PlayerPrefs.GetFloat("minutesUntilNight");

        hour = startHour;
        float hoursToNight = nightStartHour - startHour;
        if (hoursToNight <= 0f) hoursToNight += 24f;
        hoursPerSecond = hoursToNight / (Mathf.Max(minutesUntilNight, 0.1f) * 60f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        if (RenderSettings.skybox != null)
        {
            skyInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyInstance;
        }

        Apply();
    }

    void Update()
    {
        hour += hoursPerSecond * Time.deltaTime;
        if (hour >= 24f) hour -= 24f;
        Apply();
    }

    void Apply()
    {
        // 6:00 = amanecer, 12:00 = mediodia, 18:00 = atardecer, 0:00 = medianoche
        float elevation = Mathf.Sin((hour - 6f) / 24f * Mathf.PI * 2f);

        if (sun)
        {
            float angle = (hour - 6f) / 24f * 360f;
            sun.transform.rotation = Quaternion.Euler(angle, 170f, 0f);
            sun.intensity = Mathf.Clamp01(elevation) * maxSunIntensity;
        }

        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elevation + 0.1f) / 0.5f));
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, t);

        if (skyInstance != null && skyInstance.HasProperty("_Exposure"))
            skyInstance.SetFloat("_Exposure", Mathf.Lerp(minSkyExposure, maxSkyExposure, t));

        bool shouldBeOn = elevation < lampsOnBelow;
        if (!lampsInitialized || shouldBeOn != lampsOn)
        {
            lampsInitialized = true;
            lampsOn = shouldBeOn;
            foreach (var go in nightObjects)
                if (go) go.SetActive(lampsOn);
        }

        if (clockLabel)
        {
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);
            clockLabel.text = h.ToString("00") + ":" + m.ToString("00");
        }
    }
}
