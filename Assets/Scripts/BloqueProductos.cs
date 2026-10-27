using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Dibuja de una sola vez todos los productos de una zona de la tienda.
///
/// POR QUE
/// -------
/// La tienda llena tiene miles de productos. Dibujarlos uno por uno son miles de
/// "draw calls" y el Quest no lo aguanta. Este componente junta (al empezar el juego)
/// las mallas de sus productos en UNA malla por material, y apaga el dibujo
/// individual de cada producto.
///
/// Cuando agarras un producto, este se "libera" (ProductoEstante.Liberar): vuelve a
/// dibujarse solo y aqui se reconstruye la malla sin el.
///
/// Requiere que el .fbx de productos tenga Read/Write activado (lo hace la herramienta
/// Tools/Minimarket/Productos/Llenar tienda).
/// </summary>
public class BloqueProductos : MonoBehaviour
{
    class Grupo
    {
        public Material material;
        public MeshFilter filtro;
        public MeshRenderer render;
        public bool sucio;
    }

    readonly List<ProductoEstante> productos = new List<ProductoEstante>();
    readonly Dictionary<Material, Grupo> grupos = new Dictionary<Material, Grupo>();
    bool armado;

    void Start() { Armar(); }

    void Armar()
    {
        productos.Clear();
        GetComponentsInChildren(true, productos);

        foreach (var p in productos)
        {
            p.bloque = this;
            var mf = p.GetComponent<MeshFilter>();
            var r = p.Renderer;
            if (mf == null || mf.sharedMesh == null || r == null) continue;
            if (!mf.sharedMesh.isReadable)
            {
                Debug.LogWarning("[Bloque] La malla '" + mf.sharedMesh.name + "' no tiene Read/Write: se dibuja sola.");
                continue;
            }
            foreach (var m in r.sharedMaterials)
                if (m != null && !grupos.ContainsKey(m)) grupos[m] = NuevoGrupo(m);
            r.enabled = false;
        }

        foreach (var g in grupos.Values) g.sucio = true;
        armado = true;
        Reconstruir();
    }

    Grupo NuevoGrupo(Material m)
    {
        var go = new GameObject("Dibujo " + m.name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = ShadowCastingMode.Off;   // miles de productos: sin sombras (rendimiento Quest)
        mr.lightProbeUsage = LightProbeUsage.BlendProbes;
        mf.sharedMesh = new Mesh { name = "Bloque " + m.name, indexFormat = IndexFormat.UInt32 };
        return new Grupo { material = m, filtro = mf, render = mr };
    }

    /// <summary>Saca un producto del dibujo (porque lo agarraron).</summary>
    public void Quitar(ProductoEstante p)
    {
        var r = p.Renderer;
        if (r == null) return;
        foreach (var m in r.sharedMaterials)
            if (m != null && grupos.TryGetValue(m, out var g)) g.sucio = true;
    }

    void LateUpdate()
    {
        if (armado) Reconstruir();
    }

    void Reconstruir()
    {
        List<CombineInstance> lista = null;
        var aLocal = transform.worldToLocalMatrix;

        foreach (var g in grupos.Values)
        {
            if (!g.sucio) continue;
            g.sucio = false;
            if (lista == null) lista = new List<CombineInstance>(256);
            lista.Clear();

            foreach (var p in productos)
            {
                if (p == null || p.Liberado || p.bloque != this) continue;
                var mf = p.GetComponent<MeshFilter>();
                var r = p.Renderer;
                if (mf == null || r == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var mats = r.sharedMaterials;
                int subs = Mathf.Min(mats.Length, mf.sharedMesh.subMeshCount);
                for (int i = 0; i < subs; i++)
                {
                    if (mats[i] != g.material) continue;
                    lista.Add(new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        subMeshIndex = i,
                        transform = aLocal * p.transform.localToWorldMatrix,
                    });
                }
            }

            var malla = g.filtro.sharedMesh;
            malla.Clear();
            if (lista.Count > 0)
            {
                malla.CombineMeshes(lista.ToArray(), true, true, false);
                malla.RecalculateBounds();
            }
            g.render.enabled = lista.Count > 0;
        }
    }
}
