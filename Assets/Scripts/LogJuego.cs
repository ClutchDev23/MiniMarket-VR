using System.IO;
using UnityEngine;

/// <summary>
/// Solo en el editor: copia a Logs/juego_log.txt los mensajes del juego que importan
/// para las pruebas ([Caja], [Bandeja], [Voz], [Clientes]...) y TODOS los errores.
/// Asi se pueden revisar las pruebas sin abrir la consola.
/// </summary>
public static class LogJuego
{
#if UNITY_EDITOR
    static string ruta;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Iniciar()
    {
        ruta = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "juego_log.txt");
        try { File.WriteAllText(ruta, "== " + System.DateTime.Now + "\n"); } catch { ruta = null; }
        Application.logMessageReceived -= AlMensaje;
        Application.logMessageReceived += AlMensaje;
    }

    static void AlMensaje(string msg, string pila, LogType tipo)
    {
        if (ruta == null) return;
        bool error = tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert;
        if (!error && !(msg.StartsWith("[") && !msg.StartsWith("[Lector] productos"))) return;
        try
        {
            File.AppendAllText(ruta, Time.realtimeSinceStartup.ToString("0.0") + "  " + tipo + "  " + msg +
                                     (error ? "\n    " + pila.Replace("\n", "\n    ") : "") + "\n");
        }
        catch { }
    }
#endif
}
