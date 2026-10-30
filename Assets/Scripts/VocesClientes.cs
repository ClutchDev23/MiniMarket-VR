using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voces de los clientes.
///
/// Las frases se graban UNA vez en el editor con las voces en espanol de Windows
/// (Tools/Minimarket/Clientes/6 - Grabar voces de clientes) y quedan como audios en
/// Assets/Resources/Voces/. En el juego (PC o Quest) solo se reproducen.
///
/// Si una frase no tiene audio, el cliente solo muestra la burbuja.
/// </summary>
public static class VocesClientes
{
    /// <summary>clave -> lo que se dice. La herramienta de grabar usa esta lista.</summary>
    public static readonly Dictionary<string, string> Frases = new Dictionary<string, string>
    {
        { "saludo_dia",      "¡Buenos días! Solo esto, por favor." },
        { "saludo_tarde",    "¡Buenas tardes! Solo esto, por favor." },
        { "saludo_noche",    "¡Buenas noches! Solo esto, por favor." },
        { "cuanto",          "¿Cuánto es?" },
        { "tarjeta",         "Con tarjeta, por favor." },
        { "efectivo_exacto", "En efectivo. Aquí tienes, justo el monto." },
        { "efectivo_10",     "En efectivo. Aquí tienes diez soles." },
        { "efectivo_20",     "En efectivo. Aquí tienes veinte soles." },
        { "efectivo_50",     "En efectivo. Aquí tienes cincuenta soles." },
        { "efectivo_100",    "En efectivo. Aquí tienes cien soles." },
        { "efectivo_200",    "En efectivo. Aquí tienes doscientos soles." },
        { "otro_billete",    "Ah, ¿cuánto? Entonces toma este billete." },
        { "no_efectivo",     "No, yo pago en efectivo." },
        { "acerca_tarjeta",  "Aquí está mi tarjeta." },
        { "queja_tarjeta",   "¡Pero yo te dije que pagaba con tarjeta!" },
        { "queja_efectivo",  "¿Tarjeta? Yo te pagué en efectivo." },
        { "queja_recibido",  "¡Oye! Eso no es lo que te di." },
        { "queja_demas",     "Mmm... me cobraste de más." },
        { "queja_menos",     "Creo que no me cobraste todo." },
        { "queja_productos", "El total está bien, pero eso no es lo que llevo." },
        { "gracias",         "¡Gracias! ¡Hasta luego!" },
        { "gracias_vuelto",  "¡Gracias por el vuelto! ¡Hasta luego!" },
        { "gracias_costo",   "Gracias... aunque te costó el vuelto. ¡Hasta luego!" },
        { "cansado",         "Ya me cansé de esperar... me voy." },
        { "me_voy",          "¿Hola? Ya me voy..." },
        // respuestas cuando el jugador les habla
        { "hola",            "¡Hola! ¿Qué tal?" },
        { "eso_es_todo",     "No, eso es todo, gracias." },
        { "pago_efectivo",   "Voy a pagar en efectivo." },
        { "pago_tarjeta",    "Voy a pagar con tarjeta." },
        { "sin_sencillo",    "No, lo siento, solo tengo este billete." },
        { "a_ti",            "¡A ti!" },
        { "si_bolsa",        "Sí, una bolsita por favor." },
        { "boleta",          "Boleta, por favor." },
        { "igualmente",      "¡Igualmente, gracias!" },
        { "no_entendi",      "¿Perdón? No te entendí." },
        { "espera",          "Sí, ya te atiendo, gracias." },
    };

    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static bool infoLeida;
    static bool vozMujer = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { cache.Clear(); infoLeida = false; }

    /// <summary>true si la voz con la que se grabo es de mujer (a los hombres se les baja el tono).</summary>
    public static bool VozEsDeMujer
    {
        get
        {
            if (!infoLeida)
            {
                infoLeida = true;
                var info = Resources.Load<TextAsset>("Voces/voces_info");
                if (info != null) vozMujer = !info.text.ToLowerInvariant().Contains("hombre");
            }
            return vozMujer;
        }
    }

    public static AudioClip Clip(string clave)
    {
        if (string.IsNullOrEmpty(clave)) return null;
        if (cache.TryGetValue(clave, out var c)) return c;
        c = Resources.Load<AudioClip>("Voces/" + clave);
        cache[clave] = c;
        return c;
    }
}
