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
        // El letrero de fuera.
        var letrero = GameObject.Find("Letrero Cajero XR");
        if (letrero != null)
            Sacar("foto_letrero.png", letrero.transform.position + letrero.transform.forward * -9f + Vector3.down * 3.5f, letrero.transform.position + Vector3.down * 1.5f, 55f);
        Debug.Log("[Foto] Guardadas en Logs/");
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
