using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkTransform))]
public abstract class CharacterBase : NetworkBehaviour
{
    [Header("Stats Base del Personaje")]
    [SerializeField] protected float velocidad = 5f;
    [SerializeField] protected int vidaMaxima = 100;
    [SerializeField] protected int fuerzaBase = 10;
    [SerializeField] protected int nivelBase = 1;
    [SerializeField] protected float estaminaMaxima = 100f;

    [Header("Componentes")]
    protected Rigidbody rb;
    protected Animator animator;

    
    public NetworkVariable<int> Vida = new NetworkVariable<int>();
    public NetworkVariable<int> Fuerza = new NetworkVariable<int>();
    public NetworkVariable<int> Nivel = new NetworkVariable<int>();
    public NetworkVariable<float> Estamina = new NetworkVariable<float>();

   
    private float serverMoveInput;
    private bool isFacingRight = true;

    private float clientMoveInput;


   
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// OnNetworkSpawn se llama cuando el objeto se instancia en la red.
    /// Es el lugar correcto para inicializar NetworkVariables y l�gica de red.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // --- CONFIGURACI�N DE RIGIDBODY ---
        // Congelamos la rotaci�n en X/Z y el movimiento en Z para el 2.5D
        //rb.constraints = RigidbodyConstraints.FreezePositionZ | RigEidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (IsServer)
        {
            // El servidor inicializa las estad�sticas
            Vida.Value = vidaMaxima;
            Fuerza.Value = fuerzaBase;
            Nivel.Value = nivelBase;
            Estamina.Value = estaminaMaxima;
        }

        // Si eres el cliente due�o de este objeto...
        if (IsOwner)
        {
            // Aqu� ir�a la l�gica para activar tu c�mara (Cinemachine, etc.)
            // Ejemplo: FindObjectOfType<CinemachineVirtualCamera>().Follow = transform;
        }
    }

    /// <summary>
    /// Update se usa para leer inputs (en el cliente due�o)
    /// </summary>
    protected virtual void Update()
    {
        // Solo el cliente due�o de este objeto puede controlar el input
        if (!IsOwner) return;

        // Leemos el input localmente
        clientMoveInput = Input.GetAxisRaw("Horizontal");

        // Enviamos el input al servidor usando un ServerRpc
        UpdateServerMovementRpc(clientMoveInput);
    }

    /// <summary>
    /// FixedUpdate se usa para aplicar f�sica (en el servidor)
    /// </summary>
    protected virtual void FixedUpdate()
    {
        // Solo el servidor tiene autoridad para mover el Rigidbody
        if (!IsServer) return;

        HandleMovementAndRotation();
    }

    /// <summary>
    /// El servidor ejecuta esta l�gica de movimiento y rotaci�n.
    /// </summary>
    private void HandleMovementAndRotation()
    {
        // 1. APLICAR MOVIMIENTO
        // Mantenemos la velocidad vertical (gravedad/salto) y aplicamos la horizontal
        rb.linearVelocity = new Vector3(serverMoveInput * velocidad, rb.linearVelocity.y, 0f);

        // 2. APLICAR ROTACI�N (FLIP)
        // A diferencia del 2D, en 3D no cambiamos la 'scale', rotamos el objeto 180 grados en Y.
        if (serverMoveInput > 0 && !isFacingRight)
        {
            isFacingRight = true;
            // Mirar a la derecha (rotaci�n 0)
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else if (serverMoveInput < 0 && isFacingRight)
        {
            isFacingRight = false;
            // Mirar a la izquierda (rotaci�n 180)
            transform.rotation = Quaternion.Euler(0, 180, 0);
        }

        // 3. ACTUALIZAR ANIMATOR (El servidor lo controla)
        // float currentSpeed = Mathf.Abs(rb.velocity.x);
        // animator.SetFloat("Speed", currentSpeed);
    }

    // --- COMUNICACI�N DE RED (RPCs) ---

    /// <summary>
    /// [ServerRpc]
    /// El cliente due�o llama a esta funci�n. Se ejecuta EN EL SERVIDOR.
    /// </summary>
    [Rpc(SendTo.Server)]
    protected virtual void UpdateServerMovementRpc(float moveInput)
    {
        // El servidor almacena el input recibido para usarlo en FixedUpdate
        this.serverMoveInput = moveInput;
    }

    // Aqu� pondremos funciones comunes como TakeDamage()
    // [ServerRpc]
    // public virtual void TakeDamageServerRpc(int damage)
    // {
    //    if (!IsServer) return;
    //    Vida.Value -= damage;
    //    if (Vida.Value <= 0) Die();
    // }
}