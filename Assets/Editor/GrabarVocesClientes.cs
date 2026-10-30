using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;

/// <summary>
/// Tools/Minimarket/Clientes/6 - Grabar voces de clientes
///
/// Graba cada frase de VocesClientes.Frases con una voz en ESPANOL instalada en
/// Windows (Sabina, Helena, Raul, Pablo...) y la guarda en
/// Assets/Resources/Voces/&lt;clave&gt;.wav. Asi los clientes hablan tambien en el Quest
/// (solo se reproducen audios; no hace falta internet).
///
/// Usa el sintetizador de voz de Windows (System.Speech) desde PowerShell.
/// Escribe el resultado en Logs/voces.txt.
/// </summary>
public static class GrabarVocesClientes
{
    const string Carpeta = "Assets/Resources/Voces";

    [MenuItem("Tools/Minimarket/Clientes/6 - Grabar voces de clientes")]
    public static void Grabar()
    {
        string raiz = Directory.GetCurrentDirectory();
        string destino = Path.Combine(raiz, Carpeta);
        Directory.CreateDirectory(destino);

        // Frases: clave<TAB>texto, una por linea.
        string lista = Path.Combine(raiz, "Temp", "voces_frases.txt");
        var sb = new StringBuilder();
        foreach (var kv in VocesClientes.Frases) sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
        File.WriteAllText(lista, sb.ToString(), new UTF8Encoding(false));

        string info = Path.Combine(destino, "voces_info.txt");
        string ps1 = Path.Combine(raiz, "Temp", "grabar_voces.ps1");
        File.WriteAllText(ps1, Script(lista, destino, info), new UTF8Encoding(true));

        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + ps1 + "\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        string salida;
        using (var p = Process.Start(psi))
        {
            salida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(300000);
        }
        File.WriteAllText(Path.Combine(raiz, "Logs", "voces.txt"), salida);
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("[Voces] " + salida);
    }

    static string Script(string lista, string destino, string info)
    {
        return @"
Add-Type -AssemblyName System.Speech
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voces = $s.GetInstalledVoices() | Where-Object { $_.Enabled }
'Voces instaladas:'
foreach ($v in $voces) { '  ' + $v.VoiceInfo.Name + ' | ' + $v.VoiceInfo.Culture + ' | ' + $v.VoiceInfo.Gender }
$es = $voces | Where-Object { $_.VoiceInfo.Culture.TwoLetterISOLanguageName -eq 'es' }
# Preferencia: espanol de America (es-MX / es-US), luego cualquier espanol.
$elegida = $es | Where-Object { $_.VoiceInfo.Culture.Name -in @('es-MX','es-US','es-PE','es-419') } | Select-Object -First 1
if (-not $elegida) { $elegida = $es | Select-Object -First 1 }
if (-not $elegida) { 'ERROR: no hay ninguna voz en espanol instalada en Windows.'; exit 1 }
$s.SelectVoice($elegida.VoiceInfo.Name)
$s.Rate = 0
$s.Volume = 100
'Usando: ' + $elegida.VoiceInfo.Name
$genero = if ($elegida.VoiceInfo.Gender -eq 'Male') { 'hombre' } else { 'mujer' }
Set-Content -Path '" + info + @"' -Value ($elegida.VoiceInfo.Name + ' | ' + $genero) -Encoding UTF8
$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
$n = 0
foreach ($linea in Get-Content -Path '" + lista + @"' -Encoding UTF8) {
  if (-not $linea) { continue }
  $partes = $linea.Split([char]9, 2)
  $archivo = Join-Path '" + destino + @"' ($partes[0] + '.wav')
  $s.SetOutputToWaveFile($archivo, $fmt)
  $s.Speak($partes[1])
  $n++
}
$s.SetOutputToNull()
$s.Dispose()
'Grabadas: ' + $n
";
    }
}
