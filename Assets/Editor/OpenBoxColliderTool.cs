// COLOCAR EN: Assets/Editor/OpenBoxColliderTool.cs
// Convierte un Box Collider solido en una caja HUECA (paredes + piso) con repisas opcionales.
// Menu: Tools > MiniMarket > Colliders huecos (carrito y estantes)
using UnityEditor;
using UnityEngine;

public class OpenBoxColliderTool : EditorWindow
{
    enum OpenFace
    {
        Arriba,
        Frente_ZPositivo,
        Atras_ZNegativo,
        Derecha_XPositivo,
        Izquierda_XNegativo
    }

    OpenFace openFace = OpenFace.Arriba;
    int shelves = 0;
    float thickness = 0.04f;

    [MenuItem("Tools/MiniMarket/Colliders huecos (carrito y estantes)")]
    static void Open()
    {
        GetWindow<OpenBoxColliderTool>("Colliders huecos");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1) Selecciona el objeto que tiene el Box Collider (carrito, cesta, estante).\n" +
            "2) Elige la cara que debe quedar ABIERTA.\n" +
            "3) Pulsa Aplicar. El Box Collider solido se reemplaza por paredes y piso.\n" +
            "Carrito/cesta: cara abierta = Arriba, repisas = 0.\n" +
            "Estante: cara abierta = el lado por donde se meten las cosas, repisas = 2 a 4.",
            MessageType.Info);

        openFace = (OpenFace)EditorGUILayout.EnumPopup("Cara abierta", openFace);
        shelves = EditorGUILayout.IntSlider("Repisas intermedias", shelves, 0, 8);
        thickness = EditorGUILayout.Slider("Grosor de paredes (m)", thickness, 0.01f, 0.15f);

        EditorGUILayout.Space();
        if (GUILayout.Button("Aplicar a los objetos seleccionados", GUILayout.Height(32)))
            Apply();
    }

    static void AddBox(GameObject holder, Vector3 center, Vector3 size)
    {
        var b = Undo.AddComponent<BoxCollider>(holder);
        b.center = center;
        b.size = size;
    }

    void Apply()
    {
        int done = 0;

        foreach (var go in Selection.gameObjects)
        {
            var bc = go.GetComponent<BoxCollider>();
            if (!bc) continue;

            Vector3 c = bc.center;
            Vector3 s = bc.size;
            Vector3 ls = go.transform.lossyScale;

            // Grosor en metros reales, convertido al espacio local del objeto
            Vector3 t = new Vector3(
                thickness / Mathf.Max(Mathf.Abs(ls.x), 0.0001f),
                thickness / Mathf.Max(Mathf.Abs(ls.y), 0.0001f),
                thickness / Mathf.Max(Mathf.Abs(ls.z), 0.0001f));

            Undo.DestroyObjectImmediate(bc);

            var holder = new GameObject("Colliders");
            Undo.RegisterCreatedObjectUndo(holder, "Colliders huecos");
            holder.transform.SetParent(go.transform, false);

            float yMin = c.y - s.y / 2f;
            float yMax = c.y + s.y / 2f;

            // Piso (siempre)
            AddBox(holder, new Vector3(c.x, yMin + t.y / 2f, c.z), new Vector3(s.x, t.y, s.z));

            // Techo
            if (openFace != OpenFace.Arriba)
                AddBox(holder, new Vector3(c.x, yMax - t.y / 2f, c.z), new Vector3(s.x, t.y, s.z));

            // Paredes
            if (openFace != OpenFace.Derecha_XPositivo)
                AddBox(holder, new Vector3(c.x + s.x / 2f - t.x / 2f, c.y, c.z), new Vector3(t.x, s.y, s.z));
            if (openFace != OpenFace.Izquierda_XNegativo)
                AddBox(holder, new Vector3(c.x - s.x / 2f + t.x / 2f, c.y, c.z), new Vector3(t.x, s.y, s.z));
            if (openFace != OpenFace.Frente_ZPositivo)
                AddBox(holder, new Vector3(c.x, c.y, c.z + s.z / 2f - t.z / 2f), new Vector3(s.x, s.y, t.z));
            if (openFace != OpenFace.Atras_ZNegativo)
                AddBox(holder, new Vector3(c.x, c.y, c.z - s.z / 2f + t.z / 2f), new Vector3(s.x, s.y, t.z));

            // Repisas intermedias
            for (int i = 1; i <= shelves; i++)
            {
                float y = Mathf.Lerp(yMin + t.y / 2f, yMax - t.y / 2f, i / (float)(shelves + 1));
                AddBox(holder, new Vector3(c.x, y, c.z), new Vector3(s.x, t.y, s.z));
            }

            done++;
        }

        if (done == 0)
            EditorUtility.DisplayDialog("Colliders huecos",
                "Ninguno de los objetos seleccionados tiene un Box Collider.", "OK");
        else
            Debug.Log("Colliders huecos aplicados a " + done + " objeto(s). Selecciona el hijo 'Colliders' para verlos en verde.");
    }
}
