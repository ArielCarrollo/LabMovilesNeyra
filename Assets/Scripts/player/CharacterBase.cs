using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NetworkAnimator))]
public abstract class CharacterBase : NetworkBehaviour
{
    [Header("Stats Base del Personaje")]
    [SerializeField] protected float velocidad = 5f;
    [SerializeField] protected int vidaMaxima = 100;
    [SerializeField] protected int fuerzaBase = 10;
    [SerializeField] protected int nivelBase = 1;
    [SerializeField] protected float estaminaMaxima = 100f;

    [Header("Lógica de Movimiento")]
    [SerializeField, Tooltip("Qué tan rápido gira el personaje (más alto es más rápido)")]
    private float rotationSpeed = 15f;

    [Header("Lógica de Salto")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundDistance = 0.3f;

    [Header("Componentes")]
    protected Rigidbody rb;
    protected Animator animator;

    
    public NetworkVariable<int> Vida = new NetworkVariable<int>();
    public NetworkVariable<int> Fuerza = new NetworkVariable<int>();
    public NetworkVariable<int> Nivel = new NetworkVariable<int>();
    public NetworkVariable<float> Estamina = new NetworkVariable<float>();

    private float serverMoveInput;
    private bool serverIsGrounded;

    private float clientMoveInput;


    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        transform.rotation = Quaternion.Euler(0, 90, 0);
        if (IsServer)
        {
            Vida.Value = vidaMaxima;
            Fuerza.Value = fuerzaBase;
            Nivel.Value = nivelBase;
            Estamina.Value = estaminaMaxima;
        }
    }
    // --- MANEJO DE INPUT
    public virtual void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        clientMoveInput = context.ReadValue<float>();
    }
    public virtual void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.performed)
        {
            JumpServerRpc();
        }
    }
    public virtual void OnNormalAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (context.performed)
        {
            // El cliente pide al servidor ejecutar el puñete base
            NormalAttackServerRpc();
        }
    }
    public virtual void OnUltimateAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (context.performed)
        {
            // El cliente pide al servidor ejecutar la ulti
            UltimateAttackServerRpc();
        }
    }

    [Rpc(SendTo.Server)]
    protected virtual void UpdateServerMovementRpc(float moveInput)
    {
        this.serverMoveInput = moveInput;
    }

    [Rpc(SendTo.Server)]
    protected virtual void JumpServerRpc()
    {
        if (serverIsGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            // ¡NUEVO! Dispara el trigger de animación
            animator.SetTrigger("Jump");
        }
    }
    [Rpc(SendTo.Server)]
    protected virtual void NormalAttackServerRpc()
    {
        Debug.Log("SERVIDOR: ¡Ejecutando PUÑETE BASE!");
        animator.SetTrigger("NormalAttack");
    }

   
    [Rpc(SendTo.Server)]
    protected virtual void UltimateAttackServerRpc()
    {
        // Esta función está vacía a propósito.
        // El script "Ninja" o "Odin" la sobreescribirá con su Ulti.
        Debug.Log("SERVIDOR: Ulti base (no hace nada)");
    }
    protected virtual void Update()
    {
        if (!IsOwner) return;

        UpdateServerMovementRpc(clientMoveInput);
    }

    protected virtual void FixedUpdate()
    {
        // Solo el servidor tiene autoridad para mover el Rigidbody
        if (!IsServer) return;

        serverIsGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        HandleMovementAndRotation();

      
    }

    private void HandleMovementAndRotation()
    {
        // 1. APLICAR MOVIMIENTO (Esto ya lo tienes y está bien)
        rb.linearVelocity = new Vector3(serverMoveInput * velocidad, rb.linearVelocity.y, 0f);

        // 2. APLICAR ROTACIÓN SUAVE (FLIP)
        if (serverMoveInput != 0) // Solo rotar si hay input
        {
            // --- ¡ESTA ES LA LÓGICA CORREGIDA! ---

            // Determina la rotación objetivo (en grados Y)
            // 90 = Mirar a la derecha (eje X positivo)
            // -90 = Mirar a la izquierda (eje X negativo)
            Quaternion targetRotation = (serverMoveInput > 0)
                                        ? Quaternion.Euler(0, 90, 0)
                                        : Quaternion.Euler(0, -90, 0);

            // Usa Slerp para interpolar suavemente hacia la rotación objetivo
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
        }

        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("IsGrounded", serverIsGrounded);
    }


}