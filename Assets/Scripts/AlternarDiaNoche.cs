using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cambia entre la escena de dia y la de noche segun el ajuste "Anochece en X min"
/// del menu.
///
/// QUE ESTA MAL
/// ------------
/// 1. Los minutos venian de un valor fijo en el script, asi que el ajuste del
///    menu ("Anochece en 10 min") no hacia nada.
///
/// 2. La escena de noche (MinimarketXR_Night) esta DESACTIVADA en los ajustes de
///    build, asi que al llegar el momento de cambiar de escena se intentaba
///    cargar una escena que no existe en el juego: error en pantalla y el juego
///    se queda raro. Ahora se comprueba antes de cargar.
///
/// Uso: Tools/Minimarket/Agarrar/... (se instala solo al entrar en el juego)
/// </summary>
public class AlternarDiaNoche : MonoBehaviour
{
    public static AlternarDiaNoche I { get; private set; }

    [Tooltip("Minutos hasta que anochece. Si es 0, se usa el ajuste del menu.")]
    public float minutos = 0f;

    static readonly string[] Escenas = { "MinimarketXR_Full", "MinimarketXR_Night" };

    float t;
    int idx;

    static bool EscenaDeJuego()
    {
        string activa = SceneManager.GetActiveScene().name;
        return activa == Escenas[0] || activa == Escenas[1];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (I != null)
            return;

        if (!EscenaDeJuego())
            return;

        var go = new GameObject("AlternarDiaNoche");
        DontDestroyOnLoad(go);
        I = go.AddComponent<AlternarDiaNoche>();
    }

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        idx = SceneManager.GetActiveScene().name == Escenas[1] ? 1 : 0;

        // El ajuste del menu manda (si esta a 0, se usa el valor del script).
        // Por defecto 10 min, igual que el menu. Antes era 0 y, si nunca se habia
        // pasado por el menu (Play directo en la escena), cambiaba de escena en
        // CADA frame: dia -> noche -> dia sin parar.
        float delMenu = PlayerPrefs.GetFloat(GameSettings.NightMinutes, 10f);
        if (delMenu > 0.01f) minutos = delMenu;
        if (minutos < 0.5f) minutos = 10f;

        Debug.Log("[Noche] alternador activo, cada " + minutos + " min (escena actual: " + Escenas[idx] + ")");
    }

    void Update()
    {
        if (!EscenaDeJuego())
        {
            t = 0f;
            return;
        }

        bool forzar = false;
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.nKey.wasPressedThisFrame)
            forzar = true;
#else
        if (Input.GetKeyDown(KeyCode.N))
            forzar = true;
#endif

        t += Time.deltaTime;
        if (!forzar && t < minutos * 60f)
            return;

        t = 0f;

        int destino = 1 - idx;

        // La escena de noche puede no estar en los ajustes de build (de hecho no
        // lo esta). Si no se puede cargar, no se intenta: se avisa y se sigue
        // jugando en la misma escena.
        if (!Application.CanStreamedLevelBeLoaded(Escenas[destino]))
        {
            Debug.Log("[Noche] la escena '" + Escenas[destino] + "' no esta en el juego: se sigue en '" + Escenas[idx] + "'.");
            return;
        }

        idx = destino;
        Debug.Log("[Noche] cambiando a " + Escenas[idx] + (forzar ? " (tecla N)" : ""));
        SceneManager.LoadScene(Escenas[idx]);
    }
}
