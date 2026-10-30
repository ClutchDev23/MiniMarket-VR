using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;

/// <summary>
/// Tools/Minimarket/Git/Subir cambios a main
///
/// Trae los commits que vienen en Logs/cambios.bundle (preparados fuera de esta PC),
/// los pone en main (solo avanza, nunca reescribe) y hace push a GitHub con la cuenta
/// de esta PC. Escribe todo en Logs/git_subir.txt.
/// </summary>
public static class GitSubirCambios
{
    [MenuItem("Tools/Minimarket/Git/Subir cambios a main")]
    public static void Subir()
    {
        string raiz = Directory.GetCurrentDirectory();
        var log = new StringBuilder();
        string bundle = Path.Combine("Logs", "cambios.bundle");
        if (!File.Exists(Path.Combine(raiz, bundle)))
        {
            log.AppendLine("No hay " + bundle);
            Escribir(raiz, log);
            return;
        }
        Git(raiz, log, "status -sb");
        Git(raiz, log, "fetch " + bundle + " main:refs/heads/cambios-nuevos");
        // Si hay archivos modificados que ya son iguales a los del commit, se guardan aparte un momento.
        int st = Git(raiz, log, "stash push -m auto-subir");
        int m = Git(raiz, log, "merge --ff-only cambios-nuevos");
        if (st == 0) Git(raiz, log, "stash drop");
        if (m == 0) Git(raiz, log, "push origin main");
        Git(raiz, log, "branch -D cambios-nuevos");
        Git(raiz, log, "status -sb");
        Git(raiz, log, "log --oneline -5");
        log.AppendLine(m == 0 ? "LISTO" : "NO SE PUDO AVANZAR MAIN");
        Escribir(raiz, log);
    }

    static int Git(string dir, StringBuilder log, string args)
    {
        log.AppendLine("=== git " + args);
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = dir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using (var p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd();
            string e = p.StandardError.ReadToEnd();
            p.WaitForExit(180000);
            log.Append(o).Append(e);
            log.AppendLine("(salida " + p.ExitCode + ")");
            return p.ExitCode;
        }
    }

    static void Escribir(string raiz, StringBuilder log)
    {
        File.WriteAllText(Path.Combine(raiz, "Logs", "git_subir.txt"), log.ToString());
        UnityEngine.Debug.Log("[Git] " + log);
    }
}
