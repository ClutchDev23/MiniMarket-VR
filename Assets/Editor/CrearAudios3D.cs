using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CrearAudios3D
{
    const string ScenePath = "Assets/Scenes/MinimarketXR_Full.unity";
    const string AudioFolder = "Assets/Audio";

    [MenuItem("Tools/Minimarket/Crear Audios 3D")]
    public static void Crear()
    {
        Debug.Log("[CrearAudios3D] Iniciando...");

        var heladera = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/Heladera_Loop.wav");
        var puerta = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/Puerta_DingDong.wav");
        var caja = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/Caja_Bip.wav");

        if (heladera == null || puerta == null || caja == null)
        {
            Debug.LogError("[CrearAudios3D] Faltan clips en " + AudioFolder);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var heladeraGO = CrearFuente("Audio_Ambiente_Heladera", heladera,
            new Vector3(0f, 1.5f, -12f), 20f, true, true, 0.6f);
        var puertaGO = CrearFuente("Audio_Puerta", puerta,
            new Vector3(0f, 2f, 1.79f), 15f, false, false, 1f);
        var cajaGO = CrearFuente("Audio_Caja", caja,
            new Vector3(-3.55f, 1.2f, -4.4f), 15f, false, false, 1f);

        var doorTrig = GameObject.Find("DoorTrig_01");
        if (doorTrig != null)
        {
            var trigger = doorTrig.GetComponent<TriggerAudio>();
            if (trigger == null)
                trigger = doorTrig.AddComponent<TriggerAudio>();
            trigger.audioSource = puertaGO.GetComponent<AudioSource>();
            trigger.clip = puerta;

            var col = doorTrig.GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                col.isTrigger = true;
            Debug.Log("[CrearAudios3D] TriggerAudio conectado en DoorTrig_01");
        }
        else
        {
            Debug.LogWarning("[CrearAudios3D] No se encontro DoorTrig_01");
        }

        if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath))
        {
            Debug.LogError("[CrearAudios3D] Falló SaveScene");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[CrearAudios3D] OK: 3 AudioSources 3D creados y escena guardada");
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    static GameObject CrearFuente(string nombre, AudioClip clip, Vector3 pos,
        float maxDistance, bool loop, bool playOnAwake, float volume)
    {
        var go = new GameObject(nombre);
        go.transform.position = pos;

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.playOnAwake = playOnAwake;
        src.loop = loop;
        src.volume = volume;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = 1f;
        src.maxDistance = maxDistance;
        src.dopplerLevel = 0f;

        Debug.Log("[CrearAudios3D] creado " + nombre + " en " + pos);
        return go;
    }
}
