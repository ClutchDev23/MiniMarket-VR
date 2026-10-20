using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Pone el LETRERO de fuera de la tienda: "minimarket chill".
///
/// POR QUE HAY QUE HACERLO ASI
/// ----------------------------
/// El exterior de la tienda (Pref_360_Sm, que usa el modelo 360.fbx) tiene el
/// texto del letrero HORNEADO en la malla (colores por vertice), asi que no hay
/// ninguna textura ni texto que editar.
///
/// Pero el 360 tiene DOS materiales: uno normal y otro EMISIVO. Las letras
/// iluminadas del letrero son justo la parte emisiva, asi que se puede localizar
/// su rectangulo y poner un cartel nuevo ENCIMA, con el nombre nuevo.
///
/// QUE HACE ESTE
/// --------------
/// 1. Localiza las letras del letrero (la parte emisiva del modelo 360) y
///    calcula su rectangulo.
/// 2. Crea un cartel (un plano con la textura) justo delante, del mismo tamaño.
/// 3. Lo deja como hijo del objeto 360, para que se mueva con el.
///
/// Uso: Tools/Minimarket/Agarrar/Poner letrero Minimarket Chill
/// </summary>
public static class PonerLetrero
{
    const string Escena = "Assets/Scenes/MinimarketXR_Full.unity";
    const string Textura = "Assets/SuperMarket/Textures/LetreroMinimarketChill.png";
    const string Nombre = "Letrero Minimarket Chill";
    const string NombreMaterial = "LetreroMinimarketChill_Mat";

    /// <summary>Si es true, no cierra Unity al terminar (para encadenar herramientas).</summary>
    public static bool noSalir = false;

    [MenuItem("Tools/Minimarket/Agarrar/Poner letrero Minimarket Chill (fuera)")]
    public static void Ejecutar()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Letrero de fuera: minimarket chill ===");

        // ------------------------------------------------ 1. donde estan las letras
        var prefab360 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SuperMarket/Prefabs/Pref_360_Sm.prefab");
        if (prefab360 == null) { t.AppendLine("  no se encuentra Pref_360_Sm"); Escribir(t); return; }

        var mf = prefab360.GetComponentInChildren<MeshFilter>(true);
        if (mf == null || mf.sharedMesh == null) { t.AppendLine("  el 360 no tiene malla"); Escribir(t); return; }

        var malla = mf.sharedMesh;
        var mats = prefab360.GetComponentInChildren<MeshRenderer>(true).sharedMaterials;

        for (int s = 0; s < malla.subMeshCount; s++)
        {
            var b = BoundsSubmalla(malla, s);
            string nombreMat = (s < mats.Length && mats[s] != null) ? mats[s].name : "?";
            t.AppendLine("  submalla " + s + " material='" + nombreMat + "' bounds centro=" + b.center + " tam=" + b.size);
        }

