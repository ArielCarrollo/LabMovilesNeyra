using Unity.Netcode;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [Header("Stats")]
    [SerializeField] private int baseDamage = 10;
    [SerializeField] private float lifeTime = 3f;
    private int totalDamage;

    [Header("Movement")]
    [SerializeField] private float speed = 30f;
    [SerializeField] private float turnSpeed = 15f;

    // --- NUEVA VARIABLE ---
    // El radio en el que la bala "detona" automáticamente al acercarse al objetivo.
    // Un valor entre 0.5 y 1.0 suele funcionar bien.
    [SerializeField] private float explosionRadius = 0.8f;

    private ulong shooterOwnerId;
    private Transform target;
    private Rigidbody rb;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        if (IsServer)
        {
            Invoke(nameof(DestroySelf), lifeTime);
        }
    }

    // La función Initialize no necesita cambios
    public void Initialize(ulong shooterId, ulong targetId, int attackBonus)
    {
        if (!IsServer) return;
        this.shooterOwnerId = shooterId;
        this.totalDamage = baseDamage + attackBonus;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObject))
        {
            this.target = targetObject.transform;
            SetTargetClientRpc(targetId);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetTargetClientRpc(ulong targetId)
    {
        if (IsServer) return;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObject))
        {
            this.target = targetObject.transform;
        }
    }

    void FixedUpdate()
    {
        if (target != null)
        {
            // --- LÓGICA DE DETONACIÓN POR PROXIMIDAD ---
            // Solo el servidor necesita comprobar esto, ya que es quien inflige el daño.
            if (IsServer)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.position);
                if (distanceToTarget <= explosionRadius)
                {
                    // ¡Estamos lo suficientemente cerca! Forzamos el impacto y nos destruimos.
                    ApplyDamageToTarget();
                    DestroySelf();
                    return; // Detenemos la ejecución para evitar más movimiento.
                }
            }

            // La lógica de seguimiento original sigue aquí
            Vector3 direction = (target.position - rb.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed);
        }
        rb.linearVelocity = transform.forward * speed;
    }

    // --- LÓGICA DE IMPACTO REFACTORIZADA ---
    // La hemos movido a su propia función para poder llamarla desde dos sitios distintos.
    private void ApplyDamageToTarget()
    {
        if (!IsServer || target == null) return;

        if (target.TryGetComponent<EnemyAI>(out var enemy))
        {
            enemy.TakeDamage(totalDamage);
        }
        else if (target.TryGetComponent<SimplePlayerController>(out var player))
        {
            if (player.OwnerClientId != this.shooterOwnerId)
            {
                player.TakeDamage(totalDamage);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // La colisión física sigue siendo una forma válida de impactar.
        if (!IsServer) return;

        // Comprobamos si el objeto con el que hemos chocado es nuestro objetivo.
        if (other.transform == target)
        {
            ApplyDamageToTarget();
            DestroySelf();
        }
    }

    private void DestroySelf()
    {
        if (gameObject != null && IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }
}