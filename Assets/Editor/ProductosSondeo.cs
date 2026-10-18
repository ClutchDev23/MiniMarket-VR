using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProductosSondeo
{
    const string Fbx = "Assets/Productos/Models/Super market Models FBX File.fbx";

    static readonly string[] Escenas =
    {
        "Assets/Scenes/MinimarketXR_Full.unity",
        "Assets/Scenes/MinimarketXR_Night.unity",
    };

    static readonly Dictionary<string, string> Estantes = new Dictionary<string, string>
    {
        { "StandingRack01", "29ccca18fe20f1046b1caffc3c0ee1d3" },
        { "StandingRack02", "1af4fd586da010d4a8f8ad5acc6f242d" },
        { "WallRack01",     "f84f1190f7f24d14fb466863458dd85a" },
        { "WallRack02",     "07f507d2b0b6df544af029236fd8dc50" },
        { "WallRack03",     "8d362f2a5669e474d83aa927e71fa1ea" },
        { "CrateStand01",   "b91930e356b912e4dbf98a0ee09f7d33" },
        { "CrateStand_Load","56b2cf03f360f0f498da30f9f9b95258" },
        { "CashCounter",    "76296b567362f34458915f7f26c7b861" },
        { "Freezer",        "4fc55f4386fce24448a635fded679593" },
    };

    [MenuItem("Tools/Minimarket/Productos/Sondeo")]
    public static void Ejecutar()
    {
        int errores = 0;
        Debug.Log("[Sondeo] iniciando");

        try { SondearFbx(); }
        catch (System.Exception e) { Debug.LogError("[Sondeo] EX fbx: " + e); errores++; }

        foreach (var path in Escenas)
        {
            try { SondearEscena(path); }
            catch (System.Exception e) { Debug.LogError("[Sondeo] EX " + path + ": " + e); errores++; }
        }

        Debug.Log(errores == 0 ? "[Sondeo] OK" : "[Sondeo] ERRORES " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static void SondearFbx()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(Fbx) == null)
        {
            Debug.LogError("[Sondeo] FBX no encontrado: " + Fbx);
            return;
        }

        var assets = AssetDatabase.LoadAllAssetsAtPath(Fbx);
        Debug.Log("[Sondeo] FBX sub-assets: " + assets.Length);

        int meshes = 0, mats = 0, otros = 0;
        foreach (var a in assets)
        {
            if (a is Mesh m)
            {
                meshes++;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string g, out long id);
                Debug.Log($"[Sondeo] MESH '{a.name}' id={id} size={m.bounds.size.ToString("F4")} verts={m.vertexCount}");
            }
            else if (a is Material mt)
            {
                mats++;
                var tex = mt.mainTexture;
                Debug.Log($"[Sondeo] MAT '{a.name}' tex={(tex == null ? "NINGUNA" : tex.name)} shader={mt.shader.name}");
            }
            else { otros++; Debug.Log($"[Sondeo] OTRO '{a.name}' ({a.GetType().Name})"); }
        }
        Debug.Log($"[Sondeo] FBX resumen: meshes={meshes} materiales={mats} otros={otros}");
    }

    static void SondearEscena(string path)
    {
        if (!System.IO.File.Exists(path)) { Debug.LogError("[Sondeo] falta escena " + path); return; }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Debug.Log("[Sondeo] escena " + path);

        var encontrados = new Dictionary<string, List<Transform>>();
        foreach (var k in Estantes.Keys) encontrados[k] = new List<Transform>();

        var todo = new List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
            todo.AddRange(root.GetComponentsInChildren<Transform>(true));

        int conRb = 0, conGrab = 0;
        foreach (var t in todo)
        {
            if (t.GetComponent<Rigidbody>() != null) conRb++;
            if (t.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() != null) conGrab++;

            var instPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t);
            if (string.IsNullOrEmpty(instPath)) continue;
            var g = AssetDatabase.AssetPathToGUID(instPath);
            foreach (var kv in Estantes)
                if (kv.Value == g) { encontrados[kv.Key].Add(t); break; }
        }

        foreach (var kv in Estantes)
        {
            var lista = encontrados[kv.Key];
            if (lista.Count == 0) { Debug.Log($"[Sondeo] ESTANTE {kv.Key}: NO ENCONTRADO"); continue; }

            foreach (var t in lista)
            {
                var cols = t.GetComponentsInChildren<Collider>(true);
                if (cols.Length == 0)
                {
                    Debug.Log($"[Sondeo] ESTANTE {kv.Key} @ {t.position.ToString("F3")} SIN COLLIDERS");
                    continue;
                }
                var b = cols[0].bounds;
                for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
                Debug.Log($"[Sondeo] ESTANTE {kv.Key} '{t.name}' pos={t.position.ToString("F3")} boundsC={b.center.ToString("F3")} size={b.size.ToString("F3")} cols={cols.Length} hijos={t.childCount}");
            }
        }

        Debug.Log($"[Sondeo] resumen escena: transforms={todo.Count} conRigidbody={conRb} conGrab={conGrab}");
    }
}
