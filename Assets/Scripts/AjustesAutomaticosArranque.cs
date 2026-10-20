using UnityEngine;

/// <summary>
/// Crea el objeto "Ajustes Automaticos" al cargar cada escena.
///
/// POR QUE HACE FALTA
/// ------------------
/// AjustesAutomaticos se encarga de dejar bien el juego en CUALQUIER escena (la del
/// menu y la del minimarket). Para que no haya que añadirlo a mano en cada escena,
/// se crea solo al cargarse la escena.
///
/// Se comprueba que no haya ya uno (por si el usuario lo ha puesto a mano en el
/// Inspector) y se crea uno nuevo en el caso contrario.
/// </summary>
public static class AjustesAutomaticosArranque
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Crear()
    {
        if (Object.FindFirstObjectByType<AjustesAutomaticos>() != null) return;

        var go = new GameObject("[Ajustes Automaticos]");
        go.AddComponent<AjustesAutomaticos>();
    }
}
