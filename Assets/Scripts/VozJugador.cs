using System.Collections.Generic;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

/// <summary>
/// HABLAR CON LOS CLIENTES (con tu voz).
///
/// Dices una frase corta y el cliente mas cercano (o el que esta en la caja) te responde
/// con su voz. Ejemplos: "hola", "¿como va a pagar?", "¿efectivo o tarjeta?",
/// "¿algo mas?", "¿tiene sencillo?", "¿quiere bolsa?", "¿boleta?", "gracias".
///
/// Usa el reconocimiento de voz de WINDOWS, asi que funciona en el editor y en el build
/// de PC (tambien con el Quest conectado por cable o Air Link). En el Quest solo, sin PC,
/// no hay reconocimiento de voz sin internet: queda propuesto con Meta Voice SDK.
///
/// Se instala solo en las escenas del minimarket.
/// </summary>
public class VozJugador : MonoBehaviour
{
    public enum Intencion { Saludo, ComoPaga, AlgoMas, Sencillo, Bolsa, Boleta, Gracias, Despedida, Espere }

    /// <summary>Frases que se reconocen (en minusculas, sin signos).</summary>
    public static readonly Dictionary<string, Intencion> Frases = new Dictionary<string, Intencion>
    {
        { "hola", Intencion.Saludo }, { "buenos dias", Intencion.Saludo }, { "buenas tardes", Intencion.Saludo },
        { "buenas noches", Intencion.Saludo }, { "bienvenido", Intencion.Saludo }, { "bienvenida", Intencion.Saludo },
        { "como va a pagar", Intencion.ComoPaga }, { "como paga", Intencion.ComoPaga }, { "efectivo o tarjeta", Intencion.ComoPaga },
        { "tarjeta o efectivo", Intencion.ComoPaga }, { "forma de pago", Intencion.ComoPaga },
        { "algo mas", Intencion.AlgoMas }, { "seria todo", Intencion.AlgoMas }, { "es todo", Intencion.AlgoMas },
        { "tiene sencillo", Intencion.Sencillo }, { "tiene cambio", Intencion.Sencillo }, { "tiene monedas", Intencion.Sencillo },
        { "quiere bolsa", Intencion.Bolsa }, { "necesita bolsa", Intencion.Bolsa }, { "bolsita", Intencion.Bolsa },
        { "boleta o factura", Intencion.Boleta }, { "quiere boleta", Intencion.Boleta }, { "boleta", Intencion.Boleta },
        { "gracias", Intencion.Gracias }, { "muchas gracias", Intencion.Gracias },
        { "hasta luego", Intencion.Despedida }, { "que tenga buen dia", Intencion.Despedida }, { "vuelva pronto", Intencion.Despedida },
        { "un momento", Intencion.Espere }, { "espere por favor", Intencion.Espere }, { "ya le atiendo", Intencion.Espere },
    };

    public static VozJugador I { get; private set; }
    /// <summary>Ultima frase entendida (para mostrarla en pantalla y en pruebas).</summary>
    public string UltimaFrase { get; private set; } = "";
    public bool Escuchando { get; private set; }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    KeywordRecognizer reconocedor;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (esc, modo) => Asegurar(esc);
        Asegurar(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void Asegurar(UnityEngine.SceneManagement.Scene esc)
    {
        if (!esc.IsValid() || !esc.name.StartsWith("MinimarketXR")) return;
        if (FindAnyObjectByType<VozJugador>() != null) return;
        var go = new GameObject("Voz del jugador");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, esc);
        go.AddComponent<VozJugador>();
    }

    void Start()
    {
        I = this;
        // En el Quest (sin Windows) se habla con Wit.ai: manten el stick izquierdo y habla.
        if (GetComponent<VozWit>() == null) gameObject.AddComponent<VozWit>();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            var claves = new List<string>(Frases.Keys);
            reconocedor = new KeywordRecognizer(claves.ToArray(), ConfidenceLevel.Low);
            reconocedor.OnPhraseRecognized += AlReconocer;
            reconocedor.Start();
            Escuchando = reconocedor.IsRunning;
            Debug.Log("[Voz] Escuchando " + claves.Count + " frases (microfono de Windows). Activo=" + Escuchando);
        }
        catch (System.Exception e)
        {
            Escuchando = false;
            Debug.LogWarning("[Voz] No se pudo activar el reconocimiento de voz de Windows: " + e.Message +
                             " (revisa Configuracion > Privacidad > Voz y el idioma de voz en espanol).");
        }
#else
        Debug.Log("[Voz] Sin reconocedor de Windows: se usa Wit.ai (manten el stick izquierdo y habla).");
#endif
    }

    void OnDestroy()
    {
        if (I == this) I = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (reconocedor != null)
        {
            if (reconocedor.IsRunning) reconocedor.Stop();
            reconocedor.OnPhraseRecognized -= AlReconocer;
            reconocedor.Dispose();
        }
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    void AlReconocer(PhraseRecognizedEventArgs a)
    {
        if (Frases.TryGetValue(a.text, out var intencion)) Escuchar(a.text, intencion);
    }
#endif

    /// <summary>Lo que dijo el jugador (tambien se usa en las pruebas sin microfono).</summary>
    public void Escuchar(string frase, Intencion intencion)
    {
        UltimaFrase = frase;
        var c = ClienteQueEscucha();
        Debug.Log("[Voz] Jugador: \"" + frase + "\" -> " + intencion + (c != null ? " (responde " + c.name + ")" : " (nadie cerca)"));
        if (c != null) c.Responder(intencion);
    }

    /// <summary>El cliente de la caja; si no hay, el mas cercano a menos de 3.5 m.</summary>
    static Cliente ClienteQueEscucha()
    {
        var g = GestorClientes.I;
        var enCaja = g != null ? g.ClienteEnCaja : null;
        if (enCaja != null && enCaja.EnCaja) return enCaja;
        var cam = Camera.main;
        if (cam == null) return null;
        Cliente mejor = null; float d = 3.5f * 3.5f;
        foreach (var c in FindObjectsByType<Cliente>(FindObjectsSortMode.None))
        {
            float dd = (c.transform.position - cam.transform.position).sqrMagnitude;
            if (dd < d) { d = dd; mejor = c; }
        }
        return mejor;
    }
}
