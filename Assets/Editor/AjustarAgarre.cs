using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Corrige el angulo de agarre de los PRODUCTOS de las escenas de juego.
///
/// DIAGNOSTICO
/// ------------
/// Las escenas MinimarketXR_Full / MinimarketXR_Night tienen 320 productos colgados del
/// root "Productos". Cada producto tiene un XRGrabInteractable con:
///
///   m_UseDynamicAttach: 0        <-- el problema
///   m_AttachTransform:  {fileID: 0}  (ninguno)
///
/// Con "Use Dynamic Attach" apagado, XRI no crea un attach point por interactor, asi que
/// el offset de agarre se mide contra el propio transform del producto. La rotacion
/// objetivo termina siendo la del interactor en frame absoluto en vez de "la pose que
/// tenias al agarrarlo + el delta de la mano". De ahi los dos sintomas que describes:
///
///   - el objeto salta a un angulo fijo al agarrarlo
///   - no sigue la direccion real de la mano
///
/// Encendiendo "Use Dynamic Attach" (+ Match Attach Position/Rotation) XRI crea un
/// attach point dinamico por interactor cuya pose inicial se copia de la mano en el
/// instante del agarre. El producto conserva su pose relativa y a partir de ahi sigue
/// al mando de forma natural.
///
/// La escena Menu (que ya te funciona) tiene justo esa combinacion:
///   m_UseDynamicAttach: 1, m_MatchAttachPosition: 1, m_MatchAttachRotation: 1
///
/// La otra diferencia con Menu: ahi smoothPosition/smoothRotation estan en 0 (sin
/// suavizado). Con suavizado activado el objeto va "detras" de la mano y el giro se
/// siente laggy, asi que tambien lo apagamos.
///
/// QUE NO SE TOCA
/// --------------
/// Solo los objetos cuelgan del root "Productos". Los estantes (prefabs), los huecos
/// vacios (los 642 XRSocketInteractor del root "Sockets") y la escena Menu se quedan
/// exactamente como estan.
///
/// Uso: Tools/Minimarket/Agarrar/Ajustar Angulo de Agarre (escenas de juego)
/// </summary>
public static class AjustarAgarre
{
    const string RootProductos = "Productos";

    static readonly string[] EscenasJuego =
    {
        "Assets/Scenes/MinimarketXR_Full.unity",
    };

    [MenuItem("Tools/Minimarket/Agarrar/Ajustar Angulo de Agarre (escenas de juego)")]
    public static void Ejecutar()
    {
        int errores = 0;

        foreach (var path in EscenasJuego)
        {
            try { ProcesarEscena(path); }
            catch (System.Exception e) { Debug.LogError("[Agarre] EX " + path + ": " + e); errores++; }
        }

        Debug.Log(errores == 0 ? "[Agarre] TODO LISTO" : "[Agarre] TERMINO CON ERRORES: " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static void ProcesarEscena(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("[Agarre] falta escena " + path);
            return;
        }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        var root = BuscarRoot(scene, RootProductos);
        if (root == null)
        {
            Debug.LogError("[Agarre] no se encontro el root '" + RootProductos + "' en " + path);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // Solo los productos colgando del root "Productos".
        var productos = root.GetComponentsInChildren<XRGrabInteractable>(true);
        int ajustes = 0;

        foreach (var grab in productos)
        {
            if (grab == null) continue;

            // Attach point dinamico por interactor: el producto se ancla donde lo
            // agarraste y a partir de ahi sigue al mando con la pose relativa intacta.
            grab.useDynamicAttach = true;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            grab.snapToColliderVolume = true;

            // Sin suavizado: el objeto pega a la pose del mando sin retardo, que es
            // lo que hace que el giro se sienta "en la direccion de la mano".
            grab.smoothPosition = false;
            grab.smoothRotation = false;

            // Track de posicion y rotacion explicitos.
            grab.trackPosition = true;
            grab.trackRotation = true;

            // Ease-in minimo: sin esto el objeto da un salto visible al agarrarlo.
            grab.attachEaseInTime = 0.05f;

            ajustes++;
        }

        // Los sockets (root "Sockets") se dejan intactos a proposito: son los huecos
        // vacios de los estantes y no participan del agarre directo. Solo se avisa de
        // cuantos hay por si en algun momento hay que tocarlos.
        int sockets = 0;
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "Sockets")
                sockets = go.GetComponentsInChildren<XRSocketInteractor>(true).Length;

        Debug.Log("[Agarre] " + Path.GetFileName(path) + ": productos ajustados = " + ajustes +
                  " | sockets (sin tocar) = " + sockets);

        if (!EditorSceneManager.SaveScene(scene, path))
        {
            Debug.LogError("[Agarre] fallo SaveScene " + path);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    static GameObject BuscarRoot(UnityEngine.SceneManagement.Scene scene, string nombre)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == nombre)
                return go;
        return null;
    }
}
