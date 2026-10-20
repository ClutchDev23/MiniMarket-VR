using UnityEngine;

/// <summary>
/// Guarda la velocidad BASE de un proveedor de movimiento, para que el factor de
/// los ajustes ("Velocidad al caminar") se pueda aplicar varias veces sin que se
/// vaya encadenando.
///
/// Antes, al aplicar el factor se multiplicaba la velocidad que ya habia, y como
/// el factor se aplicaba al entrar en la escena y otra vez al mover el slider,
/// la velocidad se multiplicaba varias veces (x1 hacia x4, x8...).
///
/// Se anade solo al proveedor, en tiempo de ejecucion.
/// </summary>
public class VelocidadBaseMovimiento : MonoBehaviour
{
    [Tooltip("Velocidad base del proveedor, antes de aplicar el factor de los ajustes.")]
    public float baseVelocidad = 1f;
}
