using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sonidos del minimarket hechos por codigo (no hay que importar audios):
/// monedas, billetes, el cajon de la caja, la impresora de boletas y el golpe de un
/// producto que cae. Se generan una vez y se reutilizan.
/// </summary>
public static class SonidosProcedurales
{
    const int Rate = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static System.Random rnd = new System.Random(7);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { cache.Clear(); rnd = new System.Random(7); }

    static float Ruido() => (float)(rnd.NextDouble() * 2.0 - 1.0);

    static AudioClip Crear(string nombre, float dur, System.Func<int, float, float> muestra)
    {
        if (cache.TryGetValue(nombre, out var c) && c != null) return c;
        int n = Mathf.CeilToInt(Rate * dur);
        var data = new float[n];
        float max = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            data[i] = muestra(i, i / (float)Rate);
            max = Mathf.Max(max, Mathf.Abs(data[i]));
        }
        float k = 0.8f / max;   // normalizar
        for (int i = 0; i < n; i++) data[i] *= k;
        c = AudioClip.Create(nombre, n, 1, Rate, false);
        c.SetData(data, 0);
        cache[nombre] = c;
        return c;
    }

    /// <summary>Tintineo metalico de una moneda (dos golpecitos).</summary>
    public static AudioClip Moneda()
    {
        float[] f = { 3150f, 4420f, 5870f, 7300f };
        return Crear("moneda", 0.35f, (i, t) =>
        {
            float s = 0f;
            for (int k = 0; k < f.Length; k++) s += Mathf.Sin(2f * Mathf.PI * f[k] * t) / (k + 1);
            float env = Mathf.Exp(-t * 14f);
            float t2 = t - 0.07f;
            if (t2 > 0) s += 0.5f * Mathf.Sin(2f * Mathf.PI * f[1] * 1.03f * t2) * Mathf.Exp(-t2 * 20f) / env;
            return s * env * Mathf.Clamp01(t * 2000f);
        });
    }

    /// <summary>Roce de papel (billete).</summary>
    public static AudioClip Billete()
    {
        float lp = 0f;
        return Crear("billete", 0.22f, (i, t) =>
        {
            float r = Ruido();
            lp += (r - lp) * 0.35f;                     // quita lo muy agudo
            float hp = r - lp;                          // y lo muy grave
            float env = Mathf.Sin(Mathf.PI * t / 0.22f) * (0.6f + 0.4f * Mathf.Sin(t * 90f));
            return hp * env;
        });
    }

    /// <summary>"Ka-ching": campanita y el cajon que se abre con las monedas.</summary>
    public static AudioClip Cajon()
    {
        return Crear("cajon", 0.75f, (i, t) =>
        {
            float campana = (Mathf.Sin(2f * Mathf.PI * 2093f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 2637f * t)) * Mathf.Exp(-t * 6f);
            float tc = t - 0.12f;
            float golpe = tc > 0 ? Mathf.Sin(2f * Mathf.PI * 95f * tc) * Mathf.Exp(-tc * 18f) : 0f;
            float monedas = tc > 0 ? Ruido() * Mathf.Exp(-tc * 9f) * 0.25f * (0.5f + 0.5f * Mathf.Sin(tc * 300f)) : 0f;
            return 0.6f * campana + golpe + monedas;
        });
    }

    /// <summary>Impresora termica de boletas (zumbido a tirones).</summary>
    public static AudioClip Impresora()
    {
        return Crear("impresora", 1.1f, (i, t) =>
        {
            float linea = Mathf.Repeat(t * 9f, 1f) < 0.65f ? 1f : 0.15f;
            float tono = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 1180f * t)) * 0.4f + Ruido() * 0.2f;
            float env = Mathf.Clamp01(t * 40f) * Mathf.Clamp01((1.1f - t) * 20f);
            return tono * linea * env;
        });
    }

    /// <summary>Golpe seco de un producto que cae.</summary>
    public static AudioClip Golpe()
    {
        return Crear("golpe", 0.16f, (i, t) =>
        {
            float cuerpo = Mathf.Sin(2f * Mathf.PI * (180f - 300f * t) * t) * Mathf.Exp(-t * 28f);
            float roce = Ruido() * Mathf.Exp(-t * 60f) * 0.6f;
            return cuerpo + roce;
        });
    }

    /// <summary>"Tirin" del POS al leer una tarjeta sin contacto (distinto al bip del lector).</summary>
    public static AudioClip PitidoPOS()
    {
        return Crear("pos_tirin", 0.42f, (i, t) =>
        {
            // dos notas que suben (Sol6 -> Do7), suaves, con un poco de brillo
            float f = t < 0.14f ? 1568f : 2093f;
            float tl = t < 0.14f ? t : t - 0.14f;
            float dur = t < 0.14f ? 0.14f : 0.28f;
            float env = Mathf.Clamp01(tl * 500f) * Mathf.Exp(-tl * (t < 0.14f ? 6f : 9f)) * Mathf.Clamp01((dur - tl) * 80f);
            return (Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 2f * f * t)) * env;
        });
    }
}
