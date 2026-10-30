using UnityEngine;

/// <summary>
/// Configuracion de los clientes (NPC). Vive en Assets/Resources/ConfigClientes.asset
/// y la crea/llena sola la herramienta:
///     Tools/Minimarket/Clientes/1 - Configurar personajes y animaciones
///
/// Puedes cambiar los numeros desde el Inspector (clic en el asset).
/// </summary>
[CreateAssetMenu(fileName = "ConfigClientes", menuName = "Minimarket/Config Clientes")]
public class ConfigClientes : ScriptableObject
{
    [Header("Personajes (se llenan solos con la herramienta)")]
    public GameObject[] personajes;
    public RuntimeAnimatorController animador;
    [Tooltip("Camino horneado (Tools/Minimarket/Clientes/2 - Hornear camino de clientes).")]
    public UnityEngine.AI.NavMeshData camino;

    [Header("Cuantos y cada cuanto")]
    [Tooltip("Clientes dentro de la tienda a la vez (en Quest no pases de 4).")]
    [Range(1, 6)] public int maxClientes = 3;
    [Tooltip("Segundos hasta que entra el primer cliente.")]
    public float primerClienteEn = 6f;
    [Tooltip("Segundos entre un cliente y el siguiente (minimo, maximo).")]
    public Vector2 intervalo = new Vector2(25f, 45f);

    [Header("Compras")]
    [Tooltip("Cuantos productos lleva cada cliente (minimo, maximo).")]
    public Vector2Int productosPorCliente = new Vector2Int(1, 5);
    [Tooltip("Probabilidad de que pague con el monto exacto.")]
    [Range(0f, 1f)] public float probPagoExacto = 0.25f;
    [Tooltip("Segundos que espera en la caja antes de irse molesto.")]
    public float paciencia = 240f;

    [Tooltip("Probabilidad de que pague con tarjeta (POS) en vez de efectivo.")]
    [Range(0f, 1f)] public float probTarjeta = 0.35f;
    [Tooltip("Probabilidad de que tome un carrito al entrar (si hay uno libre cerca).")]
    [Range(0f, 1f)] public float probCarrito = 0.7f;

    [Header("Cuerpo y movimiento")]
    [Tooltip("Todos los clientes se escalan a esta altura (metros).")]
    public float alturaClientes = 1.72f;
    [Tooltip("Velocidad al caminar (m/s).")]
    public float velocidad = 1.15f;
    [Tooltip("Velocidad a la que 'camina' la animacion Walking. Si los pies patinan hacia " +
             "adelante, subela; si parecen ir en el sitio, bajala.")]
    public float velocidadAnimacion = 1.3f;

    [Header("Posiciones, relativas a la caja que tiene el lector")]
    [Tooltip("Donde se para el cliente que esta siendo atendido.")]
    public Vector3 puntoClienteLocal = new Vector3(1.25f, 0f, 0.25f);
    [Tooltip("Centro de la zona donde el cliente deja sus productos (la banda).")]
    public Vector3 zonaProductosLocal = new Vector3(0.50f, 0f, 0.50f);
    [Tooltip("Hacia donde crece la fila.")]
    public Vector3 direccionFilaLocal = new Vector3(0f, 0f, -1f);
    public float separacionFila = 0.9f;
}
