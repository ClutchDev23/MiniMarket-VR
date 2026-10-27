using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools/Minimarket/Clientes/Sondeo (medir la tienda)
/// Escribe en Logs/sondeo_clientes.txt donde estan la puerta, las cajas, el lector,
/// el jugador y las barreras de la fila. Solo lee la escena, no cambia nada.
/// </summary>
public static class ClientesSondeo
{
    [MenuItem("Tools/Minimarket/Clientes/Sondeo (medir la tienda)")]
    public static void Ejecutar()
    {
        var sb = new StringBuilder();
        var esc = SceneManager.GetActiveScene();
        sb.AppendLine("ESCENA " + esc.name);

        var todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

        void Linea(Transform t)
        {
            var b = Bounds(t);
            sb.AppendLine(string.Format("{0} | ruta={1} | pos={2} rot={3} fwd={4} | bounds c={5} s={6}",
                t.name, Ruta(t), V(t.position), V(t.eulerAngles), V(t.forward),
                b.HasValue ? V(b.Value.center) : "-", b.HasValue ? V(b.Value.size) : "-"));
        }

        sb.AppendLine("\n== PUERTA");
        foreach (var t in todos.Where(t => t.name.Contains("Puerta") || t.name.StartsWith("Panel") || t.name.Contains("Door"))) Linea(t);

        sb.AppendLine("\n== CAJAS");
        foreach (var t in todos.Where(t => t.name.StartsWith("Pref_CashCounter"))) Linea(t);

        sb.AppendLine("\n== LECTOR");
        foreach (var l in Object.FindObjectsByType<LectorCodigos>(FindObjectsInactive.Include)) Linea(l.transform);

        sb.AppendLine("\n== JUGADOR / CAMARA");
        foreach (var t in todos.Where(t => t.name.Contains("XR Origin") || t.name.Contains("Camera Offset") || t.name == "Main Camera")) Linea(t);

        sb.AppendLine("\n== EDIFICIO");
        foreach (var t in todos.Where(t => t.name == "SuperMarket" || t.name == "MarketBuilding" || t.name == "Floor" || t.name == "Ot_Floor" || t.name == "RackAndCounter" || t.name == "Productos")) Linea(t);

        sb.AppendLine("\n== FILA / BARRERAS / ESTANTES (nombres unicos)");
        var nombres = todos.Select(t => System.Text.RegularExpressions.Regex.Replace(t.name, @"\s*\(\d+\)$", ""))
                           .Where(n => n.StartsWith("Pref_")).GroupBy(n => n).OrderBy(g => g.Key);
        foreach (var g in nombres) sb.AppendLine(g.Key + " x" + g.Count());

        sb.AppendLine("\n== BARRERAS (posiciones)");
        foreach (var t in todos.Where(t => t.name.Contains("Queue") || t.name.Contains("Stanchion") || t.name.Contains("Barrier") || t.name.Contains("Rope") || t.name.Contains("Belt")))
            Linea(t);

        sb.AppendLine("\n== HIJOS DE RackAndCounter");
        var rc = todos.FirstOrDefault(t => t.name == "RackAndCounter");
        if (rc != null) foreach (Transform h in rc) Linea(h);

        sb.AppendLine("\n== HIJOS DE SuperMarket");
        var sm = todos.FirstOrDefault(t => t.name == "SuperMarket");
        if (sm != null) foreach (Transform h in sm) Linea(h);

        var ruta = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "sondeo_clientes.txt");
        File.WriteAllText(ruta, sb.ToString());
        Debug.Log("[Sondeo] Escrito " + ruta);
        EditorUtility.DisplayDialog("Sondeo", "Listo: Logs/sondeo_clientes.txt", "OK");
    }

    static Bounds? Bounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return null;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static string Ruta(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static string V(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.00},{1:0.00},{2:0.00})", v.x, v.y, v.z);
}
