using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Tools/Minimarket/Clientes/1 - Configurar personajes y animaciones
///
/// Deja listos los .fbx de Mixamo que pusiste en:
///     Assets/Clientes/Personajes/   (personajes, "With Skin")
///     Assets/Clientes/Animaciones/  (Idle, Walking, Talking, Looking Around)
///
/// Hace:
///   1. Personajes -> rig Humanoid, saca sus texturas a una carpeta y las baja a
///      1024 px (para que el Quest no sufra).
///   2. Animaciones -> rig Humanoid, en bucle y "en el sitio".
///   3. Crea Assets/Clientes/ClienteAnimator.controller con los estados
///      Idle, Walking, Talking y Looking.
///   4. Crea/llena Assets/Resources/ConfigClientes.asset.
///
/// Se puede ejecutar todas las veces que quieras (por ejemplo si agregas otro personaje).
/// </summary>
public static class ClientesConfigurar
{
    const string CarpetaPersonajes = "Assets/Clientes/Personajes";
    const string CarpetaAnimaciones = "Assets/Clientes/Animaciones";
    // OJO: tiene que llamarse "Textures" (en ingles): ahi es donde Unity busca las
    // texturas de un .fbx. Con otro nombre los personajes salen grises.
    const string CarpetaTexturas = "Assets/Clientes/Personajes/Textures";
    const string CarpetaTexturasVieja = "Assets/Clientes/Personajes/Texturas";
    const string RutaAnimator = "Assets/Clientes/ClienteAnimator.controller";
    const string RutaConfig = "Assets/Resources/ConfigClientes.asset";
    const string RutaCamino = "Assets/Clientes/CaminoClientes.asset";

    // estado del Animator -> palabra que debe tener el nombre del archivo
    static readonly (string estado, string archivo)[] Estados =
    {
        ("Idle", "idle"),
        ("Walking", "walk"),
        ("Talking", "talk"),
        ("Looking", "look"),
    };

    [MenuItem("Tools/Minimarket/Clientes/1 - Configurar personajes y animaciones")]
    public static void Ejecutar()
    {
        var log = new StringBuilder();
        // Sin StartAssetEditing: ExtractTextures necesita que el reimport ya este hecho.
        EditorUtility.DisplayProgressBar("Clientes", "Configurando animaciones...", 0.2f);
        try
        {
            ConfigurarAnimaciones(log);
            EditorUtility.DisplayProgressBar("Clientes", "Configurando personajes (puede tardar)...", 0.5f);
            ConfigurarPersonajes(log);
            AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }

        // Las texturas extraidas aparecen despues del Refresh.
        BajarTexturas(log);

        var ctrl = CrearAnimator(log);
        var cfg = CrearConfig(ctrl, log);

        // Si la escena del minimarket esta abierta, hornea tambien el camino.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("MinimarketXR"))
            HornearEn(cfg, log);
        else
            log.AppendLine("OJO: abre MinimarketXR_Full y usa '2 - Hornear camino de clientes'.");

        AssetDatabase.SaveAssets();
        Debug.Log("[Clientes] Configuracion:\n" + log);
        EditorUtility.DisplayDialog("Clientes",
            cfg != null && cfg.personajes != null && cfg.personajes.Length > 0
                ? "Listo: " + cfg.personajes.Length + " personaje(s) y " + ContarClips() + " animacion(es).\n\nDale Play en MinimarketXR_Full."
                : "Algo falto. Mira la Consola.",
            "OK");
        if (cfg != null) Selection.activeObject = cfg;
    }

    // ------------------------------------------------------------------ animaciones
    static void ConfigurarAnimaciones(StringBuilder log)
    {
        foreach (var ruta in Fbx(CarpetaAnimaciones))
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ruta);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;

