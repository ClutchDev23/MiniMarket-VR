using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools/Minimarket/Voz/Configurar Wit.ai
///
/// Guarda el "Client Access Token" de Wit.ai en Assets/Resources/wit_token.txt para
/// que en el Quest se pueda hablar con los clientes (VozWit). El archivo esta en
/// .gitignore: no se sube a GitHub.
///
/// Pasos (una sola vez):
///   1. Entra a https://wit.ai con tu cuenta de Meta.
///   2. "New App" -> idioma Spanish -> crear.
///   3. Management -> Settings -> copia el "Client Access Token".
///   4. Pegalo aqui y Guardar.
/// </summary>
public class ConfigurarWit : EditorWindow
{
    const string Ruta = "Assets/Resources/wit_token.txt";
    string token = "";

    [MenuItem("Tools/Minimarket/Voz/Configurar Wit.ai")]
    static void Abrir()
    {
        var w = GetWindow<ConfigurarWit>(true, "Voz en el Quest (Wit.ai)");
        w.minSize = new Vector2(460, 230);
        if (File.Exists(Ruta)) w.token = File.ReadAllText(Ruta).Trim();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Para hablar con los clientes en el Quest sin PC:\n" +
            "1. Entra a wit.ai con tu cuenta de Meta.\n" +
            "2. New App -> idioma Spanish -> crear.\n" +
            "3. Management -> Settings -> copia el Client Access Token.\n" +
            "4. Pegalo abajo y dale Guardar.\n\n" +
            "En el juego: manten el stick izquierdo presionado (o la tecla V) y habla.",
            MessageType.Info);
        token = EditorGUILayout.TextField("Client Access Token", token);
        GUILayout.Space(8);
        if (GUILayout.Button("Guardar"))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta));
            File.WriteAllText(Ruta, token.Trim());
            AssetDatabase.Refresh();
            Debug.Log("[Voz] Token de Wit.ai guardado (no se sube a GitHub).");
            Close();
        }
        if (GUILayout.Button("Abrir wit.ai")) Application.OpenURL("https://wit.ai/apps");
    }
}
