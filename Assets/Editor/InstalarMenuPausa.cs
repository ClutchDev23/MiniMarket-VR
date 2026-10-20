using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Anade el MENU DE PAUSA a la escena de juego como GameObject real (no se crea en
/// runtime), para que quede bien montado desde el principio y se pueda editar desde el
/// Inspector.
///
/// Es el menu que sale al pausar en la escena MinimarketXR_Full: Continuar, Reiniciar,
/// Salir, mas los ajustes (volumen, velocidad, tamano de productos, caminar con brazos,
/// calidad, calibrar brazos).
///
/// Uso: Tools/Minimarket/Agarrar/Instalar Menu de Pausa (escena de juego)
/// </summary>
public static class InstalarMenuPausa
{
    const string Escena = "Assets/Scenes/MinimarketXR_Full.unity";
    const string Nombre = "Menu de Pausa";

    [MenuItem("Tools/Minimarket/Agarrar/Instalar Menu de Pausa (escena de juego)")]
    public static void Ejecutar()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Instalacion del menu de pausa ===");

        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // Si ya hay uno, se reutiliza.
        var existente = UnityEngine.Object.FindFirstObjectByType<MenuPausa>();
        GameObject go;

        if (existente != null)
        {
            go = existente.gameObject;
            t.AppendLine("  ya habia un MenuPausa: '" + go.name + "'");
        }
        else
        {
            go = new GameObject(Nombre);
            t.AppendLine("  MenuPausa creado ('" + Nombre + "')");
        }

        var pausa = go.GetComponent<MenuPausa>();
        if (pausa == null) pausa = go.AddComponent<MenuPausa>();
        pausa.escenaMenu = "Menu";

        // El disparador del boton del mando, en el MISMO GameObject para que se
        // enganchen solos.
        var disparador = go.GetComponent<DisparadorMenuPausa>();
        if (disparador == null) disparador = go.AddComponent<DisparadorMenuPausa>();
        disparador.menuPausa = pausa;
        disparador.usarBotonPrimario = true;
        disparador.usarBotonSecundario = true;
        disparador.usarBotonMenu = true;

        // El instalador general ya no hace falta crearlo (pero se deja por si acaso
        // quiere el resto de sistemas).
        var instalador = UnityEngine.Object.FindFirstObjectByType<InstaladorSistemasJuego>();
        if (instalador == null)
        {
            var goInst = new GameObject("Sistemas de Juego");
            instalador = goInst.AddComponent<InstaladorSistemasJuego>();
            t.AppendLine("  InstaladorSistemasJuego creado");
        }

        if (!EditorSceneManager.SaveScene(scene, Escena))
        {
            Debug.LogError("[MenuPausa] No se pudo guardar la escena.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        t.AppendLine("  menu de pausa instalado y escena guardada");
        Debug.Log(t.ToString());
        Directory.CreateDirectory(@"C:\Users\abner\AppData\Local\Temp\opencode");
        File.WriteAllText(@"C:\Users\abner\AppData\Local\Temp\opencode\menu_pausa.txt", t.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
