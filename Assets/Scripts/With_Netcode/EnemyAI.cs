using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Linq;

public class EnemyAI : NetworkBehaviour
{
    [Header("AI Settings")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float targetUpdateRate = 0.5f;

    [Header("Stats")]
    [SerializeField] private int maxHealth = 100;
    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(
            writePerm: NetworkVariableWritePermission.Server
        );

    private NavMeshAgent agent;
    private Transform currentTarget;

    [Header("Progression")]
    [SerializeField] private int xpValue = 15;

    public override void OnNetworkSpawn()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
            InvokeRepeating(nameof(FindNearestPlayer), 0f, targetUpdateRate);
        }

        if (!IsServer)
        {
            agent.enabled = false;
        }
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }
    public void TakeDamage(int damageAmount, ulong attackerId) // --- Firma modificada ---
    {
        if (!IsServer) return;

        CurrentHealth.Value -= damageAmount;

        if (CurrentHealth.Value <= 0)
        {
            // --- Otorgar XP al atacante ---
            //GameManager.Instance.AwardExperienceServerRpc(attackerId, xpValue);

            if (gameObject != null && IsSpawned)
            {
                GetComponent<NetworkObject>().Despawn();
            }
        }
    }

    private void Update()
    {
        if (!IsServer || currentTarget == null) return;
        agent.SetDestination(currentTarget.position);
    }

    private void FindNearestPlayer()
    {
        if (!IsServer) return;
        Transform nearestPlayer = null;
        float minDistance = float.MaxValue;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            float distance = Vector3.Distance(transform.position, client.PlayerObject.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPlayer = client.PlayerObject.transform;
            }
        }
        currentTarget = nearestPlayer;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (collision.gameObject.TryGetComponent<SimplePlayerController>(out var player))
        {
            player.TakeDamage(10);
            GetComponent<NetworkObject>().Despawn();
        }
    }
}