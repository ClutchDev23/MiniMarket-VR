using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pruebas de lo nuevo (en Play): balanza, personalidades, dias, examen y personajes.
/// Los resultados quedan en Logs/juego_log.txt y fotos en Logs/prueba_*.png.
/// </summary>
public static class PruebaNovedades
{
    [MenuItem("Tools/Minimarket/Pruebas/1 - Balanza con fruta (Play)")]
    static void Balanza1()
    {
        if (!Application.isPlaying || Balanza.I == null) { Debug.LogWarning("[Prueba] Dale Play primero."); return; }
        var p = Balanza.NuevaFruta(out int tipo, out float kg);
        var plato = Balanza.I.transform.Find("Plato");
        var go = Balanza.I.SoltarFruta(p, tipo, kg, plato.position + Vector3.up * 0.04f, null);
        go.GetComponent<Rigidbody>().isKinematic = true;
        Debug.Log("[Prueba] Fruta en la balanza: " + p.nombre + " S/ " + p.precio.ToString("0.00"));
        EditorApplication.delayCall += () => Esperar(1.5f, () =>
        {
            var caja = Object.FindAnyObjectByType<CajaRegistradora>();
            bool ok = false;
            foreach (var l in caja.Lineas) if (l.producto == p) ok = true;
            Debug.Log("[Prueba] Balanza agrego la fruta a la venta: " + (ok ? "SI" : "NO"));
            var b = Balanza.I.transform;
            var cajero = caja.Mostrador.TransformPoint(new Vector3(-0.30f, 1.60f, 0.35f));
            Foto("prueba_balanza.png", Vector3.Lerp(cajero, b.position, 0.55f) + Vector3.up * 0.15f, b.position + Vector3.up * 0.12f, 50f);
        });
    }

    [MenuItem("Tools/Minimarket/Pruebas/2 - Clientes impacientes (Play)")]
    static void Personalidades()
    {
        if (!Application.isPlaying || GestorClientes.I == null) { Debug.LogWarning("[Prueba] Dale Play primero."); return; }
        Cliente.PacienciaPrueba = 40f;   // comentan a los 12 s y a los 24 s, se van a los 40 s
        var tipos = new[] { Cliente.Personalidad.Apurado, Cliente.Personalidad.Quejon, Cliente.Personalidad.Amable };
        for (int i = 0; i < tipos.Length; i++)
        {
            var c = GestorClientes.I.TraerCliente();
            if (c != null) { c.Tipo = tipos[i]; Debug.Log("[Prueba] " + c.name + " es " + tipos[i]); }
        }
        Debug.Log("[Prueba] 3 clientes con paciencia de 40 s. Mira el log en ~2 min.");
    }

