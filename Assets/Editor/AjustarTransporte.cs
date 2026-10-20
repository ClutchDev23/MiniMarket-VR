using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Arregla el "arrastre" de los productos al caminar con algo agarrado.
///
/// QUE PASABA
/// ----------
/// Al caminar con un producto en la mano se veia como que lo arrastraras por el suelo
/// en vez de llevarlo.Habia tres motivos, y todos estan aqui:
//
///
/// 1. FAR ATTACH (el principal).
///    El NearFarInteractor del rig tiene "Far Attach Mode = Far". Eso significa que
///    al agarrar con el rayo, el attach point del interactor se fija en el punto del
///    suelo donde apuntaste (ver XRRayInteractor.OnSelectEntering: attachTransform
///    .position = raycastHit.point). El producto se queda clavado ahi y tu camina
///    arrastrandolo. Con "Near" el objeto viene a la mano, que es lo que se quiere.
///
///    Aqui se actua en el propio producto: InteractableFarAttachMode.Near le dice al
///    interactor "traeme el objeto a la mano", y tiene prioridad sobre lo que diga
///    el interactor. No hace falta tocar el prefab del sample de Unity.
///
/// 2. MOVEMENT TYPE.
///    Los productos estaban en VelocityTracking, que mueve el Rigidbody por velocidad
///    dentro del FixedUpdate (fisica, 50 Hz). Tu mano va a 72-90 Hz en Quest, asi
///    que el producto va un paso de fisica por detras: se ve como que lo arrastras.
///    Con Instantaneous, el transform se mueve en cada Update: queda pegado a la mano.
///
/// 3. INTERPOLACION DEL RIGIDBODY.
///    Estaba apagada, asi que al soltar el producto (o cuando rueda) da tirones
///    entre pasos de fisica.
///
/// QUE NO SE TOCA
/// --------------
/// Solo los productos (root "Productos"), los carritos y el lector. La escena Menu no
/// se toca. Los estantes y los huecos (sockets) tampoco.
///
/// Uso: Tools/Minimarket/Agarrar/Arreglar Arrastre al Caminar
/// </summary>
public static class AjustarTransporte
{
    const string Escena = "Assets/Scenes/MinimarketXR_Full.unity";

    [MenuItem("Tools/Minimarket/Agarrar/Arreglar Arrastre al Caminar")]
    public static void Ejecutar()
    {
        var t = new System.Text.StringBuilder();
        t.AppendLine("=== Arreglo de arrastre al caminar ===");

        EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include);

        int productos = 0, carritos = 0, lectores = 0;

        foreach (var g in grabs)
        {
            string path = Ruta(g.transform);

            bool esProducto = BajoProductos(g.transform);
            bool esCarrito = path.Contains("Trolley");
            bool esLector = path == "base" || path.EndsWith("/base");

            if (!esProducto && !esCarrito && !esLector) continue;

            Corregir(g, t);

            if (esProducto) productos++;
            else if (esCarrito) carritos++;
            else lectores++;
        }

        t.AppendLine("productos corregidos: " + productos);
        t.AppendLine("carritos corregidos: " + carritos);
        t.AppendLine("lectores corregidos: " + lectores);

        if (!EditorSceneManager.SaveScene(scene, Escena))
        {
            Debug.LogError("[Transporte] No se pudo guardar la escena.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log(t.ToString());
        Directory.CreateDirectory("Temp/opencode");
        File.WriteAllText("Temp/opencode/transporte.txt", t.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Corregir(XRGrabInteractable g, System.Text.StringBuilder t)
    {
        // 1) Que el objeto venga a la mano en vez de quedarse donde lo apuntaste.
        //    Near tiene prioridad sobre el ajuste del interactor (que es Far).
        g.farAttachMode = InteractableFarAttachMode.Near;

        // 2) Movimiento en cada Update (sin retardo de fisica).
        g.movementType = XRBaseInteractable.MovementType.Instantaneous;

        // 3) Interpolacion del Rigidbody: caidas y rodadas sin tirones.
        var rb = g.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        // Sin suavizado: el objetivo ya va pegado a la mano.
        g.smoothPosition = false;
        g.smoothRotation = false;
        g.trackPosition = true;
        g.trackRotation = true;

        t.AppendLine("  " + g.name + ": farAttach=Near movement=Instantaneous" +
                     (rb != null ? " interp=Interpolate" : " (sin rigidbody)"));
    }

    static bool BajoProductos(Transform tr)
    {
        var p = tr.parent;
        int guard = 0;
        while (p != null && guard < 20) { if (p.name == "Productos") return true; p = p.parent; guard++; }
        return false;
    }

    static string Ruta(Transform tr)
    {
        var parts = new List<string>();
        var cur = tr;
        int guard = 0;
        while (cur != null && guard < 30) { parts.Insert(0, cur.name); cur = cur.parent; guard++; }
        return string.Join("/", parts);
    }
}
