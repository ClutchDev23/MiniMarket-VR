using UnityEditor;
using UnityEngine;

/// <summary>
/// Pone muebles de la TIENDA en el cuarto del menu, para que el menu se sienta parte
/// del minimarket (mismos estantes, productos, caja, carrito y el letrero Cajero XR).
/// La llama MenuBuilder.BuildRoom.
/// </summary>
public static class MenuDecorarTienda
{
    const string Prefabs = "Assets/SuperMarket/Prefabs/";

    public static void Decorar(Transform raiz, float ancho, float largo, float alto)
    {
        var deco = new GameObject("Tienda").transform;
        deco.SetParent(raiz, false);
        float hw = ancho / 2f, hl = largo / 2f;

        // Dos gondolas a los lados, con productos mirando al jugador.
        var izq = PonerGondola(deco, "Pref_StandingRack01_Sm", -hw, +1f);
        var der = PonerGondola(deco, "Pref_StandingRack02_Sm", +hw, -1f);
        int n = 0;
        if (izq != null) n += ProductosLlenarTienda.LlenarMueble(izq, deco, 0f, 11);
        if (der != null) n += ProductosLlenarTienda.LlenarMueble(der, deco, 0f, 23);

        // La caja registradora y un carrito, atras (se ven al darse la vuelta).
        var caja = Poner(deco, "Pref_CashCounter_Sm", new Vector3(hw * 0.45f, 0f, -hl * 0.62f), 90f);
        Encerrar(caja, hw, hl);
        var carrito = Poner(deco, "Pref_Trolley01_Sm", new Vector3(-hw * 0.55f, 0f, -hl * 0.55f), 35f);
        Encerrar(carrito, hw, hl);
        // Sin fisica en el menu: que no se mueva al tocarlo.
        if (carrito != null)
            foreach (var rb in carrito.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;

        // El letrero, en la pared de atras.
        Letrero(deco, new Vector3(0f, alto - 0.75f, -hl + 0.03f), Quaternion.Euler(0f, 180f, 0f), 2.6f);

        Debug.Log("[Menu] Cuarto decorado como la tienda: " + n + " productos.");
    }

    static Transform Poner(Transform padre, string prefab, Vector3 pos, float giroY)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefab + ".prefab");
        if (p == null) { Debug.LogWarning("[Menu] No encontre " + prefab); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p, padre);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, giroY, 0f);
        return go.transform;
    }

    /// <summary>Gondola pegada a la pared x = paredX, larga a lo largo de z, con su frente hacia el centro.</summary>
    static Transform PonerGondola(Transform padre, string prefab, float paredX, float haciaCentro)
    {
        var t = Poner(padre, prefab, Vector3.zero, 0f);
        if (t == null) return null;
        var lb = BoundsLocal(t);
        // Eje largo a lo largo de z.
        float giro = lb.size.x > lb.size.z ? 90f : 0f;
        t.rotation = Quaternion.Euler(0f, giro, 0f);
        // El lado "de adelante" que llena la herramienta es el eje corto POSITIVO del mueble.
        var corto = lb.size.x < lb.size.z ? Vector3.right : Vector3.forward;
        if (Mathf.Sign(t.TransformDirection(corto).x) != Mathf.Sign(haciaCentro)) t.rotation = Quaternion.Euler(0f, giro + 180f, 0f);
        Physics.SyncTransforms();
        var b = BoundsMundo(t);
        float x = paredX - Mathf.Sign(paredX) * (b.extents.x + 0.08f);
        t.position += new Vector3(x - b.center.x, -b.min.y, -b.center.z);
        return t;
    }

    /// <summary>Si el mueble se sale del cuarto, lo mete.</summary>
    static void Encerrar(Transform t, float hw, float hl)
    {
        if (t == null) return;
        Physics.SyncTransforms();
        var b = BoundsMundo(t);
        var d = Vector3.zero;
        if (b.max.x > hw - 0.05f) d.x = hw - 0.05f - b.max.x;
        if (b.min.x < -hw + 0.05f) d.x = -hw + 0.05f - b.min.x;
        if (b.max.z > hl - 0.05f) d.z = hl - 0.05f - b.max.z;
        if (b.min.z < -hl + 0.05f) d.z = -hl + 0.05f - b.min.z;
        d.y = -b.min.y;
        t.position += d;
    }

    static void Letrero(Transform padre, Vector3 pos, Quaternion rot, float ancho)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SuperMarket/Materials/LetreroCajeroXR_Mat.mat");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SuperMarket/Textures/LetreroCajeroXR.png");
        if (mat == null && tex != null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture("_BaseMap", tex);
            mat.EnableKeyword("_ALPHATEST_ON");
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            AssetDatabase.CreateAsset(mat, "Assets/MenuRoom/Materials/LetreroMenu.mat");
        }
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Letrero Cajero XR";
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(padre, false);
        q.transform.position = pos;
        q.transform.rotation = rot;
        q.transform.localScale = new Vector3(ancho, ancho * 386f / 1024f, 1f);
        if (mat != null) q.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Bounds BoundsMundo(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Bounds BoundsLocal(Transform t)
    {
        var w = BoundsMundo(t);   // con giro 0 el local coincide con el mundo
        return new Bounds(w.center - t.position, w.size);
    }
}
