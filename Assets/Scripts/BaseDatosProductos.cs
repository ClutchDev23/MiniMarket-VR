using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Un producto de la base de datos.
/// </summary>
[Serializable]
public class Producto
{
    [Tooltip("Codigo de barras (EAN-13).")]
    public string codigo;

    [Tooltip("Nombre del objeto en la escena (sin el ' (1)', ' (2)'...). Asi se enlaza el modelo 3D con su precio.")]
    public string clave;

    [Tooltip("Nombre que se muestra en la caja.")]
    public string nombre;

    public string categoria;

    [Tooltip("Precio en soles.")]
    public float precio;

    public int stock;
}

/// <summary>
/// BASE DE DATOS DE PRODUCTOS
/// --------------------------
/// Carga Assets/Resources/BaseDatosProductos.json (funciona en el editor y en el APK).
///
/// Para cambiar precios, nombres o agregar productos, edita ese JSON. El campo
/// "clave" debe ser el nombre del objeto en la escena (ej. "chips 3"); las copias
/// "chips 3 (1)", "chips 3 (2)"... usan el mismo precio automaticamente.
///
/// Uso desde cualquier script:
///     var p = BaseDatosProductos.Buscar(gameObject.name);
///     if (p != null) Debug.Log(p.nombre + " " + BaseDatosProductos.Formatear(p.precio));
/// </summary>
public static class BaseDatosProductos
{
    public const string RutaResources = "BaseDatosProductos";

    [Serializable]
    class Archivo
    {
        public string moneda = "S/";
        public Producto[] productos = new Producto[0];
    }

    static Dictionary<string, Producto> porClave;
    static Dictionary<string, Producto> porCodigo;
    static List<Producto> todos;

    public static string Moneda { get; private set; } = "S/";

    public static IReadOnlyList<Producto> Todos
    {
        get { AsegurarCarga(); return todos; }
    }

    static readonly Regex SufijoCopia = new Regex(@"(\s*\(\d+\))+$|\s*\(Clone\)$", RegexOptions.Compiled);

    /// <summary>"Chips 3 (2)" -> "chips 3"</summary>
    public static string Normalizar(string nombreObjeto)
    {
        if (string.IsNullOrEmpty(nombreObjeto)) return "";
        string n = nombreObjeto.Trim();
        string previo;
        do { previo = n; n = SufijoCopia.Replace(n, "").Trim(); } while (n != previo);
        return n.ToLowerInvariant();
    }

    /// <summary>Busca un producto por el nombre del objeto de la escena.</summary>
    public static Producto Buscar(string nombreObjeto)
    {
        AsegurarCarga();
        porClave.TryGetValue(Normalizar(nombreObjeto), out var p);
        return p;
    }

    /// <summary>Busca un producto por su codigo de barras.</summary>
    public static Producto BuscarPorCodigo(string codigo)
    {
        AsegurarCarga();
        if (string.IsNullOrEmpty(codigo)) return null;
        porCodigo.TryGetValue(codigo.Trim(), out var p);
        return p;
    }

    /// <summary>12.5 -> "S/ 12.50"</summary>
    public static string Formatear(float monto)
    {
        AsegurarCarga();
        return Moneda + " " + monto.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Vuelve a leer el JSON (util si lo editas con el juego en Play).</summary>
    public static void Recargar()
    {
        porClave = null;
        AsegurarCarga();
    }

    static void AsegurarCarga()
    {
        if (porClave != null) return;

        porClave = new Dictionary<string, Producto>();
        porCodigo = new Dictionary<string, Producto>();
        todos = new List<Producto>();

        var texto = Resources.Load<TextAsset>(RutaResources);
        if (texto == null)
        {
            Debug.LogError("[BaseDatos] No se encontro Resources/" + RutaResources + ".json");
            return;
        }

        Archivo archivo;
        try { archivo = JsonUtility.FromJson<Archivo>(texto.text); }
        catch (Exception e)
        {
            Debug.LogError("[BaseDatos] El JSON tiene un error de formato: " + e.Message);
            return;
        }

        if (archivo == null || archivo.productos == null) return;
        if (!string.IsNullOrEmpty(archivo.moneda)) Moneda = archivo.moneda;

        foreach (var p in archivo.productos)
        {
            if (p == null || string.IsNullOrEmpty(p.clave)) continue;

            string clave = Normalizar(p.clave);
            if (porClave.ContainsKey(clave))
                Debug.LogWarning("[BaseDatos] Clave repetida: '" + p.clave + "' (se usa la primera).");
            else
                porClave[clave] = p;

            if (!string.IsNullOrEmpty(p.codigo))
            {
                if (porCodigo.ContainsKey(p.codigo))
                    Debug.LogWarning("[BaseDatos] Codigo repetido: " + p.codigo);
                else
                    porCodigo[p.codigo] = p;
            }

            todos.Add(p);
        }

        Debug.Log("[BaseDatos] " + todos.Count + " productos cargados.");
    }
}