    [MenuItem("Tools/Minimarket/Pruebas/3 - Completar el dia (Play)")]
    static void Dia()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Prueba] Dale Play primero."); return; }
        var caja = Object.FindAnyObjectByType<CajaRegistradora>();
        int antes = Jornada.Dia;
        for (int i = 0, n = Jornada.Meta - Jornada.VentasDelDia; i < n; i++) Jornada.VentaHecha(caja);
        Debug.Log("[Prueba] Dia antes " + antes + " -> ahora " + Jornada.Dia + " (max clientes " + Jornada.MaxClientes(3) + ")");
        Jornada.Reiniciar();
    }

    [MenuItem("Tools/Minimarket/Pruebas/4 - Examen corto de 20 s (Play)")]
    static void ExamenCorto()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Prueba] Dale Play primero."); return; }
        Examen.Empezar(20f);
        EditorApplication.delayCall += () => Esperar(23f, () =>
        {
            string r = Examen.UltimoCertificado;
            Debug.Log("[Prueba] Certificado: " + (r != null && File.Exists(r) ? r + " (" + new FileInfo(r).Length + " bytes)" : "NO SE CREO"));
        });
    }

    [MenuItem("Tools/Minimarket/Pruebas/5 - Foto de todos los personajes (Play)")]
    static void Personajes()
    {
        var g = GestorClientes.I;
        if (!Application.isPlaying || g == null) { Debug.LogWarning("[Prueba] Dale Play primero."); return; }
        var cam = Camera.main.transform;
        var f = cam.forward; f.y = 0; f.Normalize();
        var der = Vector3.Cross(Vector3.up, f);
        var centro = new Vector3(cam.position.x, 0f, cam.position.z) + f * 4f;
        var piso = Physics.Raycast(centro + Vector3.up * 3f, Vector3.down, out var hit, 10f) ? hit.point.y : 0f;
        var lista = g.Config.personajes;
        var creados = new List<GameObject>();
        for (int i = 0; i < lista.Length; i++)
        {
            if (lista[i] == null) continue;
            var pos = centro + der * ((i - (lista.Length - 1) / 2f) * 0.9f);
            pos.y = piso;
            var go = Object.Instantiate(lista[i], pos, Quaternion.LookRotation(-f));
            var a = go.GetComponentInChildren<Animator>();
            if (a != null) { a.runtimeAnimatorController = g.Config.animador; a.applyRootMotion = false; }
            creados.Add(go);
            Debug.Log("[Prueba] Personaje " + i + ": " + lista[i].name);
        }
        EditorApplication.delayCall += () => Esperar(1f, () =>
        {
            Foto("prueba_personajes.png", centro - f * 3.2f + Vector3.up * (piso + 1.3f), centro + Vector3.up * (piso + 0.9f), 65f);
            foreach (var c in creados) Object.Destroy(c);
        });
    }

    // ----------------------------------------------------------------------------
    static void Esperar(float seg, System.Action hacer)
    {
        double hasta = EditorApplication.timeSinceStartup + seg;
        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            if (!Application.isPlaying) { EditorApplication.update -= cb; return; }
            if (EditorApplication.timeSinceStartup < hasta) return;
            EditorApplication.update -= cb;
            hacer();
        };
        EditorApplication.update += cb;
    }

    static void Foto(string archivo, Vector3 desde, Vector3 hacia, float fov)
    {
        var go = new GameObject("FotoCam");
        var c = go.AddComponent<Camera>();
        c.fieldOfView = fov; c.nearClipPlane = 0.02f;
        go.transform.position = desde; go.transform.LookAt(hacia);
        var rt = new RenderTexture(1280, 720, 24);
        c.targetTexture = rt; c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine("Logs", archivo), tex.EncodeToPNG());
        Object.DestroyImmediate(go); Object.DestroyImmediate(rt);
        Debug.Log("[Prueba] Foto Logs/" + archivo);
    }

    // ----------------------------------------------------------------------------
    /// <summary>Copia los personajes de Mixamo del escritorio al proyecto (los archivos grandes).</summary>
    [MenuItem("Tools/Minimarket/Pruebas/Importar personajes del escritorio")]
    static void Importar()
    {
        var nombres = new Dictionary<string, string>
        {
            { "Ch09_nonPBR.fbx", "personaje09cliente.fbx" },
            { "Ch22_nonPBR.fbx", "personaje22cliente.fbx" },
            { "Ch23_nonPBR.fbx", "personaje23cliente.fbx" },
            { "Ch26_nonPBR.fbx", "personaje26cliente.fbx" },
        };
        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var carpetas = new[] { Path.Combine(home, "OneDrive", "Desktop"), Path.Combine(home, "Desktop"), Path.Combine(home, "Downloads") };
        int n = 0;
        foreach (var kv in nombres)
            foreach (var c in carpetas)
            {
                var src = Path.Combine(c, kv.Key);
                if (!File.Exists(src)) continue;
                File.Copy(src, Path.Combine("Assets/Clientes/Personajes", kv.Value), true);
                Debug.Log("[Importar] " + src + " -> " + kv.Value);
                n++; break;
            }
        AssetDatabase.Refresh();
        Debug.Log("[Importar] " + n + " personaje(s) copiados. Ahora: Clientes/1 - Configurar personajes y animaciones.");
    }
}