        // La parte emisiva son las letras.
        int idxLetras = -1;
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            if (s < mats.Length && mats[s] != null && mats[s].name.ToLowerInvariant().Contains("emi"))
            { idxLetras = s; break; }
        }

        if (idxLetras < 0)
        {
            t.AppendLine("  no se ha encontrado la parte emisiva: se usa la submalla 0");
            idxLetras = 0;
        }

        Bounds letras = BoundsSubmalla(malla, idxLetras);
        t.AppendLine("  rectangulo de las letras: centro=" + letras.center + " tam=" + letras.size);

        // ------------------------------------------------ 2. la textura
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Textura);
        if (tex == null)
        {
            t.AppendLine("  FALTA la textura del letrero en " + Textura);
            Escribir(t);
            Debug.LogError(t.ToString());
            return;
        }

        // ------------------------------------------------ 3. el cartel en la escena
        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // Se borra el cartel anterior, si lo hubiera.
        var previos = new List<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            if (go.name == Nombre) previos.Add(go);
        for (int i = 0; i < previos.Count; i++) Object.DestroyImmediate(previos[i]);

        // El ancla: el objeto 360 de la escena.
        var ancla = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        Transform padre = null;
        foreach (var go in ancla)
            if (go.name.Contains("360")) { padre = go.transform; break; }

        // El cartel.
        var cartel = new GameObject(Nombre);
        cartel.transform.SetParent(padre, false);

        // La cara del cartel: la normal es el eje mas fino del rectangulo, y mira
        // hacia fuera (alejandose del centro de la tienda).
        Vector3 tam = letras.size;
        Vector3 normal = Vector3.forward;
        if (tam.x <= tam.y && tam.x <= tam.z) normal = Vector3.right;
        else if (tam.z <= tam.y && tam.z <= tam.x) normal = Vector3.forward;
        else normal = Vector3.up;

        if (padre != null)
        {
            // Que mire hacia fuera del centro de la tienda.
            Vector3 fuera = cartel.transform.position - padre.position;
            fuera.y = 0f;
            if (fuera.sqrMagnitude > 0.0001f)
            {
                fuera.Normalize();
                // La normal tiene que ser perpendicular a la direccion de fuera: se
                // toma la componente perpendicular mas cercana a la del modelo.
                Vector3 perpendicular = Vector3.Cross(Vector3.up, fuera).normalized;
                if (Vector3.Dot(perpendicular, normal) < 0f) perpendicular = -perpendicular;
                normal = perpendicular;
            }
        }

        Vector3 arriba = Vector3.Cross(normal, Vector3.right).normalized;
        if (Vector3.Dot(arriba, Vector3.up) < 0f) arriba = -arriba;
        if (Mathf.Abs(Vector3.Dot(arriba, Vector3.up)) < 0.9f) arriba = Vector3.up;

        cartel.transform.rotation = Quaternion.LookRotation(-normal, arriba);

        // Tamano: un poco mas grande que las letras viejas, para taparlas del
        // todo (y que no asome ni un borde).
        float ancho = Mathf.Max(tam.x, tam.z) * 1.05f;
        float alto = tam.y * 1.08f;

        cartel.transform.localPosition = Vector3.zero;

        // La malla: un plano con UVs 0..1.
        var filtro = cartel.AddComponent<MeshFilter>();
        var renderer = cartel.AddComponent<MeshRenderer>();
        filtro.sharedMesh = CrearPlano(ancho, alto);

        // El material.
        var mat = CrearMaterial(tex);
        renderer.sharedMaterial = mat;

        // Colocarlo justo delante del panel (el frente del panel esta en z=1.77).
        Vector3 posLetras = Cartear(cartel.transform, mf, letras, padre);
        cartel.transform.position = posLetras + normal * 0.04f;

        t.AppendLine("  cartel creado en '" + cartel.name + "'");
        t.AppendLine("     rot=" + cartel.transform.eulerAngles + " pos=" + cartel.transform.position);
        t.AppendLine("     tam=" + ancho + " x " + alto + " normal=" + normal);
        t.AppendLine("     material=" + mat.name);

        if (!EditorSceneManager.SaveScene(scene, Escena))
        {
            Debug.LogError("[Letrero] No se pudo guardar la escena.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log(t.ToString());
        Escribir(t);
        if (Application.isBatchMode && !noSalir) EditorApplication.Exit(0);
    }

    /// <summary>
    /// Traduce el rectangulo de las letras (en el espacio del modelo) al mundo.
    /// </summary>
    static Vector3 Cartear(Transform cartel, MeshFilter mf, Bounds letras, Transform padre)
    {
        if (padre == null) return cartel.position;

        // Los vertices del modelo estan en el espacio del MeshFilter.
        Vector3 local = mf.transform.InverseTransformPoint(letras.center);
        return mf.transform.TransformPoint(local);
    }

    /// <summary>
    /// El rectangulo que ocupan los triangulos de una submalla.
    /// </summary>
    static Bounds BoundsSubmalla(Mesh malla, int sub)
    {
        var indices = malla.GetTriangles(sub);
        var verts = malla.vertices;

        if (indices == null || indices.Length == 0)
            return new Bounds(malla.bounds.center, Vector3.zero);

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (int i in indices)
        {
            if (i < 0 || i >= verts.Length) continue;
            Vector3 v = verts[i];
            if (v.x < min.x) min.x = v.x;
            if (v.y < min.y) min.y = v.y;
            if (v.z < min.z) min.z = v.z;
            if (v.x > max.x) max.x = v.x;
            if (v.y > max.y) max.y = v.y;
            if (v.z > max.z) max.z = v.z;
        }

        var b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    /// <summary>
    /// Un plano con las UVs 0..1, para poder poner la textura del texto.
    /// </summary>
    static Mesh CrearPlano(float ancho, float alto)
    {
        var m = new Mesh();
        m.name = "Plano Letrero";
        m.vertices = new[]
        {
            new Vector3(-ancho * 0.5f, -alto * 0.5f, 0f),
            new Vector3( ancho * 0.5f, -alto * 0.5f, 0f),
            new Vector3( ancho * 0.5f,  alto * 0.5f, 0f),
            new Vector3(-ancho * 0.5f,  alto * 0.5f, 0f),
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// Material sin luz con la textura, para que el texto se vea igual de claro
    /// haya la luz que haya.
    /// </summary>
    static Material CrearMaterial(Texture2D tex)
    {
        string ruta = "Assets/SuperMarket/Materials/" + NombreMaterial + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);

        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");

            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, ruta);
        }

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

        // El panel es opaco (tapa el letrero viejo) y el texto va encima, asi que
        // el material no necesita transparencia.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 1);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static void Escribir(System.Text.StringBuilder t)
    {
        Directory.CreateDirectory(@"C:\Users\abner\AppData\Local\Temp\opencode");
        File.WriteAllText(@"C:\Users\abner\AppData\Local\Temp\opencode\letrero_fix.txt", t.ToString());
    }
}
