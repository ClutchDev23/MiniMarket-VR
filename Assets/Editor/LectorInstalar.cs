using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class LectorInstalar
{
    const string FbxPath = "Assets/pistolitaLectoraDeCodigoXD/base.fbx";
    const string DiaPath = "Assets/Scenes/MinimarketXR_Full.unity";
    const string NochePath = "Assets/Scenes/MinimarketXR_Night.unity";
    const string ClipPath = "Assets/Audio/Caja_Bip.wav";

    [MenuItem("Tools/Minimarket/Lector/Instalar Lector (Dia + Noche)")]
    public static void Instalar()
    {
        Debug.Log("[Lector] iniciando");
        int errores = 0;

        if (!Application.isBatchMode)
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        var dia = EditorSceneManager.OpenScene(DiaPath, OpenSceneMode.Single);
        var pDia = BuscarVinculado(dia);
        if (pDia == null) pDia = BuscarLibre(dia);

        if (pDia == null)
        {
            Debug.LogError("[Lector] falta la pistolita en " + DiaPath);
            errores++;
        }
        else
        {
            Configurar(pDia);
            if (!EditorSceneManager.SaveScene(dia))
            {
                Debug.LogError("[Lector] fallo guardar escena dia");
                errores++;
            }
        }

        var noche = EditorSceneManager.OpenScene(NochePath, OpenSceneMode.Additive);
        EditorSceneManager.SetActiveScene(dia);

        var pNoche = BuscarVinculado(noche);

        if (pNoche == null && pDia != null)
        {
            var previo = BuscarLibre(noche);
            if (previo != null)
            {
                Debug.Log("[Lector] reemplazo copia previa sin vinculo: " + previo.name);
                Object.DestroyImmediate(previo);
            }

            pNoche = ClonarEn(pDia, noche);
            Debug.Log("[Lector] pistolita replicada en escena noche");
        }

        if (pNoche == null)
        {
            Debug.LogError("[Lector] no hay pistolita en la escena noche");
            errores++;
        }
        else
        {
            Configurar(pNoche);
            if (!EditorSceneManager.SaveScene(noche))
            {
                Debug.LogError("[Lector] fallo guardar escena noche");
                errores++;
            }
        }

        EditorSceneManager.CloseScene(noche, true);

        Debug.Log(errores == 0 ? "[Lector] INSTALACION OK" : "[Lector] INSTALACION CON ERRORES: " + errores);
        if (Application.isBatchMode)
            EditorApplication.Exit(errores == 0 ? 0 : 1);
    }

    static GameObject BuscarVinculado(Scene escena)
    {
        foreach (var root in escena.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == FbxPath)
                    return t.gameObject;
        return null;
    }

    static GameObject BuscarLibre(Scene escena)
    {
        foreach (var root in escena.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<LectorCodigos>(true) != null)
                return root.GetComponentInChildren<LectorCodigos>(true).gameObject;
            if (root.name == "base" || root.name == "base(Clone)")
                return root;
        }
        return null;
    }

    static GameObject ClonarEn(GameObject origen, Scene destino)
    {
        var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(origen);
        var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);

        GameObject go;
        if (asset != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(asset, destino);
            PrefabUtility.SetPropertyModifications(go, PrefabUtility.GetPropertyModifications(origen));
            go.SetActive(origen.activeSelf);
        }
        else
        {
            go = Object.Instantiate(origen);
            SceneManager.MoveGameObjectToScene(go, destino);
        }

        go.transform.SetParent(null);
        go.name = origen.name;
        return go;
    }

    static void Configurar(GameObject lector)
    {
        var box = lector.GetComponent<BoxCollider>();
        if (box == null)
        {
            var lb = MallaLocalBounds(lector);
            box = lector.AddComponent<BoxCollider>();
            box.center = lb.center;
            box.size = lb.size;
        }

        var rb = lector.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = lector.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        var grab = lector.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            grab = lector.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.smoothPosition = true;
            grab.smoothRotation = true;
            grab.useDynamicAttach = false;
        }

        var audio = lector.GetComponent<AudioSource>();
        if (audio == null)
            audio = lector.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.dopplerLevel = 0f;
        audio.minDistance = 1f;
        audio.maxDistance = 15f;

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null)
            Debug.LogError("[Lector] no encontre el clip " + ClipPath);

        var lec = lector.GetComponent<LectorCodigos>();
        if (lec == null)
            lec = lector.AddComponent<LectorCodigos>();
        lec.audioLector = audio;
        lec.clipBip = clip;
        lec.lectorCollider = box;
        lec.distanciaLectura = 0.10f;
        lec.enfriamiento = 0.30f;
        lec.grab = grab;

        if (lector.name.EndsWith("(Clone)"))
            lector.name = lector.name.Substring(0, lector.name.Length - "(Clone)".Length);

        Debug.Log("[Lector] configurado '" + lector.name + "' colliderTam=" + box.size.ToString("F3")
            + " escala=" + lector.transform.lossyScale.ToString("F3")
            + " pos=" + lector.transform.position.ToString("F3"));
    }

    static Bounds MallaLocalBounds(GameObject raiz)
    {
        var mtx = raiz.transform.worldToLocalMatrix;
        bool primero = true;
        var b = new Bounds();

        foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;

            var lb = mf.sharedMesh.bounds;
            var m = mtx * mf.transform.localToWorldMatrix;

            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3(
                    (i & 1) == 0 ? lb.min.x : lb.max.x,
                    (i & 2) == 0 ? lb.min.y : lb.max.y,
                    (i & 4) == 0 ? lb.min.z : lb.max.z);
                var p = m.MultiplyPoint3x4(c);
                if (primero) { b = new Bounds(p, Vector3.zero); primero = false; }
                else b.Encapsulate(p);
            }
        }

        if (primero)
        {
            var world = new Bounds(raiz.transform.position, Vector3.one * 0.2f);
            var centroLocal = raiz.transform.InverseTransformPoint(world.center);
            b = new Bounds(centroLocal, Vector3.one * 0.2f);
            Debug.LogWarning("[Lector] sin mallas, collider generico de 0.2m");
        }

        return b;
    }
}
