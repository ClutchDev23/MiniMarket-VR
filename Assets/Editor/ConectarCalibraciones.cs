using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Conecta los scripts de CALIBRACION a la escena de juego:
///
///   - CalibrarBrazos: mide la longitud real de los brazos para el caminar con balanceo.
///   - CalibrarTamanoProducto: aplica a cada producto el tamano elegido en el menu.
///
/// Tambien anade el "Sistemas de Juego" con el menu de pausa si no existe.
///
/// Uso: Tools/Minimarket/Agarrar/Conectar Calibraciones (escena de juego)
/// </summary>
public static class ConectarCalibraciones
{
    const string Escena = "Assets/Scenes/MinimarketXR_Full.unity";

    /// <summary>Si es true, no cierra Unity al terminar (para encadenar herramientas).</summary>
    public static bool noSalir = false;

    [MenuItem("Tools/Minimarket/Agarrar/Conectar Calibraciones (escena de juego)")]
    public static void Ejecutar()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Conexion de calibraciones ===");

        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // ---- 1. CalibrarBrazos (sobre el ArmSwingLocomotion del rig) ----
        var armSwing = Object.FindFirstObjectByType<ArmSwingLocomotion>();
        if (armSwing == null)
        {
            t.AppendLine("  NO se encontro ArmSwingLocomotion: la calibracion de brazos no se puede conectar");
        }
        else
        {
            var cal = armSwing.GetComponent<CalibrarBrazos>();
            if (cal == null) cal = armSwing.gameObject.AddComponent<CalibrarBrazos>();
            cal.armSwing = armSwing;

            // La cabeza: la camara del XR Origin.
            if (cal.head == null)
            {
                var cam = Camera.main;
                if (cam != null) cal.head = cam.transform;
            }
            t.AppendLine("  CalibrarBrazos en '" + armSwing.name + "' | head=" + (cal.head != null ? cal.head.name : "NULL"));
        }

        // ---- 2. CalibrarTamanoProducto en cada producto ----
        // Los productos son hijos anadidos al GameObject "Productos" (no son
        // instancias de prefab), asi que el componente se anade directamente a
        // cada uno.
        //
        // OJO: Unity reserializa la escena y puede perder estos componentes
        // anadidos. Por eso el InstaladorSistemasJuego tambien los comprueba y
        // los anade en tiempo de ejecucion si faltan (esa es la red de seguridad
        // que hace que el tamano de los productos SIEMPRE funcione).
        var root = BuscarRoot(scene, "Productos");
        if (root == null)
        {
            t.AppendLine("  NO se encontro el root 'Productos'");
        }
        else
        {
            var grabs = root.GetComponentsInChildren<XRGrabInteractable>(true);
            int n = 0;
            foreach (var g in grabs)
            {
                if (g.GetComponent<CalibrarTamanoProducto>() == null)
                    g.gameObject.AddComponent<CalibrarTamanoProducto>();
                n++;
            }
            t.AppendLine("  CalibrarTamanoProducto anadido a " + n + " productos");
        }

        // ---- 3. El instalador de sistemas (menu de pausa) ----
        var inst = Object.FindFirstObjectByType<InstaladorSistemasJuego>();
        if (inst == null)
        {
            var go = new GameObject("Sistemas de Juego");
            inst = go.AddComponent<InstaladorSistemasJuego>();
            t.AppendLine("  InstaladorSistemasJuego creado ('Sistemas de Juego')");
        }
        else t.AppendLine("  InstaladorSistemasJuego ya existia");

        // ---- 4. Componentes de calibracion en la escena ----
        var calBrazos = Object.FindFirstObjectByType<CalibrarBrazos>();
        if (calBrazos == null && armSwing != null)
        {
            calBrazos = armSwing.gameObject.AddComponent<CalibrarBrazos>();
            calBrazos.armSwing = armSwing;
        }

        if (!EditorSceneManager.SaveScene(scene, Escena))
        {
            Debug.LogError("[Calibracion] No se pudo guardar la escena.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log(t.ToString());
        Directory.CreateDirectory(@"C:\Users\abner\AppData\Local\Temp\opencode");
        File.WriteAllText(@"C:\Users\abner\AppData\Local\Temp\opencode\calibracion.txt", t.ToString());

        if (Application.isBatchMode && !noSalir) EditorApplication.Exit(0);
    }

    static GameObject BuscarRoot(UnityEngine.SceneManagement.Scene scene, string nombre)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == nombre) return go;
        return null;
    }
}
