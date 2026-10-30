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
        string antes = Salida(raiz, "rev-parse HEAD").Trim();
        Git(raiz, log, "fetch " + bundle + " main:refs/heads/cambios-nuevos");
        // Si hay archivos modificados que ya son iguales a los del commit, se guardan aparte un momento.
        int st = Git(raiz, log, "stash push -m auto-subir");
        int m = Git(raiz, log, "merge --ff-only cambios-nuevos");
        // Se devuelven los cambios guardados (los que ya estan en el commit se juntan solos).
        if (st == 0 && !Salida(raiz, "stash list").Contains("auto-subir")) st = 1;
        if (st == 0) Git(raiz, log, "stash pop");
        if (m == 0)
        {
            // Los archivos de Git LFS (imagenes, audios) llegan como "punteros": el
            // contenido real esta en la carpeta del proyecto. Se guarda en el almacen de
            // LFS de esta PC para que el push lo pueda subir.
            foreach (var f in Salida(raiz, "diff --name-only " + antes + " HEAD").Split('\n'))
            {
                string ruta = f.Trim();
                if (ruta.Length == 0 || !File.Exists(Path.Combine(raiz, ruta))) continue;
                if (!Salida(raiz, "check-attr filter -- \"" + ruta + "\"").Contains("filter: lfs")) continue;
                GuardarEnLfs(raiz, log, ruta);
            }
            Git(raiz, log, "push origin main");
        }
        Git(raiz, log, "branch -D cambios-nuevos");
        Git(raiz, log, "status -sb");
        Git(raiz, log, "log --oneline -5");
        log.AppendLine(m == 0 ? "LISTO" : "NO SE PUDO AVANZAR MAIN");
        Escribir(raiz, log);
    }

    static string Salida(string dir, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = dir, UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true,
        };
        using (var p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit(60000);
            return o;
        }
    }

    static void GuardarEnLfs(string dir, StringBuilder log, string ruta)
    {
        var psi = new ProcessStartInfo("git", "lfs clean -- \"" + ruta + "\"")
        {
            WorkingDirectory = dir, UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
        };
        using (var p = Process.Start(psi))
        {
            var datos = File.ReadAllBytes(Path.Combine(dir, ruta));
            p.StandardInput.BaseStream.Write(datos, 0, datos.Length);
            p.StandardInput.Close();
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit(60000);
            log.AppendLine("=== lfs " + ruta + " -> " + (o.Contains("oid") ? "ok" : o));
        }
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
