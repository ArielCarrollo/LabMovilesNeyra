using Unity.Netcode;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [Header("Stats")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifeTime = 3f;

    [Header("Movement")]
    [SerializeField] private float speed = 30f;
    [SerializeField] private float turnSpeed = 15f; 

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

    public void SetTarget(ulong targetId)
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObject))
        {
            target = targetObject.transform;
            SetTargetClientRpc(targetId);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetTargetClientRpc(ulong targetId)
    {
        if (IsServer) return;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObject))
        {
            target = targetObject.transform;
        }
    }

    void FixedUpdate()
    {
        if (target != null)
        {
            Vector3 direction = (target.position - rb.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed);
        }

        rb.linearVelocity = transform.forward * speed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // Comprobamos si es un enemigo
        if (other.TryGetComponent<EnemyAI>(out var enemy))
        {
            enemy.TakeDamage(damage);
            DestroySelf();
            return; // Salimos para no seguir comprobando
        }

        // Comprobamos si es un jugador
        if (other.TryGetComponent<SimplePlayerController>(out var player))
        {
            // Nos aseguramos de no dañar al jugador que disparó la bala
            if (player.OwnerClientId != OwnerClientId)
            {
                player.TakeDamage(damage);
                DestroySelf();
            }
        }
    }

    private void DestroySelf()
    {
        if (gameObject != null && IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}