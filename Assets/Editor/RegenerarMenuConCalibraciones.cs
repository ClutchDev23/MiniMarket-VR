using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Regenera el MENU VR de la escena Menu con los controles de calibracion nuevos
/// (tamano de productos y calibracion de brazos), SIN tocar el cuarto del menu.
///
/// El MenuBuilder original abre un dialogo, asi que no sirve para ejecucion automatica.
/// Este hace lo mismo que su BuildMenu() pero sin preguntas.
///
/// Uso: Tools/Minimarket/Agarrar/Regenerar Menu con Calibraciones
/// </summary>
public static class RegenerarMenuConCalibraciones
{
    const string Escena = "Assets/Scenes/Menu.unity";

    [MenuItem("Tools/Minimarket/Agarrar/Regenerar Menu con Calibraciones")]
    public static void Ejecutar()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Regeneracion del menu con calibraciones ===");

        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // Se llama al builder por reflexion para no duplicar su logica.
        var tipo = System.Type.GetType("MenuBuilder") ??
                   typeof(MenuBuilder);

        var metodo = tipo.GetMethod("BuildMenu",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (metodo == null)
        {
            t.AppendLine("  no se encontro MenuBuilder.BuildMenu");
            Escribir(t);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // El builder pide sprites: se preparations antes.
        var ensure = tipo.GetMethod("EnsureSprites",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        ensure?.Invoke(null, null);

        var lastF = tipo.GetField("lastFloaters", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var lastB = tipo.GetField("lastBob", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var lastN = tipo.GetField("lastNeon", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (lastF != null) lastF.SetValue(null, null);
        if (lastB != null) lastB.SetValue(null, null);
        if (lastN != null) lastN.SetValue(null, null);

        metodo.Invoke(null, null);

        // Comprobacion: los controles nuevos deben estar conectados.
        var mm = UnityEngine.Object.FindFirstObjectByType<MainMenu>();
        if (mm == null)
        {
            t.AppendLine("  ERROR: no se encontro MainMenu tras regenerar");
        }
        else
        {
            t.AppendLine("  productScaleSlider=" + (mm.productScaleSlider != null ? "OK" : "FALTA"));
            t.AppendLine("  productScaleLabel=" + (mm.productScaleLabel != null ? "OK" : "FALTA"));
            t.AppendLine("  calibrateArmsButton=" + (mm.calibrateArmsButton != null ? "OK" : "FALTA"));
            t.AppendLine("  calibrateArmsLabel=" + (mm.calibrateArmsLabel != null ? "OK" : "FALTA"));
        }

        if (!EditorSceneManager.SaveScene(scene, Escena))
        {
            Debug.LogError("[Menu] No se pudo guardar la escena del menu.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log(t.ToString());
        Escribir(t);

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Escribir(System.Text.StringBuilder t)
    {
        Directory.CreateDirectory(@"C:\Users\abner\AppData\Local\Temp\opencode");
        File.WriteAllText(@"C:\Users\abner\AppData\Local\Temp\opencode\menu_fix.txt", t.ToString());
    }
}
