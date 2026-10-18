using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EscenaNoche
{
    const string DiaPath = "Assets/Scenes/MinimarketXR_Full.unity";
    const string NochePath = "Assets/Scenes/MinimarketXR_Night.unity";
    const string PackNightPath = "Assets/SuperMarket/Scenes/SuperMarket_Night_01.unity";
    const string PrefijoLuces = "[NocheLuz] ";

    [MenuItem("Tools/Minimarket/Noche/Crear Escena Noche")]
    public static void Crear()
    {
        Debug.Log("[Noche] Iniciando creacion...");
        int errores = 0;
        Scene noche;

        if (System.IO.File.Exists(NochePath))
        {
            noche = EditorSceneManager.OpenScene(NochePath, OpenSceneMode.Single);
            Debug.Log("[Noche] escena ya existia, se limpian luces previas y re-aplican ajustes");
            foreach (var root in noche.GetRootGameObjects())
                if (root.name.StartsWith(PrefijoLuces))
                    Object.DestroyImmediate(root);
        }
        else
        {
            EditorSceneManager.OpenScene(DiaPath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), NochePath))
            {
                Debug.LogError("[Noche] fallo duplicar escena");
                EditorApplication.Exit(1);
                return;
            }
            noche = SceneManager.GetActiveScene();
            Debug.Log("[Noche] escena duplicada -> " + NochePath);
        }

        var pack = EditorSceneManager.OpenScene(PackNightPath, OpenSceneMode.Additive);
        if (!EditorSceneManager.SetActiveScene(pack))
        {
            Debug.LogError("[Noche] fallo SetActiveScene pack");
            EditorApplication.Exit(1);
            return;
        }

        Material skybox = RenderSettings.skybox;
        var ambMode = RenderSettings.ambientMode;
        var ambSky = RenderSettings.ambientSkyColor;
        var ambEq = RenderSettings.ambientEquatorColor;
        var ambGnd = RenderSettings.ambientGroundColor;
        float ambInt = RenderSettings.ambientIntensity;
        bool fog = RenderSettings.fog;
        var fogCol = RenderSettings.fogColor;
        var fogMode = RenderSettings.fogMode;
        float fogDen = RenderSettings.fogDensity;
        float fogStart = RenderSettings.fogStartDistance;
        float fogEnd = RenderSettings.fogEndDistance;
        float reflInt = RenderSettings.reflectionIntensity;
        int reflBounces = RenderSettings.reflectionBounces;
        Light sunPack = RenderSettings.sun;

        var lucesPack = new List<Light>();
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.gameObject.scene == pack)
                lucesPack.Add(l);

        Light dirPack = sunPack != null ? sunPack : lucesPack.Find(l => l.type == LightType.Directional);
        var extrasPack = lucesPack.FindAll(l => l.type != LightType.Directional);
        Debug.Log("[Noche] pack: direccional=" + (dirPack ? dirPack.name : "NINGUNA") + " extras=" + extrasPack.Count);

        EditorSceneManager.SetActiveScene(noche);

        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = ambMode;
        RenderSettings.ambientSkyColor = ambSky;
        RenderSettings.ambientEquatorColor = ambEq;
        RenderSettings.ambientGroundColor = ambGnd;
        RenderSettings.ambientIntensity = ambInt;
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogCol;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDen;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.reflectionIntensity = reflInt;
        RenderSettings.reflectionBounces = reflBounces;

        Light dirMia = null;
        foreach (var root in noche.GetRootGameObjects())
        {
            foreach (var l in root.GetComponentsInChildren<Light>(true))
                if (l.type == LightType.Directional) { dirMia = l; break; }
            if (dirMia != null) break;
        }

        if (dirMia != null && dirPack != null)
        {
            CopiarValores(dirPack, dirMia);
            dirMia.enabled = dirPack.enabled;
            RenderSettings.sun = dirMia;
            Debug.Log("[Noche] direccional ajustada: " + dirMia.name + " color=" + dirMia.color + " intensidad=" + dirMia.intensity);
        }
        else
        {
            Debug.LogError("[Noche] falta luz direccional (mia=" + (dirMia != null) + ", pack=" + (dirPack != null) + ")");
            errores++;
        }

        int puestos = 0;
        foreach (var lp in extrasPack)
        {
            var g = new GameObject(PrefijoLuces + lp.name);
            g.transform.SetPositionAndRotation(lp.transform.position, lp.transform.rotation);
            g.SetActive(lp.gameObject.activeSelf);
            var nl = g.AddComponent<Light>();
            CopiarValores(lp, nl);
            nl.enabled = lp.enabled;

            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, noche);
            if (g.scene != noche)
            {
                Debug.LogError("[Noche] fallo mover luz: " + lp.name);
                Object.DestroyImmediate(g);
                errores++;
                continue;
            }
            puestos++;
        }
        Debug.Log("[Noche] luces copiadas: " + puestos);

        EditorSceneManager.CloseScene(pack, false);

        if (!EditorSceneManager.SaveScene(noche))
        {
            Debug.LogError("[Noche] fallo SaveScene");
            errores++;
        }

        var escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool tieneDia = false, tieneNoche = false;
        foreach (var e in escenas)
        {
            if (e.path == DiaPath) tieneDia = true;
            if (e.path == NochePath) tieneNoche = true;
        }
        if (!tieneDia) escenas.Insert(0, new EditorBuildSettingsScene(DiaPath, true));
        if (!tieneNoche) escenas.Add(new EditorBuildSettingsScene(NochePath, true));
        EditorBuildSettings.scenes = escenas.ToArray();

        Debug.Log(errores == 0 ? "[Noche] CREACION OK" : "[Noche] CREACION CON ERRORES: " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    [MenuItem("Tools/Minimarket/Noche/Hornear Escena Noche")]
    public static void Hornear()
    {
        Debug.Log("[Noche] Iniciando hornada...");
        EditorSceneManager.OpenScene(NochePath, OpenSceneMode.Single);
        Lightmapping.Clear();
        bool ok = Lightmapping.Bake();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), NochePath);
        Debug.Log(ok ? "[Noche] HORNEADO OK" : "[Noche] HORNEADO FALLO");
        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    static void CopiarValores(Component origen, Component destino)
    {
        EditorUtility.CopySerialized(origen, destino);
    }
}