            var clips = mi.defaultClipAnimations;
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            foreach (var c in clips)
            {
                c.name = nombre;
                c.loopTime = true;
                c.loopPose = true;
                // "Bake into pose": el personaje no se desplaza solo, lo mueve el NavMeshAgent.
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                // Altura basada en los PIES: asi el personaje no flota ni se hunde
                // (con "Original" los pies de Mixamo se ven raros).
                c.keepOriginalPositionY = false;
                c.heightFromFeet = true;
                c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
            log.AppendLine("Animacion OK: " + nombre);
        }
    }

    // ------------------------------------------------------------------ personajes
    static void ConfigurarPersonajes(StringBuilder log)
    {
        // Carpeta de una version anterior de esta herramienta (nombre equivocado).
        if (AssetDatabase.IsValidFolder(CarpetaTexturasVieja))
            AssetDatabase.DeleteAsset(CarpetaTexturasVieja);
        if (!AssetDatabase.IsValidFolder(CarpetaTexturas))
            AssetDatabase.CreateFolder(CarpetaPersonajes, "Textures");

        var rutas = Fbx(CarpetaPersonajes).ToList();
        foreach (var ruta in rutas)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ruta);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importBlendShapes = false;   // menos memoria en Quest
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.isReadable = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
            mi.SaveAndReimport();

            // Saca las texturas incrustadas a Personajes/Textures (para bajarles la resolucion).
            bool ext = mi.ExtractTextures(CarpetaTexturas);
            log.AppendLine("Personaje OK: " + Path.GetFileName(ruta) + (ext ? " (texturas extraidas)" : ""));
        }

        // Reimporta para que los materiales encuentren las texturas extraidas.
        AssetDatabase.Refresh();
        foreach (var ruta in rutas)
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
    }

    static void BajarTexturas(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaTexturas)) return;
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaTexturas }))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (ti == null) continue;
            bool cambio = false;
            if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; cambio = true; }
            string low = Path.GetFileNameWithoutExtension(ruta).ToLowerInvariant();
            if (low.Contains("normal") && ti.textureType != TextureImporterType.NormalMap)
            { ti.textureType = TextureImporterType.NormalMap; cambio = true; }
            if (cambio) { ti.SaveAndReimport(); n++; }
        }
        log.AppendLine("Texturas ajustadas a 1024: " + n);
    }

    // ------------------------------------------------------------------ animator
    static AnimatorController CrearAnimator(StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaAnimator) != null)
            AssetDatabase.DeleteAsset(RutaAnimator);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(RutaAnimator);
        var sm = ctrl.layers[0].stateMachine;

        var clips = Fbx(CarpetaAnimaciones)
            .Select(r => AssetDatabase.LoadAllAssetsAtPath(r).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__")))
            .Where(c => c != null).ToList();

        AnimatorState porDefecto = null;
        foreach (var (estado, archivo) in Estados)
        {
            var clip = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(archivo));
            var st = sm.AddState(estado);
            st.motion = clip;
            st.iKOnFeet = true;   // Foot IK: pies bien apoyados al usar la animacion en otro personaje
            if (estado == "Idle") porDefecto = st;
            log.AppendLine("Estado " + estado + " -> " + (clip != null ? clip.name : "(FALTA la animacion)"));
        }
        if (porDefecto != null) sm.defaultState = porDefecto;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // ------------------------------------------------------------------ config
    static ConfigClientes CrearConfig(RuntimeAnimatorController ctrl, StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var cfg = AssetDatabase.LoadAssetAtPath<ConfigClientes>(RutaConfig);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<ConfigClientes>();
            AssetDatabase.CreateAsset(cfg, RutaConfig);
        }

        var personajes = Fbx(CarpetaPersonajes)
            .Select(r => AssetDatabase.LoadAssetAtPath<GameObject>(r))
            .Where(g => g != null).ToArray();
        cfg.personajes = personajes;
        cfg.animador = ctrl;
        EditorUtility.SetDirty(cfg);

        foreach (var p in personajes)
        {
            int tris = 0;
            foreach (var r in p.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null) tris += r.sharedMesh.triangles.Length / 3;
            log.AppendLine("  " + p.name + ": " + tris + " triangulos");
        }
        return cfg;
    }

    // ------------------------------------------------------------------ camino (NavMesh)
    [MenuItem("Tools/Minimarket/Clientes/2 - Hornear camino de clientes")]
    public static void Hornear()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Clientes", "Deten el Play primero.", "OK"); return; }
        if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("MinimarketXR"))
        {
            EditorUtility.DisplayDialog("Clientes", "Abre la escena MinimarketXR_Full primero.", "OK");
            return;
        }
        var cfg = AssetDatabase.LoadAssetAtPath<ConfigClientes>(RutaConfig);
        if (cfg == null) { EditorUtility.DisplayDialog("Clientes", "Primero usa '1 - Configurar personajes y animaciones'.", "OK"); return; }
        var log = new StringBuilder();
        HornearEn(cfg, log);
        AssetDatabase.SaveAssets();
        Debug.Log("[Clientes] " + log);
        EditorUtility.DisplayDialog("Clientes", log.ToString(), "OK");
    }

    static void HornearEn(ConfigClientes cfg, StringBuilder log)
    {
        EditorUtility.DisplayProgressBar("Clientes", "Horneando el camino de los clientes...", 0.5f);
        try
        {
            var data = GestorClientes.ConstruirCamino();
            if (data == null) { log.AppendLine("No se pudo hornear el camino."); return; }
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(RutaCamino) != null)
                AssetDatabase.DeleteAsset(RutaCamino);
            AssetDatabase.CreateAsset(data, RutaCamino);
            cfg.camino = data;
            EditorUtility.SetDirty(cfg);
            log.AppendLine("Camino horneado: " + RutaCamino);
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static int ContarClips() => Fbx(CarpetaAnimaciones).Count();

    static IEnumerable<string> Fbx(string carpeta)
    {
        if (!AssetDatabase.IsValidFolder(carpeta)) return Enumerable.Empty<string>();
        return AssetDatabase.FindAssets("t:Model", new[] { carpeta })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)
                        && Path.GetDirectoryName(p).Replace('\\', '/') == carpeta)
            .Distinct().OrderBy(p => p);
    }
}

/// <summary>Pruebas de clientes sin visor (solo en Play).</summary>
public static class ClientesPruebas
{
    static GestorClientes Gestor()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Clientes", "Dale Play primero (escena MinimarketXR_Full).", "OK");
            return null;
        }
        var g = Object.FindAnyObjectByType<GestorClientes>();
        if (g == null || !g.Listo) Debug.LogWarning("[Clientes] El gestor aun no esta listo.");
        return g != null && g.Listo ? g : null;
    }

    [MenuItem("Tools/Minimarket/Clientes/3 - Traer un cliente ahora (prueba)")]
    public static void Traer()
    {
        var g = Gestor(); if (g == null) return;
        var c = g.TraerCliente();
        if (c != null) Selection.activeGameObject = c.gameObject;
    }

    [MenuItem("Tools/Minimarket/Clientes/5 - Simular cajero: solo escanear (prueba)")]
    public static void SoloEscanear()
    {
        var g = Gestor(); if (g == null) return;
        g.StartCoroutine(g.SimularCajero(false));
    }

    [MenuItem("Tools/Minimarket/Clientes/4 - Simular cajero: escanear y cobrar (prueba)")]
    public static void Cajero()
    {
        var g = Gestor(); if (g == null) return;
        g.StartCoroutine(g.SimularCajero());
    }
}
