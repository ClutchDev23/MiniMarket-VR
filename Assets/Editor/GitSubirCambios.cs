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

        // Los archivos NUEVOS que trae el bundle ya estan copiados en esta carpeta (sin
        // seguimiento): git no deja avanzar si los pisaria. Se guarda su contenido, se
        // quitan, se avanza y se vuelven a poner (asi las imagenes LFS quedan bien).
        var respaldo = new System.Collections.Generic.Dictionary<string, byte[]>();
        foreach (var f in Salida(raiz, "diff --name-only --diff-filter=A HEAD cambios-nuevos").Split('\n'))
        {
            string ruta = f.Trim();
            string completo = Path.Combine(raiz, ruta);
            if (ruta.Length == 0 || !File.Exists(completo)) continue;
            respaldo[ruta] = File.ReadAllBytes(completo);
            File.Delete(completo);
        }
        log.AppendLine("archivos nuevos respaldados: " + respaldo.Count);

        int m = Git(raiz, log, "merge --ff-only cambios-nuevos", true);
        foreach (var kv in respaldo) File.WriteAllBytes(Path.Combine(raiz, kv.Key), kv.Value);
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
            CommitsDeEstaPC(raiz, log);
            Git(raiz, log, "push origin main");
        }
        Git(raiz, log, "branch -D cambios-nuevos");
        Git(raiz, log, "status -sb");
        Git(raiz, log, "log --oneline -5");
        log.AppendLine(m == 0 ? "LISTO" : "NO SE PUDO AVANZAR MAIN");
        Escribir(raiz, log);
    }

    /// <summary>Vuelve a intentar el push (guardando antes en LFS las imagenes/audios nuevos).</summary>
    [MenuItem("Tools/Minimarket/Git/Reintentar push")]
    public static void ReintentarPush()
    {
        string raiz = Directory.GetCurrentDirectory();
        var log = new StringBuilder();
        // Objetos LFS que llegan aparte como texto (Logs/lfs_*.b64: 1a linea = ruta, resto = base64).
        foreach (var b64 in Directory.GetFiles(Path.Combine(raiz, "Logs"), "lfs_*.b64"))
        {
            var lineas = File.ReadAllText(b64).Split(new[] { '\n' }, 2);
            if (lineas.Length < 2) continue;
            var datos = System.Convert.FromBase64String(lineas[1].Replace("\n", "").Replace("\r", "").Trim());
            GuardarBytesEnLfs(raiz, log, lineas[0].Trim(), datos);
            File.Delete(b64);
        }
        foreach (var f in Salida(raiz, "diff --name-only origin/main HEAD").Split('\n'))
        {
            string ruta = f.Trim();
            if (ruta.Length == 0 || !File.Exists(Path.Combine(raiz, ruta))) continue;
            if (!Salida(raiz, "check-attr filter -- \"" + ruta + "\"").Contains("filter: lfs")) continue;
            GuardarEnLfs(raiz, log, ruta);
        }
        Git(raiz, log, "push origin main");
        Git(raiz, log, "status -sb");
        Escribir(raiz, log);
    }

    /// <summary>
    /// Lo que se genero en ESTA PC (escenas, audios grabados, .meta) se sube en commits
    /// pequenos definidos en Logs/commits_pc.txt, una linea por commit:
    ///     mensaje|ruta1;ruta2;carpeta/
    /// </summary>
    static void CommitsDeEstaPC(string raiz, StringBuilder log)
    {
        string archivo = Path.Combine(raiz, "Logs", "commits_pc.txt");
        if (!File.Exists(archivo)) return;
        const string id = "-c user.name=AbnerGA7 -c user.email=215039447+AbnerGA7@users.noreply.github.com -c commit.gpgsign=false ";
        foreach (var linea in File.ReadAllLines(archivo, Encoding.UTF8))
        {
            var partes = linea.Split('|');
            if (partes.Length != 2) continue;
            foreach (var ruta in partes[1].Split(';'))
                if (ruta.Trim().Length > 0) Git(raiz, log, "add -A -- \"" + ruta.Trim() + "\"");
            if (Salida(raiz, "diff --cached --name-only").Trim().Length == 0) { log.AppendLine("(nada que subir para: " + partes[0] + ")"); continue; }
            string msg = Path.Combine(raiz, "Logs", "msg_commit.txt");
            File.WriteAllText(msg, partes[0], new UTF8Encoding(false));
            Git(raiz, log, id + "commit -F \"" + msg + "\"");
        }
        File.Delete(archivo);
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
        GuardarBytesEnLfs(dir, log, ruta, File.ReadAllBytes(Path.Combine(dir, ruta)));
    }

    static void GuardarBytesEnLfs(string dir, StringBuilder log, string ruta, byte[] datos)
    {
        var psi = new ProcessStartInfo("git", "lfs clean -- \"" + ruta + "\"")
        {
            WorkingDirectory = dir, UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
        };
        using (var p = Process.Start(psi))
        {
            p.StandardInput.BaseStream.Write(datos, 0, datos.Length);
            p.StandardInput.Close();
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit(60000);
            log.AppendLine("=== lfs " + ruta + " -> " + (o.Contains("oid") ? "ok" : o));
        }
    }

    static int Git(string dir, StringBuilder log, string args, bool sinLfs = false)
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
        // Sin bajar LFS al avanzar: el contenido de las imagenes nuevas ya esta en la PC.
        if (sinLfs) psi.EnvironmentVariables["GIT_LFS_SKIP_SMUDGE"] = "1";
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
