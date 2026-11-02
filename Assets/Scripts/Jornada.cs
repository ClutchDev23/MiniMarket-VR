using UnityEngine;

/// <summary>
/// DIAS (niveles). Cada dia pide cierta cantidad de ventas; al cumplirla pasas al siguiente.
///   Dia 1: tranquilo (2 clientes a la vez, llegan despacio, compras chicas).
///   Cada dia: un cliente mas a la vez (hasta 6), llegan mas seguido, compran mas,
///   tienen menos paciencia y, desde el dia 3, pagan mas con billetes grandes.
/// El dia se guarda (PlayerPrefs "dia"); "Nuevo turno" no lo reinicia.
/// </summary>
public static class Jornada
{
    public static int Dia => Mathf.Max(1, PlayerPrefs.GetInt("dia", 1));
    public static int VentasDelDia { get; private set; }
    public static int Meta => 3 + Dia * 2;

    public static int MaxClientes(int baseConfig) => Mathf.Clamp(1 + Dia, 2, 6);
    public static float FactorIntervalo => Mathf.Max(0.45f, 1.35f - 0.15f * (Dia - 1));
    public static int ProductosExtra => Mathf.Min(Dia - 1, 4);
    public static float FactorPaciencia => Mathf.Max(0.6f, 1.2f - 0.1f * (Dia - 1));
    public static float ProbBilleteGrande => Dia >= 3 ? Mathf.Min(0.15f * (Dia - 2), 0.5f) : 0f;

    public static void VentaHecha(CajaRegistradora caja)
    {
        if (Examen.Activo) return;   // el examen no cuenta para los dias
        VentasDelDia++;
        if (VentasDelDia < Meta) return;
        int nuevo = Dia + 1;
        PlayerPrefs.SetInt("dia", nuevo);
        PlayerPrefs.Save();
        VentasDelDia = 0;
        Debug.Log("[Jornada] ¡Dia " + (nuevo - 1) + " completado! Empieza el dia " + nuevo + ".");
        if (caja != null) caja.Mensaje("¡DÍA " + (nuevo - 1) + " COMPLETADO!  Empieza el día " + nuevo + ": vendrán más clientes.", new Color(1f, 0.85f, 0.3f));
        Haptica.Ambos(0.5f, 0.25f);
    }

    public static void Reiniciar() { PlayerPrefs.SetInt("dia", 1); VentasDelDia = 0; }
}
