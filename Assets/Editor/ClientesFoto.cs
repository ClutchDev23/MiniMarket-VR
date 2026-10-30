using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools/Minimarket/Clientes/Foto de prueba (en Play)
/// Saca 3 fotos y las guarda en Logs/: el cliente de frente, la caja vista por el
/// cajero y la tienda desde arriba. Sirve para revisar sin visor.
/// </summary>
public static class ClientesFoto
{
    [MenuItem("Tools/Minimarket/Clientes/Foto de prueba (en Play)")]
    public static void Foto()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Foto", "Dale Play primero.", "OK"); return; }
        var g = Object.FindAnyObjectByType<GestorClientes>();
        Cliente c = null;
        if (Selection.activeGameObject != null) c = Selection.activeGameObject.GetComponentInParent<Cliente>();
        if (c == null) c = Object.FindAnyObjectByType<Cliente>();

        if (c != null)
        {
            var obj = c.transform.position + Vector3.up * 1.0f;
            Sacar("foto_cliente.png", obj + c.transform.forward * 2.6f + Vector3.up * 0.5f, obj, 50f);
        }
        if (g != null && g.Mostrador != null)
        {
            var m = g.Mostrador;
            var cajero = m.TransformPoint(new Vector3(-0.35f, 0f, 0.45f)) + Vector3.up * 1.75f;
            Sacar("foto_caja.png", cajero, g.PuntoCaja + Vector3.up * 1.0f, 70f);
            Sacar("foto_arriba.png", g.PuntoCaja + new Vector3(0f, 9f, -4f), g.PuntoCaja + new Vector3(0, 0, -4f), 80f);
            var pantalla = GameObject.Find("Pantalla Caja");
            if (pantalla != null) Sacar("foto_pantalla.png", cajero, pantalla.transform.position, 60f);
            // El mostrador desde arriba: bandeja de dinero, POS e impresora.
            var centroM = m.position + Vector3.up * 0.95f;
            Sacar("foto_mostrador.png", centroM + m.right * 0.2f + Vector3.up * 1.4f - m.forward * 0.6f, centroM + m.right * 0.2f, 60f);
            Sacar("foto_mostrador2.png", cajero, centroM + m.right * 0.35f, 55f);
            var resumen = GameObject.Find("Resumen");
            if (resumen != null && resumen.activeInHierarchy && pantalla != null) Sacar("foto_resumen.png", cajero, pantalla.transform.position, 60f);
            var enCaja = g.ClienteEnCaja;
            if (enCaja != null)
                Sacar("foto_burbuja.png", cajero, enCaja.transform.position + Vector3.up * 1.7f, 60f);
        }
        // Vistas de la tienda (pasillos).
        Sacar("foto_tienda.png", new Vector3(0f, 2.3f, -2.0f), new Vector3(0f, 1.2f, -14f), 75f);
        Sacar("foto_pasillo.png", new Vector3(4.1f, 2.2f, -9.5f), new Vector3(4.1f, 1.3f, -16f), 70f);
        Sacar("foto_pared.png", new Vector3(10f, 2.1f, -2.5f), new Vector3(10.5f, 1.4f, 0f), 70f);
        // De cerca: bandeja, POS e impresora.
        var bandeja = BandejaDinero.I;
        if (bandeja != null)
            Sacar("foto_bandeja.png", bandeja.transform.position + Vector3.up * 0.55f + (bandeja.transform.position - (g != null ? g.PuntoCaja : Vector3.zero)).normalized * 0.35f, bandeja.transform.position, 60f);
        var imp = ImpresoraBoletas.I;
        if (imp != null)
            Sacar("foto_impresora.png", imp.transform.position + Vector3.up * 0.5f + imp.transform.forward * 0.45f, imp.transform.position, 60f);
        // El letrero de fuera.
        var letrero = GameObject.Find("Letrero Cajero XR");
        if (letrero != null)
            Sacar("foto_letrero.png", letrero.transform.position + letrero.transform.forward * -9f + Vector3.down * 3.5f, letrero.transform.position + Vector3.down * 1.5f, 55f);
        Debug.Log("[Foto] Guardadas en Logs/");
    }

    /// <summary>
    /// Prueba completa (en Play): escanea, el cliente paga con billete, se entrega el
    /// vuelto y, 2.5 s despues (cuando ya salio la boleta), saca fotos del mostrador y
    /// del resumen del turno.
    /// </summary>
    [MenuItem("Tools/Minimarket/Caja/4 - Prueba completa con fotos")]
    public static void PruebaCompleta()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Prueba", "Dale Play primero.", "OK"); return; }
        var caja = Object.FindAnyObjectByType<CajaRegistradora>();
        if (caja == null) return;
        if (caja.PasoActual == CajaRegistradora.Paso.Completada) caja.NuevaVenta();
        caja.ProbarCobro();
        double fin = EditorApplication.timeSinceStartup + 2.5;
        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            if (EditorApplication.timeSinceStartup < fin) return;
            EditorApplication.update -= cb;
            Foto();
            caja.MostrarResumen();
            var g = Object.FindAnyObjectByType<GestorClientes>();
            var pantalla = GameObject.Find("Pantalla Caja");
            if (g != null && g.Mostrador != null && pantalla != null)
            {
                var cajero = g.Mostrador.TransformPoint(new Vector3(-0.35f, 0f, 0.45f)) + Vector3.up * 1.75f;
                Sacar("foto_resumen.png", cajero, pantalla.transform.position, 60f);
            }
            caja.CerrarResumen();
        };
        EditorApplication.update += cb;
    }

    /// <summary>Fotos de un mueble de cada tipo (para revisar como quedan llenos).</summary>
    [MenuItem("Tools/Minimarket/Productos/Foto de muebles")]
    public static void FotoMuebles()
    {
        foreach (var tipo in new[] { "Pref_Freezer01", "Pref_IceCreamM", "Pref_WallRack01", "Pref_WallRack02", "Pref_WallRack03", "Pref_CrateStand01" })
        {
            Transform t = null;
            foreach (var x in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (x.name.StartsWith(tipo) && (x.parent == null || !x.parent.name.StartsWith(tipo))) { t = x; break; }
            if (t == null) continue;
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var f = t.forward; f.y = 0; f.Normalize();
            float d = Mathf.Max(b.size.x, b.size.z) * 0.9f + 1.2f;
            Sacar("mueble_" + tipo + "_a.png", b.center + f * d + Vector3.up * 0.5f, b.center, 60f);
            Sacar("mueble_" + tipo + "_b.png", b.center - f * d + Vector3.up * 0.5f, b.center, 60f);
        }
        Debug.Log("[Foto] Muebles guardados en Logs/");
    }

    /// <summary>Pone un billete de cada valor (y dos al reves) y todas las monedas, y les toma una foto de cerca (en Play).</summary>
    [MenuItem("Tools/Minimarket/Caja/5 - Foto de billetes y monedas")]
    public static void FotoSoles()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Foto] Dale Play primero."); return; }
        var o = new Vector3(0f, 40f, 0f);
        var hechos = new System.Collections.Generic.List<GameObject>();
        int[] billetes = { 1000, 2000, 5000, 10000, 20000 };
        for (int i = 0; i < billetes.Length; i++)
        {
            var b = BandejaDinero.Billete3D(billetes[i]);
            b.transform.SetPositionAndRotation(o + new Vector3(-0.32f + 0.16f * i, 0f, 0.05f), Quaternion.identity);
            hechos.Add(b);
            if (i < 2)
            {
                var r = BandejaDinero.Billete3D(billetes[i + 3]);
                r.transform.SetPositionAndRotation(o + new Vector3(-0.24f + 0.16f * i * 2, 0f, -0.04f), Quaternion.Euler(0f, 0f, 180f));
                hechos.Add(r);
            }
        }
        int[] monedas = { 10, 20, 50, 100, 200, 500 };
        for (int i = 0; i < monedas.Length; i++)
        {
            var m = BandejaDinero.Moneda3D(monedas[i]);
            m.transform.SetPositionAndRotation(o + new Vector3(-0.15f + 0.06f * i, 0f, -0.11f), Quaternion.identity);
            hechos.Add(m);
        }
        Sacar("foto_soles.png", o + new Vector3(0f, 0.42f, -0.22f), o + new Vector3(0f, 0f, -0.02f), 50f);
        Sacar("foto_soles_cerca.png", o + new Vector3(0f, 0.16f, -0.2f), o + new Vector3(0f, 0f, -0.1f), 40f);
        foreach (var h in hechos) Object.Destroy(h);
        Debug.Log("[Foto] Billetes y monedas: Logs/foto_soles.png");
    }

    static void Sacar(string archivo, Vector3 desde, Vector3 hacia, float fov)
    {
        var go = new GameObject("FotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        go.transform.position = desde;
        go.transform.LookAt(hacia);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "Logs", archivo), tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
    }
}
