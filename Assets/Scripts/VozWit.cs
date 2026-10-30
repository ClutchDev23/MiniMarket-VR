using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Networking;

/// <summary>
/// HABLAR CON LOS CLIENTES EN EL QUEST (sin PC).
///
/// En Windows la voz la entiende el reconocedor de Windows (VozJugador). En el Quest
/// no existe ese reconocedor, asi que se usa Wit.ai, el servicio de voz de Meta (el
/// mismo que usa el Meta Voice SDK por dentro), llamandolo directamente:
///
///   1. Mantienes presionado el STICK IZQUIERDO (clic) y hablas. En la PC tambien
///      sirve mantener la tecla V.
///   2. Al soltar, el audio del microfono se manda a Wit.ai y vuelve el texto.
///   3. Se busca en ese texto una de las frases de VozJugador ("hola", "¿algo mas?",
///      "¿efectivo o tarjeta?"...) y el cliente responde con su voz.
///
/// Necesita el "Client Access Token" de una app de Wit.ai en espanol, guardado en
/// Assets/Resources/wit_token.txt (Tools/Minimarket/Voz/Configurar Wit.ai). Ese
/// archivo no se sube a GitHub. Sin token esto no hace nada y todo sigue igual.
/// Necesita internet en el Quest.
/// </summary>
public class VozWit : MonoBehaviour
{
    const int Frecuencia = 16000;
    const int MaxSegundos = 6;
    const string Url = "https://api.wit.ai/speech?v=20240304";

    public static VozWit I { get; private set; }
    public bool Grabando { get; private set; }
    public string UltimoTexto { get; private set; } = "";

    string token;
    string mic;
    AudioClip clip;
    bool enviando;

    void Start()
    {
        I = this;
        var ta = Resources.Load<TextAsset>("wit_token");
        token = ta != null ? ta.text.Trim() : "";
        if (token.Length == 0)
        {
            Debug.Log("[Voz] Wit.ai sin token: en el Quest solo no se puede hablar con los clientes (Tools/Minimarket/Voz/Configurar Wit.ai).");
            enabled = false;
            return;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
        Debug.Log("[Voz] Wit.ai listo: manten el stick izquierdo (o la tecla V) y habla.");
    }

    void OnDestroy() { if (I == this) I = null; }

    void Update()
    {
        bool apretado = BotonHablar();
        if (apretado && !Grabando && !enviando) Empezar();
        else if (!apretado && Grabando) Terminar();
        else if (Grabando && clip != null && Microphone.GetPosition(mic) >= clip.samples - 1) Terminar();
    }

    static bool BotonHablar()
    {
        if (Keyboard.current != null && Keyboard.current.vKey.isPressed) return true;
        foreach (var dev in InputSystem.devices)
        {
            if (!(dev is UnityEngine.InputSystem.XR.XRController)) continue;
            if (!dev.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand)) continue;
            foreach (var n in new[] { "thumbstickClicked", "primary2DAxisClick", "joystickClicked" })
                if (dev.TryGetChildControl(n) is ButtonControl b && b.isPressed) return true;
        }
        return false;
    }

    void Empezar()
    {
        if (Microphone.devices.Length == 0) { Debug.LogWarning("[Voz] No hay microfono."); return; }
        mic = Microphone.devices[0];
        clip = Microphone.Start(mic, false, MaxSegundos, Frecuencia);
        Grabando = true;
        Haptica.Ambos(0.25f, 0.05f);
        Debug.Log("[Voz] Escuchando (Wit.ai)...");
    }

    void Terminar()
    {
        int muestras = Microphone.GetPosition(mic);
        Microphone.End(mic);
        Grabando = false;
        if (clip == null || muestras < Frecuencia / 4) return;   // menos de 1/4 de segundo: nada
        var datos = new float[muestras * clip.channels];
        clip.GetData(datos, 0);
        StartCoroutine(Enviar(Wav(datos, clip.channels, Frecuencia)));
    }

    IEnumerator Enviar(byte[] wav)
    {
        enviando = true;
        using (var req = new UnityWebRequest(Url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(wav);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.SetRequestHeader("Content-Type", "audio/wav");
            req.timeout = 15;
            yield return req.SendWebRequest();
            enviando = false;
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[Voz] Wit.ai no respondio: " + req.error);
                yield break;
            }
            // La respuesta llega en partes (JSON seguidos); el ultimo "text" es el final.
            string texto = "";
            foreach (Match m in Regex.Matches(req.downloadHandler.text, "\"text\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
                texto = Regex.Unescape(m.Groups[1].Value);
            UltimoTexto = texto;
            Interpretar(texto);
        }
    }

    /// <summary>Busca en lo que dijo una de las frases conocidas (la mas larga que aparezca).</summary>
    public static void Interpretar(string texto)
    {
        string t = Normalizar(texto);
        string mejor = null;
        foreach (var f in VozJugador.Frases.Keys)
            if ((" " + t + " ").Contains(" " + f + " ") && (mejor == null || f.Length > mejor.Length)) mejor = f;
        Debug.Log("[Voz] Wit.ai entendio: \"" + texto + "\"" + (mejor != null ? " -> " + mejor : " (ninguna frase conocida)"));
        if (mejor != null && VozJugador.I != null) VozJugador.I.Escuchar(mejor, VozJugador.Frases[mejor]);
    }

    static string Normalizar(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return Regex.Replace(sb.ToString(), "\\s+", " ").Trim();
    }

    static byte[] Wav(float[] datos, int canales, int frecuencia)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            int bytes = datos.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)canales);
            w.Write(frecuencia); w.Write(frecuencia * canales * 2); w.Write((short)(canales * 2)); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (var f in datos) w.Write((short)(Mathf.Clamp(f, -1f, 1f) * short.MaxValue));
            return ms.ToArray();
        }
    }
}
