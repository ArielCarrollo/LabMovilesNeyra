using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SimplePlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;

    [Header("Player Stats")]
    [SerializeField] private int maxHealth = 100;
    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Combat Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 4f;
    [SerializeField] private float shootingRange = 20f;
    [SerializeField] private float projectileSpeed = 30f; 

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.3f;
    [SerializeField] private LayerMask groundMask;

    private float nextFireTime = 0f;
    private Rigidbody rb;
    private Animator animator;
    private NetworkVariable<Vector3> serverMoveInput = new NetworkVariable<Vector3>();
    private NetworkVariable<bool> serverIsGrounded = new NetworkVariable<bool>();

    private Camera mainCamera;
    private Transform currentTarget;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
        }

        if (IsOwner)
        {
            UIManager.Instance.RegisterPlayer(CurrentHealth, maxHealth);

            mainCamera = Camera.main;
            FindObjectOfType<CameraController>()?.SetTarget(transform);
        }

        if (!IsServer && !IsOwner)
        {
            rb.isKinematic = true;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleMovementInput();

        UpdateTargetAndShoot();
    }

    private void UpdateTargetAndShoot()
    {
        Transform mouseTarget = null;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, shootingRange))
        {
            if (hit.collider.CompareTag("Enemy"))
            {
                mouseTarget = hit.transform;
            }
        }

        if (mouseTarget != null)
        {
            currentTarget = mouseTarget;
        }
        else
        {
            currentTarget = FindNearestEnemy();
        }

        if (Input.GetKey(KeyCode.Mouse0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + 1f / fireRate;

            if (currentTarget != null)
            {
                Vector3 direction = (currentTarget.position - firePoint.position).normalized;
                ShootServerRpc(direction, currentTarget.GetComponent<NetworkObject>().NetworkObjectId);
            }
        }
    }

    private Transform FindNearestEnemy()
    {
        return Physics.OverlapSphere(transform.position, shootingRange)
            .Where(col => col.CompareTag("Enemy"))
            .OrderBy(col => Vector3.Distance(transform.position, col.transform.position))
            .Select(col => col.transform)
            .FirstOrDefault();
    }

    [Rpc(SendTo.Server)]
    private void ShootServerRpc(Vector3 direction, ulong targetId)
    {
        nextFireTime = Time.time + 1f / fireRate;

        GameObject projectileInstance = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        NetworkObject netObj = projectileInstance.GetComponent<NetworkObject>();
        netObj.Spawn(true);

        projectileInstance.GetComponent<Projectile>().SetTarget(targetId);
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

    public void TakeDamage(int amount)
    {
        if (!IsServer) return;
        CurrentHealth.Value -= amount;
        if (CurrentHealth.Value <= 0)
        {
            Debug.Log($"Player {OwnerClientId} has died.");
            Destroy(gameObject);
        }
    }
    public void OnFootstep()
    {

    }
    public void OnLand()
    {

    }
}
