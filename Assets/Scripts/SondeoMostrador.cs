using System.Text;
using UnityEngine;

/// <summary>Diagnostico: mapa de alturas del mostrador (Logs) para rediseñarlo.</summary>
public static class SondeoMostrador
{
    public static void Sondear(Transform m)
    {
        var sb = new StringBuilder("[Sondeo] mostrador pos=" + m.position.ToString("F2") + " rotY=" + m.eulerAngles.y.ToString("F0") + " escala=" + m.lossyScale.ToString("F2") + "\n");
        foreach (var c in m.GetComponentsInChildren<Collider>()) sb.Append("  collider " + c.GetType().Name + " en " + c.name + "\n");
        sb.Append("  filas = z local de +1.0 a -1.0 ; columnas = x local de -1.0 a +1.0 ; valor = altura sobre el suelo en cm/10 (.=nada)\n");
        for (float z = 1.0f; z >= -1.001f; z -= 0.1f)
        {
            sb.Append(z.ToString("+0.0;-0.0")).Append(' ');
            for (float x = -1.0f; x <= 1.001f; x += 0.1f)
            {
                var p = m.TransformPoint(new Vector3(x, 0f, z));
                float best = -1f;
                foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider.transform.IsChildOf(m) && h.point.y - m.position.y > best) best = h.point.y - m.position.y;
                sb.Append(best < 0 ? " ." : ((int)(best * 10f)).ToString().PadLeft(2));
            }
            sb.Append('\n');
        }
        Debug.Log(sb.ToString());
    }
}
