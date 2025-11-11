using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;


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
    private float attackDelay = 0.3f; // El 'delay' que pediste

    [SerializeField, Tooltip("Tiempo total entre un ataque y el siguiente (cooldown)")]
    private float attackCooldown = 0.8f; // El 'cooldown' que pediste

    // Variable del servidor para rastrear el cooldown
    private float nextAttackTime = 0f;

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

        HandleMovementAndRotation();

      
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

        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("IsGrounded", serverIsGrounded);
    }

    public void HitCheck()
    {
        if (!IsServer) return;

        Debug.Log("SERVIDOR: ¡HitCheck! Buscando golpes en " + hitPoint.position);

        Collider[] hits = Physics.OverlapSphere(hitPoint.position, hitRadius, hitableLayers);

        foreach (Collider hit in hits)
        {
            if (hit.transform == this.transform) continue;

            if (hit.TryGetComponent<Rigidbody>(out Rigidbody objectRb))
            {
                Debug.Log("SERVIDOR: ¡Golpe conectado con " + hit.name + "!");

                Vector3 direction = (hit.transform.position - transform.position).normalized + (Vector3.up * 0.3f);

                objectRb.AddForce(direction * punchForce, ForceMode.Impulse);
            }
        }
    }
    private IEnumerator HitCheckDelay()
    {
        // 1. ESPERAR EL TIEMPO DE RETRASO (el float del Inspector)
        yield return new WaitForSeconds(attackDelay);

        // 2. LLAMAR A LA FUNCIÓN DE GOLPE
        // (La función HitCheck() que ya tenías no necesita cambios)
        HitCheck();
    }
}