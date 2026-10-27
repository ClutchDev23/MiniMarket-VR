using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Tools/Minimarket/Productos/Verificar Precios
/// Revisa la escena abierta y dice que productos NO tienen precio en
/// Assets/Resources/BaseDatosProductos.json (y cuales del JSON no estan en la escena).
/// </summary>
public static class ProductosVerificarPrecios
{
    [MenuItem("Tools/Minimarket/Productos/Verificar Precios")]
    public static void Verificar()
    {
        BaseDatosProductos.Recargar();

        var raiz = GameObject.Find("Productos");
        if (raiz == null)
        {
            EditorUtility.DisplayDialog("Verificar precios", "No hay un objeto 'Productos' en la escena abierta.", "OK");
            return;
        }

        var faltan = new SortedDictionary<string, int>();
        var enEscena = new HashSet<string>();
        int conPrecio = 0;

        foreach (var g in raiz.GetComponentsInChildren<XRGrabInteractable>(true))
        {
            string clave = BaseDatosProductos.Normalizar(g.name);
            enEscena.Add(clave);
            if (BaseDatosProductos.Buscar(g.name) != null) conPrecio++;
            else faltan[clave] = faltan.TryGetValue(clave, out var n) ? n + 1 : 1;
        }

        var sinUso = BaseDatosProductos.Todos
            .Where(p => !enEscena.Contains(BaseDatosProductos.Normalizar(p.clave)))
            .Select(p => p.clave).ToList();

        Debug.Log("[Precios] Objetos con precio: " + conPrecio + " | tipos sin precio: " + faltan.Count +
                  " | en el JSON pero no en la escena: " + sinUso.Count);
        foreach (var kv in faltan)
            Debug.LogWarning("[Precios] SIN PRECIO: '" + kv.Key + "' (" + kv.Value + " en la escena)");
        foreach (var c in sinUso)
            Debug.Log("[Precios] En el JSON pero no en la escena: '" + c + "'");

        string msg = faltan.Count == 0
            ? "Todos los productos de la escena tienen precio (" + conPrecio + " objetos)."
            : faltan.Count + " tipo(s) de producto sin precio. Mira la Consola para la lista.";
        EditorUtility.DisplayDialog("Verificar precios", msg, "OK");
    }

    [MenuItem("Tools/Minimarket/Productos/Abrir Base de Datos (JSON)")]
    public static void Abrir()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/BaseDatosProductos.json");
        if (asset != null) AssetDatabase.OpenAsset(asset);
        else Debug.LogError("[Precios] No existe Assets/Resources/BaseDatosProductos.json");
    }
}

/// <summary>
/// Tools/Minimarket/Caja/...  Pruebas de la caja SIN visor (solo en Play).
/// </summary>
public static class ProductosProbarCaja
{
    static CajaRegistradora Caja()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Probar caja", "Dale Play primero (escena MinimarketXR_Full).", "OK");
            return null;
        }
        var caja = Object.FindAnyObjectByType<CajaRegistradora>();
        if (caja == null) Debug.LogError("[Caja] No hay CajaRegistradora en la escena.");
        return caja;
    }

    static void Enfocar(CajaRegistradora caja)
    {
        var p = caja.transform.Find("Pantalla Caja");
        if (p == null) return;
        Selection.activeGameObject = p.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    [MenuItem("Tools/Minimarket/Caja/1 - Escanear 5 productos (prueba)")]
    public static void Escanear()
    {
        var c = Caja(); if (c == null) return;
        c.ProbarEscaneo(5);
        Enfocar(c);
    }

    [MenuItem("Tools/Minimarket/Caja/2 - Cliente paga con billete y cobrar (prueba)")]
    public static void Cobrar()
    {
        var c = Caja(); if (c == null) return;
        c.ProbarCobro();
        Enfocar(c);
    }

    [MenuItem("Tools/Minimarket/Caja/3 - Siguiente cliente")]
    public static void Siguiente()
    {
        var c = Caja(); if (c == null) return;
        c.NuevaVenta();
        Enfocar(c);
    }
}
