using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProductosVerificar
{
    static readonly string[] Escenas =
    {
        "Assets/Scenes/MinimarketXR_Full.unity",
        "Assets/Scenes/MinimarketXR_Night.unity",
    };

    const float LimiteGrande = 0.20f;

    [MenuItem("Tools/Minimarket/Productos/Verificar Tamano")]
    public static void Ejecutar()
    {
        int errores = 0;
        Debug.Log("[Verificar] iniciando");

        foreach (var path in Escenas)
        {
            try { VerificarEscena(path); }
            catch (System.Exception e) { Debug.LogError("[Verificar] EX " + path + ": " + e); errores++; }
        }

        Debug.Log(errores == 0 ? "[Verificar] OK" : "[Verificar] ERRORES " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static void VerificarEscena(string path)
    {
        if (!System.IO.File.Exists(path)) { Debug.LogError("[Verificar] falta " + path); return; }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        Transform padreProd = null, padreSock = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Productos") padreProd = root.transform;
            if (root.name == "Sockets") padreSock = root.transform;
        }

        int productos = padreProd == null ? 0 : padreProd.childCount;
        int sockets = padreSock == null ? 0 : padreSock.childCount;
        Debug.Log($"[Verificar] {path}: productos={productos} sockets={sockets}");

        if (padreProd == null) return;

        float minTam = float.MaxValue, maxTam = 0f, suma = 0f;
        int grandes = 0, sinCol = 0, sinRb = 0, sinGrab = 0;
        var ejemplosGrandes = new List<string>();

        foreach (Transform p in padreProd)
        {
            var mf = p.GetComponent<MeshFilter>();
            var box = p.GetComponent<BoxCollider>();
            var rb = p.GetComponent<Rigidbody>();
            var grab = p.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (box == null) sinCol++;
            if (rb == null) sinRb++;
            if (grab == null) sinGrab++;

            Bounds wb;
            if (box != null) wb = box.bounds;
            else if (mf != null && mf.sharedMesh != null)
            {
                var lb = mf.sharedMesh.bounds;
                var mtx = p.localToWorldMatrix;
                wb = new Bounds(mtx.MultiplyPoint3x4(lb.min), Vector3.zero);
                foreach (var c in new[]
                {
                    new Vector3(lb.min.x, lb.min.y, lb.min.z), new Vector3(lb.max.x, lb.min.y, lb.min.z),
                    new Vector3(lb.min.x, lb.max.y, lb.min.z), new Vector3(lb.min.x, lb.min.y, lb.max.z),
                    new Vector3(lb.max.x, lb.max.y, lb.min.z), new Vector3(lb.max.x, lb.min.y, lb.max.z),
                    new Vector3(lb.min.x, lb.max.y, lb.max.z), new Vector3(lb.max.x, lb.max.y, lb.max.z),
                })
                    wb.Encapsulate(mtx.MultiplyPoint3x4(c));
            }
            else continue;

            float tam = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
            minTam = Mathf.Min(minTam, tam);
            maxTam = Mathf.Max(maxTam, tam);
            suma += tam;

            if (tam > LimiteGrande)
            {
                grandes++;
                if (ejemplosGrandes.Count < 10)
                    ejemplosGrandes.Add($"{p.name} tam={tam:F3}m escala={p.localScale.ToString("F3")}");
            }
        }

        float prom = productos > 0 ? suma / productos : 0f;
        Debug.Log($"[Verificar] tamaños: min={minTam:F3}m max={maxTam:F3}m prom={prom:F3}m grandes(>{LimiteGrande}m)={grandes}");
        Debug.Log($"[Verificar] componentes: sinCollider={sinCol} sinRigidbody={sinRb} sinGrab={sinGrab}");
        foreach (var e in ejemplosGrandes)
            Debug.LogWarning("[Verificar] GRANDE: " + e);
    }
}
