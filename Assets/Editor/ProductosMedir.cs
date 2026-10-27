using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Tools/Minimarket/Productos/Medir (triangulos y tamanos). Escribe Logs/medir_productos.txt</summary>
public static class ProductosMedir
{
    [MenuItem("Tools/Minimarket/Productos/Medir (triangulos y tamanos)")]
    public static void Medir()
    {
        var sb = new StringBuilder();
        var raiz = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Productos/Models/Super market Models FBX File.fbx");
        int total = 0;
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null) continue;
            var m = mf.sharedMesh; int tris = m.triangles.Length / 3; total += tris;
            var s = Vector3.Scale(m.bounds.size, t.localScale);
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} | tris={1} | mats={2} | size={3:0.000},{4:0.000},{5:0.000} | rot={6} | verts={7}",
                t.name, tris, mr != null ? mr.sharedMaterials.Length : 0, s.x, s.y, s.z, t.localEulerAngles, m.vertexCount));
        }
        sb.AppendLine("TOTAL tris catalogo=" + total);
        var prod = GameObject.Find("Productos");
        if (prod != null)
        {
            int n = 0, tr = 0;
            foreach (var mf in prod.GetComponentsInChildren<MeshFilter>()) { n++; tr += mf.sharedMesh.triangles.Length / 3; }
            sb.AppendLine("Escena: productos=" + n + " tris=" + tr);
            int i = 0;
            foreach (var r in prod.GetComponentsInChildren<Renderer>())
            {
                if (i++ > 12) break;
                sb.AppendLine("  ej " + r.name + " bounds=" + r.bounds.size.ToString("F3") + " scale=" + r.transform.localScale.ToString("F3"));
            }
        }
        // materiales distintos
        var mats = raiz.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToList();
        sb.AppendLine("materiales distintos=" + mats.Count + " shader=" + (mats.Count > 0 && mats[0] != null ? mats[0].shader.name : "-") + " instancing=" + (mats.Count > 0 && mats[0] != null && mats[0].enableInstancing));
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Logs", "medir_productos.txt"), sb.ToString());
        Debug.Log("[Medir] Logs/medir_productos.txt");
    }
}
