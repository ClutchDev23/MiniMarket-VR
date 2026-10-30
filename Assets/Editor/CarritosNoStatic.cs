using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools/Minimarket/Carritos/Quitar static a los carritos
/// Los carritos estaban marcados "Static" (Marcar Statics y Hornear): Unity los junta en
/// un solo mesh y ya NO se pueden mover, por eso los clientes "tomaban" el carrito pero
/// este se quedaba quieto. Les quita el static en las escenas de dia y noche.
/// </summary>
public static class CarritosNoStatic
{
    [MenuItem("Tools/Minimarket/Carritos/Quitar static a los carritos")]
    public static void Quitar()
    {
        var log = new StringBuilder();
        string activa = EditorSceneManager.GetActiveScene().path;
        foreach (var ruta in new[] { "Assets/Scenes/MinimarketXR_Full.unity", "Assets/Scenes/MinimarketXR_Night.unity" })
        {
            var esc = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool carrito = false;
                for (var x = t; x != null; x = x.parent) if (x.name.Contains("Trolley")) { carrito = true; break; }
                if (!carrito || GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(esc);
            EditorSceneManager.SaveScene(esc);
            log.AppendLine(ruta + ": " + n + " objetos de carrito sin static");
        }
        if (!string.IsNullOrEmpty(activa)) EditorSceneManager.OpenScene(activa, OpenSceneMode.Single);
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Logs", "carritos_static.txt"), log.ToString());
        Debug.Log("[Carritos] " + log);
    }
}
