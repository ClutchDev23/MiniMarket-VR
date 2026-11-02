using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// MODO EXAMEN: un turno de 5 minutos cronometrado. Al terminar se muestra el resumen
/// con la nota y se genera un CERTIFICADO en PDF (Certificados/ en la carpeta del juego;
/// en el editor tambien en Logs/). Se empieza desde el menu de pausa.
/// </summary>
public class Examen : MonoBehaviour
{
    public const float Duracion = 300f;
    public static bool Activo { get; private set; }
    static float fin;
    static Examen runner;

    public static string Restante
    {
        get { int s = Mathf.Max(0, Mathf.CeilToInt(fin - Time.time)); return (s / 60) + ":" + (s % 60).ToString("00"); }
    }

    public static void Empezar()
    {
        var caja = FindAnyObjectByType<CajaRegistradora>();
        if (caja == null) return;
        if (runner == null) runner = new GameObject("Examen").AddComponent<Examen>();
        caja.NuevoTurno();
        Activo = true;
        fin = Time.time + Duracion;
        caja.Mensaje("EXAMEN: tienes 5 minutos. ¡Atiende bien a cada cliente!", new Color(1f, 0.85f, 0.3f));
        Haptica.Ambos(0.4f, 0.15f);
        Debug.Log("[Examen] Empieza (5 min).");
    }

    float proximoRefresco;
    void Update()
    {
        if (!Activo) return;
        var caja = FindAnyObjectByType<CajaRegistradora>();
        if (Time.time >= proximoRefresco) { proximoRefresco = Time.time + 1f; if (caja != null) caja.Refrescar(); }
        if (Time.time < fin) return;
        Activo = false;
        if (caja == null) return;
        var e = caja.Evaluar();
        string ruta = Certificado(e);
        caja.MostrarResumen();
        caja.Mensaje("EXAMEN TERMINADO: " + e.nota + "/20. Certificado guardado.", new Color(1f, 0.85f, 0.3f));
        Haptica.Ambos(0.6f, 0.3f);
        Debug.Log("[Examen] Terminado. Nota " + e.nota + "/20. Certificado: " + ruta);
    }

    // ------------------------------------------------------------------ PDF
    /// <summary>Genera un PDF de una pagina (A4 apaisado) sin librerias.</summary>
    public static string Certificado(CajaRegistradora.Evaluacion e)
    {
        string carpeta = Path.Combine(Application.isEditor ? Path.GetFullPath("Logs") : Application.persistentDataPath, "Certificados");
        Directory.CreateDirectory(carpeta);
        string archivo = Path.Combine(carpeta, "Certificado_" + System.DateTime.Now.ToString("yyyyMMdd_HHmm") + ".pdf");

        string aprobado = e.nota >= 14 ? "APROBADO" : "DESAPROBADO";
        var c = new StringBuilder();
        // Marco naranja
        c.Append("0.94 0.47 0.13 RG 6 w 30 30 782 535 re S\n");
        c.Append("0.94 0.47 0.13 rg 30 495 782 70 re f\n");
        T(c, "F2", 30, 1, 1, 1, 260, 518, "CERTIFICADO");
        T(c, "F1", 14, 0.3f, 0.3f, 0.3f, 300, 455, "MiniMarket VR - Simulador de cajero");
        T(c, "F1", 16, 0.1f, 0.1f, 0.1f, 160, 410, "Se certifica que el participante completo el examen de caja de 5 minutos");
        T(c, "F2", 64, e.nota >= 14 ? 0.1f : 0.8f, e.nota >= 14 ? 0.55f : 0.2f, 0.25f, 330, 315, e.nota + "/20");
        T(c, "F2", 22, 0.1f, 0.1f, 0.1f, 330, 280, aprobado + " - " + Ascii(e.calificacion));
        T(c, "F1", 14, 0.2f, 0.2f, 0.2f, 120, 220, "Ventas: " + e.ventas + "     Total cobrado: S/ " + (e.totalCent / 100f).ToString("0.00") +
                                               "     Articulos: " + e.articulos);
        T(c, "F1", 14, 0.2f, 0.2f, 0.2f, 120, 195, "Errores de vuelto: " + e.erroresVuelto + "     Quejas: " + e.quejas +
                                               "     Clientes perdidos: " + e.abandonos + "     Ventas lentas: " + e.lentas);
        T(c, "F1", 12, 0.35f, 0.35f, 0.35f, 120, 160, "Consejo: " + Ascii(e.consejo));
        T(c, "F1", 12, 0.45f, 0.45f, 0.45f, 120, 70, "Fecha: " + System.DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "      Firma: ____________________");

        string contenido = c.ToString();
        var objs = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>",
            "<< /Length " + Encoding.ASCII.GetByteCount(contenido) + " >>\nstream\n" + contenido + "endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offs = new int[objs.Length];
        for (int i = 0; i < objs.Length; i++)
        {
            offs[i] = Encoding.ASCII.GetByteCount(pdf.ToString());
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objs[i]).Append("\nendobj\n");
        }
        int xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 ").Append(objs.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var o in offs) pdf.Append(o.ToString("0000000000")).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size ").Append(objs.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        File.WriteAllBytes(archivo, Encoding.ASCII.GetBytes(pdf.ToString()));
        return archivo;
    }

    static void T(StringBuilder c, string f, int tam, float r, float g, float b, float x, float y, string s)
    {
        c.Append("BT /").Append(f).Append(' ').Append(tam).Append(" Tf ")
         .Append(N(r)).Append(' ').Append(N(g)).Append(' ').Append(N(b)).Append(" rg ")
         .Append(N(x)).Append(' ').Append(N(y)).Append(" Td (").Append(Ascii(s).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")).Append(") Tj ET\n");
    }
    static string N(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Quita tildes y signos que la fuente basica del PDF no tiene.</summary>
    static string Ascii(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder();
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (ch == '¡' || ch == '¿') continue;
            sb.Append(ch < 128 ? ch : ' ');
        }
        return sb.ToString();
    }
}
