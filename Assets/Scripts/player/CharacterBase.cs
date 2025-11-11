using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
public enum PlayerState
{
    Normal,
    Knockback
}

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

    [Header("Lógica de Ataque Básico")]
    [SerializeField, Tooltip("El punto en la mano que detecta el golpe")]
    private Transform hitPoint;

    [SerializeField, Tooltip("El radio del golpe (qué tan grande es el 'puño')")]
    private float hitRadius = 0.5f;

    [SerializeField, Tooltip("La fuerza con la que el puñete lanza objetos")]
    private float punchForce = 15f;

    [SerializeField, Tooltip("Qué capas (Layers) pueden ser golpeadas por el puñete")]
    private LayerMask hitableLayers;
    [SerializeField, Tooltip("Segundos desde que se presiona el botón hasta que se registra el golpe")]
    private float attackDelay = 0.3f; 
    [SerializeField, Tooltip("Tiempo total entre un ataque y el siguiente (cooldown)")]
    private float attackCooldown = 0.8f; 
    private float nextAttackTime = 0f;

    [Header("Lógica de Knockback")]
    [SerializeField, Tooltip("Segundos que el jugador queda en estado 'Knockback'")]
    private float knockbackDuration = 0.5f;
    [SerializeField, Tooltip("La fuerza vertical (hacia arriba) fija del golpe")]
    private float verticalKnockup = 7f;

    public NetworkVariable<PlayerState> CurrentState = new NetworkVariable<PlayerState>(PlayerState.Normal);
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
        if (!IsOwner || CurrentState.Value != PlayerState.Normal)
        {
            clientMoveInput = 0; 
            return;
        }
        clientMoveInput = context.ReadValue<float>();
    }
    public virtual void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner || CurrentState.Value != PlayerState.Normal) return;

        if (context.performed)
        {
            JumpServerRpc();
        }
    }
    public virtual void OnNormalAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner || CurrentState.Value != PlayerState.Normal) return;
        
        if (context.performed)
        {
            NormalAttackServerRpc();
        }
    }
    public virtual void OnUltimateAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner || CurrentState.Value != PlayerState.Normal) return;
        
        if (context.performed)
        {
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
        if (Time.time < nextAttackTime)
        {
            return;
        }
        nextAttackTime = Time.time + attackCooldown;
        Debug.Log("SERVIDOR: ¡Iniciando PUÑETE BASE!");
        animator.SetTrigger("NormalAttack");
        StartCoroutine(HitCheckDelay());
    }

    [Rpc(SendTo.Server)]
    protected virtual void UltimateAttackServerRpc()
    {
        Debug.Log("SERVIDOR: Ulti base (no hace nada)");
    }
    protected virtual void Update()
    {
        if (!IsOwner) return;

        UpdateServerMovementRpc(clientMoveInput);
    }

    protected virtual void FixedUpdate()
    {
        if (!IsServer) return;

        serverIsGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (CurrentState.Value == PlayerState.Normal)
        {
            HandleMovementAndRotation();
        }
        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("IsGrounded", serverIsGrounded);
    }

    private void HandleMovementAndRotation()
    {
        rb.linearVelocity = new Vector3(serverMoveInput * velocidad, rb.linearVelocity.y, 0f);

        if (serverMoveInput != 0) 
        {
 
            Quaternion targetRotation = (serverMoveInput > 0)
                                        ? Quaternion.Euler(0, 90, 0)
                                        : Quaternion.Euler(0, -90, 0);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
        }

      
    }

    public void HitCheck()
    {
        if (!IsServer) return;
        Debug.Log("SERVIDOR: ¡HitCheck! Buscando golpes en " + hitPoint.position);

        Collider[] hits = Physics.OverlapSphere(hitPoint.position, hitRadius, hitableLayers);

        foreach (Collider hit in hits)
        {
            if (hit.transform == this.transform) continue; 


            Vector3 direction = (hit.transform.position - transform.position).normalized + (Vector3.up * 0.3f);

            if (hit.TryGetComponent<CharacterBase>(out CharacterBase victimPlayer))
            {
                // 1. Calcular la dirección HORIZONTAL
                Vector3 horizontalDir = (hit.transform.position - transform.position);
                horizontalDir.y = 0; // Ignorar diferencia de altura
                horizontalDir.z = 0; // Estar seguros de que es 2D
                horizontalDir.Normalize(); // Dirección pura (izquierda o derecha)

                Debug.Log("SERVIDOR: ¡Golpe conectado con JUGADOR " + hit.name + "!");

                // 2. Llamar a ApplyKnockback SÓLO con la fuerza horizontal
                victimPlayer.ApplyKnockback(horizontalDir, punchForce);
            }
            // Opción 2: ¿Es un objeto (barril, etc.)?
            else if (hit.TryGetComponent<Rigidbody>(out Rigidbody objectRb))
            {
                // A los objetos sí les damos la dirección original (con el 'up')
                Vector3 objectDirection = (hit.transform.position - transform.position).normalized + (Vector3.up * 0.3f);
                Debug.Log("SERVIDOR: ¡Golpe conectado con OBJETO " + hit.name + "!");
                objectRb.AddForce(objectDirection * punchForce, ForceMode.Impulse);
            }
        }
    }
    private IEnumerator HitCheckDelay()
    {
        yield return new WaitForSeconds(attackDelay);

        HitCheck();
    }

    public void ApplyKnockback(Vector3 horizontalDirection, float horizontalForce)
    {
        if (!IsServer) return;

        if (CurrentState.Value != PlayerState.Normal) return;

        CurrentState.Value = PlayerState.Knockback;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        // 2. Aplica las dos fuerzas POR SEPARADO
        rb.AddForce(horizontalDirection * horizontalForce, ForceMode.Impulse); // Fuerza Horizontal (costado)
        rb.AddForce(Vector3.up * verticalKnockup, ForceMode.Impulse);          // Fuerza Vertical (arriba)

        StartCoroutine(KnockbackCooldown());
    }

    private IEnumerator KnockbackCooldown()
    {
        yield return new WaitForSeconds(knockbackDuration);

        // Resetea la velocidad por si acaso (opcional)
        // rb.velocity = Vector3.zero; 
        CurrentState.Value = PlayerState.Normal;
    }
}