using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine; // Importante: Añadir la referencia a Cinemachine

public class SimplePlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;

    [Header("Player Stats")]
    [SerializeField] private int maxHealth = 100;
    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();
    public NetworkVariable<int> AttackBonus = new NetworkVariable<int>();

    [Header("Combat Settings")]
    [SerializeField] private bool friendlyFireEnabled = false;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 4f;
    [SerializeField] private float shootingRange = 20f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.3f;
    [SerializeField] private LayerMask groundMask;

    private Rigidbody rb;
    private Animator animator;
    private NetworkVariable<Vector3> serverMoveInput = new NetworkVariable<Vector3>();
    private NetworkVariable<bool> serverIsGrounded = new NetworkVariable<bool>();
    private float nextFireTime = 0f;

    // Se eliminó la variable 'mainCamera' para evitar conflictos.
    private PlayerNicknameUI nicknameUI;
    private CinemachineCamera virtualCamera;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        nicknameUI = GetComponent<PlayerNicknameUI>();
    }

    public override void OnNetworkSpawn()
    {
        // Esta lógica se ejecuta en el servidor para inicializar las estadísticas del jugador.
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;

            // El servidor busca el nombre de usuario que corresponde a este jugador
            // y lo asigna a la variable de red del nickname.
            foreach (var playerData in GameManager.Instance.PlayersInLobby)
            {
                if (playerData.ClientId == OwnerClientId)
                {
                    nicknameUI.Nickname.Value = playerData.Username;
                    break;
                }
            }
        }

        // Esta lógica solo se ejecuta en la máquina del jugador que controla este personaje.
        if (IsOwner)
        {
            // Buscamos la cámara virtual de Cinemachine en la escena...
            virtualCamera = FindObjectOfType<CinemachineCamera>();
            if (virtualCamera != null)
            {
                // ...y le decimos que nos siga y nos mire.
                virtualCamera.Follow = this.transform;
                virtualCamera.LookAt = this.transform;
            }

            // Registramos la barra de vida en el UIManager local.
            if (UIManager.Instance != null)
            {
                UIManager.Instance.RegisterPlayer(CurrentHealth, maxHealth);
            }
        }
    }

    // Se eliminó el método Start() ya que su lógica fue movida a OnNetworkSpawn
    // para asegurar que se ejecute en el momento correcto del ciclo de vida de la red.

    void Update()
    {
        // La guardia de autoridad: solo el dueño puede procesar inputs.
        if (!IsOwner) return;

        HandleMovementInput();
        UpdateTargetAndShoot();
    }

    // --- Toda la lógica de combate, movimiento y daño permanece intacta ---

    public void TakeDamage(int amount)
    {
        if (!IsServer) return;
        CurrentHealth.Value -= amount;

        if (CurrentHealth.Value <= 0)
        {
            TriggerCameraShakeClientRpc();
            Respawn();
        }
    }

    [ClientRpc]
    private void TriggerCameraShakeClientRpc()
    {
        // Cada cliente le pide a su GameManager local que active el impulso de Cinemachine.
        GameManager.Instance.TriggerCameraShake();
    }

    // (El resto de tus funciones: Respawn, UpdateTargetAndShoot, FixedUpdate, etc. no necesitan cambios)
    private void UpdateTargetAndShoot()
    {
        if (Time.time >= nextFireTime && Input.GetButton("Fire1"))
        {
            Transform target = FindClosestTarget();
            if (target != null)
            {
                if (target.TryGetComponent<NetworkObject>(out var netObj))
                {
                    ShootServerRpc(netObj.NetworkObjectId);
                    nextFireTime = Time.time + 1f / fireRate;
                    animator.SetTrigger("Shoot");
                }
            }
        }
    }

    private Transform FindClosestTarget()
    {
        var enemies = Physics.OverlapSphere(transform.position, shootingRange)
            .Where(col => col.CompareTag("Enemy"))
            .Select(col => col.transform);

        var players = FindObjectsOfType<SimplePlayerController>()
            .Where(p => p.OwnerClientId != this.OwnerClientId)
            .Select(p => p.transform);

        var allTargets = friendlyFireEnabled ? enemies.Concat(players) : enemies;

        return allTargets
            .OrderBy(t => Vector3.Distance(transform.position, t.position))
            .FirstOrDefault();
    }

    [Rpc(SendTo.Server)]
    private void ShootServerRpc(ulong targetNetworkObjectId)
    {
        GameObject projectileGO = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        NetworkObject networkObject = projectileGO.GetComponent<NetworkObject>();
        networkObject.Spawn(true);

        Projectile projectile = projectileGO.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Initialize(OwnerClientId, targetNetworkObjectId, AttackBonus.Value);
        }
    }


    private void HandleMovementInput()
    {
        bool isGroundedNow = Physics.Raycast(groundCheck.position, Vector3.down, groundDistance, groundMask);
        Vector3 currentMoveInput = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;
        bool jumpPressed = Input.GetButtonDown("Jump") && isGroundedNow;
        if (jumpPressed) animator.SetTrigger("Jump");
        UpdateServerRpc(currentMoveInput, jumpPressed);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        serverIsGrounded.Value = Physics.Raycast(groundCheck.position, Vector3.down, groundDistance, groundMask);
        rb.linearVelocity = new Vector3(serverMoveInput.Value.x * moveSpeed, rb.linearVelocity.y, serverMoveInput.Value.z * moveSpeed);
        if (serverMoveInput.Value != Vector3.zero)
        {
            rb.rotation = Quaternion.LookRotation(serverMoveInput.Value);
        }
        HandleAnimationsOnServer();
    }

    private void HandleAnimationsOnServer()
    {
        float horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
        animator.SetFloat("Speed", horizontalVelocity);
        animator.SetFloat("MotionSpeed", serverMoveInput.Value.magnitude);
        animator.SetBool("Grounded", serverIsGrounded.Value);
        animator.SetBool("FreeFall", !serverIsGrounded.Value && rb.linearVelocity.y < -0.1f);
    }

    [Rpc(SendTo.Server)]
    private void UpdateServerRpc(Vector3 currentMoveInput, bool isJumping)
    {
        serverMoveInput.Value = currentMoveInput;
        if (isJumping && serverIsGrounded.Value)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            NotifyJumpClientRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyJumpClientRpc()
    {
        if (IsOwner) return;
        animator.SetTrigger("Jump");
    }
    public void ApplyAttackBuff(int bonus, float duration)
    {
        if (!IsServer) return;

        AttackBonus.Value += bonus;
    }

    private void Respawn()
    {
        if (!IsServer) return;

        CurrentHealth.Value = maxHealth;
        AttackBonus.Value = 0;

        Vector3 spawnPoint = GameSceneManager.Instance.GetRandomSpawnPoint();

        RespawnClientRpc(spawnPoint);
    }
    [ClientRpc]
    private void RespawnClientRpc(Vector3 spawnPosition)
    {
        if (TryGetComponent<CharacterController>(out var cc))
        {
            cc.enabled = false;
            transform.position = spawnPosition;
            cc.enabled = true;
        }
        else if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            transform.position = spawnPosition;
            rb.isKinematic = false;
        }
    }
    public void OnFootstep() { }
    public void OnLand() { }
}
