using UnityEngine;
using UnityEngine.SceneManagement;

public class AlternarDiaNoche : MonoBehaviour
{
    public static AlternarDiaNoche I { get; private set; }

    public float minutos = 3f;

    static readonly string[] Escenas = { "MinimarketXR_Full", "MinimarketXR_Night" };

    float t;
    int idx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (I != null)
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
        Debug.Log("[Noche] alternador activo, cada " + minutos + " min (escena actual: " + Escenas[idx] + ")");
    }

    void Update()
    {
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
        idx = 1 - idx;
        Debug.Log("[Noche] cambiando a " + Escenas[idx] + (forzar ? " (tecla N)" : ""));
        SceneManager.LoadScene(Escenas[idx]);
    }
}
